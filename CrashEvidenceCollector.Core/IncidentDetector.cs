using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

public sealed class IncidentDetector(EventLogReader eventLogReader, StructuredLog log)
{
    public async Task<IReadOnlyList<Incident>> DetectAsync(DateTimeOffset since, string? testDataDirectory, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.Now.AddMinutes(1);
        if (!string.IsNullOrWhiteSpace(testDataDirectory)) until = DateTimeOffset.MaxValue;
        var events = new List<EvidenceEvent>();
        foreach (var logName in new[] { "System", "Application" })
        {
            try { events.AddRange(await eventLogReader.ReadAsync(logName, since, until, testDataDirectory, cancellationToken).ConfigureAwait(false)); }
            catch (Exception ex) when (ex is not OperationCanceledException) { await log.WriteAsync("warning", "Incident log unavailable", new { logName, ex.Message }, cancellationToken).ConfigureAwait(false); }
        }

        var bootEvents = events.Where(IsBootEvent).OrderBy(x => x.Timestamp).ToList();
        var candidates = events.Select(ToIncident).Where(x => x is not null).Cast<Incident>()
            .OrderByDescending(CorrelationTimestamp).ToList();
        // Events 41 and 6008 frequently describe the same outage. Keep the most diagnostic record within two minutes.
        var correlated = new List<Incident>();
        foreach (var candidate in candidates)
        {
            var existingIndex = correlated.FindIndex(x => Math.Abs((CorrelationTimestamp(x) - CorrelationTimestamp(candidate)).TotalMinutes) <= 2 && IsShutdownFamily(x.Kind) && IsShutdownFamily(candidate.Kind));
            if (existingIndex >= 0)
            {
                correlated[existingIndex] = MergeShutdownEvidence(correlated[existingIndex], candidate);
                continue;
            }
            // Application Error and WER commonly emit several records for one process failure.
            // Collapse matching process/module reports within two minutes, retaining the primary Application Error record.
            existingIndex = correlated.FindIndex(x => IsSameApplicationFailure(x, candidate));
            if (existingIndex < 0) correlated.Add(candidate);
            else if (candidate.Source.Contains("Application Error/1000", StringComparison.OrdinalIgnoreCase)) correlated[existingIndex] = candidate;
        }
        return correlated.Select(incident =>
        {
            var previousBoot = bootEvents.LastOrDefault(entry => entry.Timestamp <= incident.Timestamp)?.Timestamp;
            var reportBoundary = (incident.RecordedAt ?? incident.Timestamp).AddMinutes(5);
            var nextBoot = bootEvents.FirstOrDefault(entry => entry.Timestamp > incident.Timestamp && entry.Timestamp <= reportBoundary)?.Timestamp;
            return incident with { BootTime = previousBoot, RebootTime = nextBoot };
        }).OrderByDescending(x => x.Timestamp).ToList();
    }

