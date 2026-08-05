using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed record CrashMetricPoint(string Label, int Value);
public sealed record CrashDailyMetric(string Label, DateTimeOffset Day, int BugChecks, int PowerOrShutdown, int Hardware, int Applications)
{
    public int Total => BugChecks + PowerOrShutdown + Hardware + Applications;
}

public sealed record CrashMetricsSnapshot(
    int IncidentsInRange,
    int BugChecksInRange,
    int NewIncidentsInRange,
    int RetainedIncidentCount,
    IReadOnlyList<CrashMetricPoint> Trend,
    IReadOnlyList<CrashDailyMetric> DailyIncidentMix,
    IReadOnlyList<CrashMetricPoint> IncidentTypes,
    IReadOnlyList<CrashMetricPoint> TopBugChecks);

/// <summary>
/// Produces small, deterministic chart datasets from the live event timeline
/// and unique incidents retained in generated reports.
/// </summary>
public static class CrashMetricsCalculator
{
    public static CrashMetricsSnapshot Build(
        IReadOnlyList<Incident> incidentsInRange,
        IReadOnlyList<Incident> retainedIncidents,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        IReadOnlySet<string>? newIncidentIds = null,
        IReadOnlyList<Incident>? dailyHistory = null)
    {
        var visible = incidentsInRange
            .Where(item => item.Timestamp >= rangeStart && item.Timestamp <= rangeEnd)
            .ToList();
        var retained = retainedIncidents
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Timestamp).First())
            .ToList();

        var duration = rangeEnd - rangeStart;
        var bucket = duration <= TimeSpan.FromHours(6) ? TimeSpan.FromMinutes(30)
            : duration <= TimeSpan.FromDays(1) ? TimeSpan.FromHours(1)
            : duration <= TimeSpan.FromDays(7) ? TimeSpan.FromHours(12)
            : TimeSpan.FromDays(1);
        var bucketCount = Math.Clamp((int)Math.Ceiling(duration.TotalSeconds / bucket.TotalSeconds), 1, 60);
        var trend = new List<CrashMetricPoint>(bucketCount);
        for (var index = 0; index < bucketCount; index++)
        {
            var start = rangeStart + TimeSpan.FromTicks(bucket.Ticks * index);
            var end = index == bucketCount - 1 ? rangeEnd.AddTicks(1) : start + bucket;
            var label = duration <= TimeSpan.FromDays(1) ? start.ToLocalTime().ToString("HH:mm")
                : duration <= TimeSpan.FromDays(7) ? start.ToLocalTime().ToString("ddd HH")
                : start.ToLocalTime().ToString("dd MMM");
            trend.Add(new(label, visible.Count(item => item.Timestamp >= start && item.Timestamp < end)));
        }

        var types = new[]
        {
            new CrashMetricPoint("Bugchecks", visible.Count(item => item.Kind == IncidentKind.BugCheck)),
            new CrashMetricPoint("Power / shutdown", visible.Count(item => item.Kind is IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown)),
            new CrashMetricPoint("Hardware", visible.Count(item => item.Kind == IncidentKind.HardwareError)),
            new CrashMetricPoint("Applications", visible.Count(item => item.Kind == IncidentKind.ApplicationCrash))
        };

        // The daily incident-type chart deliberately keeps a stable 14-day
        // context even when the timeline is narrowed to the last hour.
        var history = dailyHistory ?? visible;
        var localEndDay = rangeEnd.ToLocalTime().Date;
        var daily = new List<CrashDailyMetric>(14);
        for (var offset = 13; offset >= 0; offset--)
        {
            var localDay = localEndDay.AddDays(-offset);
            var dayStart = new DateTimeOffset(localDay, TimeZoneInfo.Local.GetUtcOffset(localDay));
            var nextLocalDay = localDay.AddDays(1);
            var dayEnd = new DateTimeOffset(nextLocalDay, TimeZoneInfo.Local.GetUtcOffset(nextLocalDay));
            var entries = history.Where(item => item.Timestamp >= dayStart && item.Timestamp < dayEnd).ToList();
            daily.Add(new(
                localDay.ToString("dd MMM"),
                dayStart,
                entries.Count(item => item.Kind == IncidentKind.BugCheck),
                entries.Count(item => item.Kind is IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown),
                entries.Count(item => item.Kind == IncidentKind.HardwareError),
                entries.Count(item => item.Kind == IncidentKind.ApplicationCrash)));
        }

        // Retained reports are the durable history. Include currently visible
        // incidents only when they have not already been collected.
        var topCodeSource = retained
            .Concat(visible.Where(item => retained.All(saved => !saved.Id.Equals(item.Id, StringComparison.Ordinal))))
            .Where(item => NumericParser.TryParse(item.BugCheckCode, out var code) && code != 0)
            .GroupBy(item =>
            {
                NumericParser.TryParse(item.BugCheckCode, out var code);
                var name = BugCheckKnowledge.Find(code)?.Name;
                return name is null ? $"0x{code:X}" : $"0x{code:X} {name}";
            }, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(group => new CrashMetricPoint(group.Key, group.Count()))
            .ToList();

        return new(
            visible.Count,
            visible.Count(item => item.Kind == IncidentKind.BugCheck),
            newIncidentIds is null ? 0 : visible.Count(item => newIncidentIds.Contains(item.Id)),
            retained.Count,
            trend,
            daily,
            types,
            topCodeSource);
    }
}

/// <summary>
/// Reads only report summaries. Raw dumps and event files are never opened.
/// Malformed, moved, or older-schema reports are skipped independently.
/// </summary>
public static class ReportHistoryReader
{
    public static async Task<IReadOnlyList<Incident>> LoadIncidentsAsync(string outputRoot, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot)) return [];
        var incidents = new List<Incident>();
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(outputRoot, "report.json", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            }).OrderByDescending(path =>
            {
                try { return File.GetLastWriteTimeUtc(path); }
                catch { return DateTime.MinValue; }
            }).Take(500).ToList();
        }
        catch { return []; }

        foreach (var path in files)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var report = await JsonSerializer.DeserializeAsync<EvidenceReport>(stream, JsonDefaults.Indented, token).ConfigureAwait(false);
                if (report?.Incident is not null) incidents.Add(report.Incident);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
        }
        return incidents
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Timestamp).First())
            .OrderByDescending(item => item.Timestamp)
            .ToList();
    }
}