    public static Incident? ToIncident(EvidenceEvent entry)
    {
        var provider = entry.Provider;
        IncidentKind? kind;
        if (provider.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001 && entry.LogName.Equals("Application", StringComparison.OrdinalIgnoreCase))
        {
            // WER 1001 is a general report-status event, not synonymous with an application crash.
            // Only known crash event names become incidents; informational, update, telemetry,
            // LiveKernelEvent and report-queue records remain evidence but do not flood the timeline.
            var eventName = Find(entry.Data, "EventName", "EventType") ?? string.Empty;
            if (eventName.Length == 0) eventName = entry.Message;
            // Application-log BlueScreen entries describe WER report processing after boot.
            // Their P1 code is an unprefixed hexadecimal string (for example "20001"),
            // and Windows may write the same report several times. The authoritative
            // incident comes from System WER/1001, Kernel-Power/41 or EventLog/6008.
            kind = IsBlueScreenWer(eventName) ? null : IsApplicationCrashWer(eventName) ? IncidentKind.ApplicationCrash : null;
        }
        else kind = (provider, entry.EventId) switch
        {
            ("Microsoft-Windows-WER-SystemErrorReporting", 1001) or ("BugCheck", 1001) => IncidentKind.BugCheck,
            ("Microsoft-Windows-Kernel-Power", 41) => IncidentKind.PowerLossOrFreeze,
            ("EventLog", 6008) => IncidentKind.UnexpectedShutdown,
            ("Microsoft-Windows-WHEA-Logger", 18) => IncidentKind.HardwareError,
            ("Application Error", 1000) when entry.LogName.Equals("Application", StringComparison.OrdinalIgnoreCase) => IncidentKind.ApplicationCrash,
            _ => (IncidentKind?)null
        };
        if (kind is null) return null;
        string? code = null;
        var parameters = new List<string>();
        if (kind == IncidentKind.BugCheck || kind == IncidentKind.PowerLossOrFreeze)
        {
            var combined = Find(entry.Data, "BugcheckCode", "param1", "P1", "Bugcheck code");
            var hexValues = Regex.Matches(combined ?? string.Empty, @"0x[0-9a-fA-F]+", RegexOptions.CultureInvariant).Select(match => match.Value).ToList();
            code = hexValues.FirstOrDefault() ?? combined;
            if (hexValues.Count > 1) parameters.AddRange(hexValues.Skip(1).Take(4));
            else parameters.AddRange(new[] { Find(entry.Data, "BugcheckParameter1", "param2"), Find(entry.Data, "BugcheckParameter2", "param3"), Find(entry.Data, "BugcheckParameter3", "param4"), Find(entry.Data, "BugcheckParameter4", "param5") }.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>());
        }
        var normalizedCode = NormalizeCode(code);
        if (normalizedCode == "0x0") normalizedCode = null;
        var application = Find(entry.Data, "ApplicationName", "AppName", "FaultingApplicationName", "P1", "param1", "Data0");
        var title = kind switch { IncidentKind.BugCheck => $"Bugcheck {CodeDecoder.GetBugCheckLabel(normalizedCode)}", IncidentKind.PowerLossOrFreeze => normalizedCode is null ? "Kernel-Power unexpected restart" : $"Kernel-Power unexpected restart after bugcheck {CodeDecoder.GetBugCheckLabel(normalizedCode)}", IncidentKind.UnexpectedShutdown => "Unexpected shutdown", IncidentKind.HardwareError => "WHEA hardware error", _ => string.IsNullOrWhiteSpace(application) ? "Application crash" : $"Application crash — {Path.GetFileName(application)}" };
        var summary = entry.Message.Length == 0 ? $"{provider} event {entry.EventId}" : entry.Message;
        var incidentTimestamp = entry.Timestamp;
        var timestampBasis = "Source event timestamp";
        var timeConfidence = IncidentTimeConfidence.EventTimestamp;
        if (provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && entry.EventId == 6008 && TryReadPreviousShutdownTime(entry, out var previousShutdown))
        {
            incidentTimestamp = previousShutdown;
            timestampBasis = "EventLog 6008 reported previous unexpected-shutdown time";
            timeConfidence = IncidentTimeConfidence.Event6008ReportedShutdown;
        }
        else if ((provider.Equals("Microsoft-Windows-WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001) || (provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) && entry.EventId == 41))
        {
            timestampBasis = provider.Contains("WER", StringComparison.OrdinalIgnoreCase) ? "Post-reboot WER bugcheck registration time" : "Post-reboot Kernel-Power record time";
            timeConfidence = IncidentTimeConfidence.PostRebootReportTime;
        }
        return new(CreateId(entry), incidentTimestamp, kind.Value, title, summary, normalizedCode, parameters, Source: $"{entry.LogName}: {provider}/{entry.EventId}", RecordedAt: entry.Timestamp, TimestampBasis: timestampBasis, TimeConfidence: timeConfidence);
    }

    public static async Task<DateTimeOffset> ReadLastSuccessfulRunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(AppPaths.StateFile)) return DateTimeOffset.Now.AddDays(-7);
            var json = await File.ReadAllTextAsync(AppPaths.StateFile, cancellationToken).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<RunState>(json);
            return state?.LastSuccessfulDetection ?? DateTimeOffset.Now.AddDays(-7);
        }
        catch { return DateTimeOffset.Now.AddDays(-7); }
    }

    public static async Task MarkSuccessfulRunAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.StateDirectory);
        var json = JsonSerializer.Serialize(new RunState(DateTimeOffset.Now), JsonDefaults.Indented);
        await File.WriteAllTextAsync(AppPaths.StateFile, json, cancellationToken).ConfigureAwait(false);
    }

    private static string? Find(IReadOnlyDictionary<string, string>? data, params string[] keys) => data is null ? null : keys.Select(key => data.TryGetValue(key, out var value) ? value : null).FirstOrDefault(value => value is not null);
    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (ulong.TryParse(code, out var number)) return $"0x{number:X}";
        return code.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? "0x" + code[2..].ToUpperInvariant() : code;
    }
    private static string CreateId(EvidenceEvent entry) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{entry.Timestamp:O}|{entry.Provider}|{entry.EventId}|{entry.RecordId}")))[..16];
    private static bool IsShutdownFamily(IncidentKind kind) => kind is IncidentKind.BugCheck or IncidentKind.UnexpectedShutdown or IncidentKind.PowerLossOrFreeze;
    private static DateTimeOffset CorrelationTimestamp(Incident incident) => incident.RecordedAt ?? incident.Timestamp;
    private static Incident MergeShutdownEvidence(Incident left, Incident right)
    {
        var diagnostic = Priority(right.Kind) > Priority(left.Kind) ? right : left;
        var timing = left.TimeConfidence == IncidentTimeConfidence.Event6008ReportedShutdown ? left
            : right.TimeConfidence == IncidentTimeConfidence.Event6008ReportedShutdown ? right
            : left;
        return diagnostic with { Timestamp = timing.Timestamp, TimestampBasis = timing.TimestampBasis, TimeConfidence = timing.TimeConfidence };
    }

    public static bool TryReadPreviousShutdownTime(EvidenceEvent entry, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var combined = Find(entry.Data, "PreviousShutdownTime", "ShutdownTime");
        if (!string.IsNullOrWhiteSpace(combined) && TryParseLocalEventTime(combined, out timestamp)) return true;
        var time = Find(entry.Data, "Data0"); var date = Find(entry.Data, "Data1");
        return !string.IsNullOrWhiteSpace(time) && !string.IsNullOrWhiteSpace(date) && TryParseLocalEventTime($"{date} {time}", out timestamp);
    }

    private static bool TryParseLocalEventTime(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var cleaned = new string(value.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format).ToArray()).Trim();
        foreach (var culture in new[] { CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo("en-GB"), CultureInfo.InvariantCulture })
        {
            if (!DateTime.TryParse(cleaned, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed)) continue;
            var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            timestamp = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            return true;
        }
        return false;
    }

    private static bool IsSameApplicationFailure(Incident left, Incident right)
    {
        if (left.Kind != IncidentKind.ApplicationCrash || right.Kind != IncidentKind.ApplicationCrash || Math.Abs((left.Timestamp - right.Timestamp).TotalMinutes) > 2) return false;
        if (!left.Title.Equals("Application crash", StringComparison.OrdinalIgnoreCase) && left.Title.Equals(right.Title, StringComparison.OrdinalIgnoreCase)) return true;
        return left.Summary.Equals(right.Summary, StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsBlueScreenWer(string value) => value.Contains("BlueScreen", StringComparison.OrdinalIgnoreCase);
    private static bool IsApplicationCrashWer(string value) => new[] { "APPCRASH", "BEX", "CLR20R3", "MOAPPCRASH", "APPHANG" }.Any(name => value.Contains(name, StringComparison.OrdinalIgnoreCase));
    private static bool IsBootEvent(EvidenceEvent entry) => (entry.Provider.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase) && entry.EventId == 12) || (entry.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && entry.EventId == 6005);
    private static int Priority(IncidentKind kind) => kind switch { IncidentKind.BugCheck => 3, IncidentKind.PowerLossOrFreeze => 2, IncidentKind.UnexpectedShutdown => 1, _ => 0 };
    private sealed record RunState(DateTimeOffset LastSuccessfulDetection);
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
}
