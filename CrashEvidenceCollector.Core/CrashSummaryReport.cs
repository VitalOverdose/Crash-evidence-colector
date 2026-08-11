using System.Net;
using System.Text;
using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed record CrashSummaryEntry(
    DateTimeOffset Timestamp,
    IncidentKind Kind,
    string Title,
    string? BugCheckCode,
    string? BugCheckName,
    string? FailureBucket,
    string? Module,
    string? Process,
    string? ExceptionCode,
    ConfidenceLevel? Confidence,
    int? QualityScore,
    bool Collected,
    string? ReportPath);

/// <summary>
/// Selects which incidents appear in a summary. A filtered summary always states
/// what it excluded and how many records that removed: an evidence document that
/// hides its own filtering cannot be relied on by whoever reads it.
/// </summary>
public sealed record CrashSummaryFilter(
    bool IncludeBugChecks = true,
    bool IncludePowerAndShutdown = true,
    bool IncludeHardwareErrors = true,
    bool IncludeApplicationCrashes = true,
    bool OnlyWithCollectedEvidence = false,
    string? BugCheckCode = null,
    string? TextContains = null)
{
    public static readonly CrashSummaryFilter All = new();

    /// <summary>Kernel-side incidents only — the usual selection for a hardware warranty claim.</summary>
    public static readonly CrashSummaryFilter KernelOnly = new(IncludeApplicationCrashes: false);

    public bool Matches(CrashSummaryEntry entry)
    {
        var kindAllowed = entry.Kind switch
        {
            IncidentKind.BugCheck => IncludeBugChecks,
            IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown => IncludePowerAndShutdown,
            IncidentKind.HardwareError => IncludeHardwareErrors,
            IncidentKind.ApplicationCrash => IncludeApplicationCrashes,
            _ => true
        };
        if (!kindAllowed) return false;
        if (OnlyWithCollectedEvidence && !entry.Collected) return false;
        if (!string.IsNullOrWhiteSpace(BugCheckCode))
        {
            var wanted = CrashSummaryBuilder.NormalizeCode(BugCheckCode);
            if (!string.Equals(entry.BugCheckCode, wanted, StringComparison.OrdinalIgnoreCase)) return false;
        }
        if (!string.IsNullOrWhiteSpace(TextContains))
        {
            var haystack = string.Join(" ", new[] { entry.Title, entry.BugCheckName, entry.FailureBucket, entry.Module, entry.Process, entry.ExceptionCode }.Where(value => value is not null));
            if (!haystack.Contains(TextContains.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public string Describe()
    {
        if (this == All) return "No filter: every recorded incident in the range is included.";
        var included = new List<string>();
        if (IncludeBugChecks) included.Add("bugchecks");
        if (IncludePowerAndShutdown) included.Add("power/shutdown");
        if (IncludeHardwareErrors) included.Add("hardware errors");
        if (IncludeApplicationCrashes) included.Add("application crashes");
        var parts = new List<string> { included.Count == 0 ? "Including: nothing selected" : "Including: " + string.Join(", ", included) };
        if (OnlyWithCollectedEvidence) parts.Add("only incidents with collected evidence");
        if (!string.IsNullOrWhiteSpace(BugCheckCode)) parts.Add($"bugcheck code {CrashSummaryBuilder.NormalizeCode(BugCheckCode)}");
        if (!string.IsNullOrWhiteSpace(TextContains)) parts.Add($"text matching \"{TextContains.Trim()}\"");
        return string.Join("; ", parts) + ".";
    }
}

public sealed record CrashSummary(
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    string ComputerName,
    IReadOnlyList<CrashSummaryEntry> Entries,
    IReadOnlyList<MonitorEventRecord> HardwareEvents,
    CrashSummaryFilter Filter,
    int ExcludedByFilter)
{
    public int BugChecks => Entries.Count(entry => entry.Kind == IncidentKind.BugCheck);
    public int PowerOrShutdown => Entries.Count(entry => entry.Kind is IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown);
    public int ApplicationCrashes => Entries.Count(entry => entry.Kind == IncidentKind.ApplicationCrash);
    public int HardwareErrors => Entries.Count(entry => entry.Kind == IncidentKind.HardwareError);
    public int Collected => Entries.Count(entry => entry.Collected);
}

/// <summary>
/// Aggregates every incident in a chosen date range: retained collection reports
/// supply analysed detail, and detected-but-never-collected incidents are listed
/// as such rather than omitted. Counting is descriptive — the summary reports what
/// was recorded and how often, and never infers a cause from frequency alone.
/// </summary>
public static class CrashSummaryBuilder
{
    public static async Task<CrashSummary> BuildAsync(string outputRoot, DateTimeOffset from, DateTimeOffset to, IReadOnlyList<Incident> detectedIncidents, string? monitorDirectory, CancellationToken token, CrashSummaryFilter? filter = null)
    {
        filter ??= CrashSummaryFilter.All;
        var entries = new List<CrashSummaryEntry>();
        var collectedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in EnumerateReports(outputRoot))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var report = await JsonSerializer.DeserializeAsync<EvidenceReport>(stream, JsonDefaults.Indented, token).ConfigureAwait(false);
                if (report?.Incident is null) continue;
                var when = report.CrashTimestamp?.SelectedCrashTime ?? report.Incident.Timestamp;
                if (when < from || when > to) continue;
                if (!collectedIds.Add(report.Incident.Id)) continue;
                var primary = report.DumpAnalyses.FirstOrDefault(item => item.Association.IsPrimaryForIncident);
                NumericParser.TryParse(primary?.Analyze.BugCheckCode ?? report.Incident.BugCheckCode, out var code);
                entries.Add(new(
                    when,
                    report.Incident.Kind,
                    report.Incident.Title,
                    // Reports store codes in varied widths ("0x1E", "0x0000001E");
                    // grouping raw strings would split one code across several rows.
                    NormalizeCode(primary?.Analyze.BugCheckCode ?? report.Incident.BugCheckCode),
                    BugCheckKnowledge.Find(code)?.Name ?? primary?.Analyze.BugCheckString,
                    primary?.Analyze.FailureBucketId,
                    primary?.Analyze.ModuleName ?? report.ApplicationFailure?.FaultingModule,
                    primary?.Analyze.ProcessName ?? report.ApplicationFailure?.Application,
                    primary?.Analyze.ExceptionCode ?? report.ApplicationFailure?.ExceptionCode,
                    report.OverallAssessment?.Confidence,
                    report.AnalysisQuality?.Score,
                    true,
                    path));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
        }

        foreach (var incident in detectedIncidents.Where(item => item.Timestamp >= from && item.Timestamp <= to && !collectedIds.Contains(item.Id)))
        {
            NumericParser.TryParse(incident.BugCheckCode, out var code);
            entries.Add(new(incident.Timestamp, incident.Kind, incident.Title, NormalizeCode(incident.BugCheckCode), BugCheckKnowledge.Find(code)?.Name, null, null, null, null, null, null, false, null));
        }

        var hardware = monitorDirectory is null ? [] : await SafeLoadEventsAsync(monitorDirectory, from, to, token).ConfigureAwait(false);
        var kept = entries.Where(filter.Matches).OrderByDescending(entry => entry.Timestamp).ToList();
        return new(from, to, DateTimeOffset.Now, Environment.MachineName, kept, hardware, filter, entries.Count - kept.Count);
    }

    public static string? NormalizeCode(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : NumericParser.TryParse(raw, out var value) && value != 0 ? $"0x{value:X}" : raw;

    private static IEnumerable<string> EnumerateReports(string outputRoot)
    {
        if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot)) return [];
        try { return Directory.EnumerateFiles(outputRoot, "report.json", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Take(1000).ToList(); }
        catch { return []; }
    }

    private static async Task<IReadOnlyList<MonitorEventRecord>> SafeLoadEventsAsync(string directory, DateTimeOffset from, DateTimeOffset to, CancellationToken token)
    {
        try { return await MonitorLog.LoadEventsAsync(directory, from, to, token).ConfigureAwait(false); }
        catch { return []; }
    }
}

public static class CrashSummaryWriter
{
    public static string BuildPlainText(CrashSummary summary)
    {
        var sb = new StringBuilder();
        void Section(string title) { sb.AppendLine(); sb.AppendLine(title); sb.AppendLine(new string('=', title.Length)); }

        sb.AppendLine("CRASH SUMMARY REPORT");
        sb.AppendLine($"Machine: {summary.ComputerName}");
        sb.AppendLine($"Range: {summary.From.ToLocalTime():d MMM yyyy} to {summary.To.ToLocalTime():d MMM yyyy}");
        sb.AppendLine($"Generated: {summary.GeneratedAt.ToLocalTime():F}");
        sb.AppendLine($"Incidents: {summary.Entries.Count} ({summary.BugChecks} bugcheck, {summary.PowerOrShutdown} power/shutdown, {summary.HardwareErrors} hardware, {summary.ApplicationCrashes} application); {summary.Collected} with collected evidence.");
        sb.AppendLine($"Filter: {summary.Filter.Describe()}");
        if (summary.ExcludedByFilter > 0) sb.AppendLine($"NOTE: {summary.ExcludedByFilter} recorded incident(s) in this date range are not listed because of the filter above.");

        Section("INCIDENTS");
        if (summary.Entries.Count == 0) sb.AppendLine("No incidents were recorded in this range.");
        foreach (var entry in summary.Entries)
        {
            sb.AppendLine($"{entry.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm}  {entry.Kind,-18} {entry.BugCheckCode ?? "—",-12} {entry.BugCheckName ?? entry.Title}");
            var detail = new[]
            {
                entry.FailureBucket is null ? null : $"bucket {entry.FailureBucket}",
                entry.Module is null ? null : $"module {entry.Module}",
                entry.Process is null ? null : $"process {entry.Process}",
                entry.ExceptionCode is null ? null : $"exception {entry.ExceptionCode}",
                entry.Confidence is null ? null : $"confidence {entry.Confidence}",
                entry.QualityScore is null ? null : $"quality {entry.QualityScore}/100",
                entry.Collected ? null : "no evidence collected for this incident"
            }.Where(value => value is not null);
            if (detail.Any()) sb.AppendLine($"    {string.Join("; ", detail)}");
        }

        Section("REPEATED PATTERNS");
        foreach (var line in DescribePatterns(summary)) sb.AppendLine("- " + line);

        Section("HARDWARE EVENTS RECORDED BY BACKGROUND MONITORING");
        if (summary.HardwareEvents.Count == 0) sb.AppendLine("None recorded in this range (background monitoring only records while the app is running).");
        foreach (var group in summary.HardwareEvents.GroupBy(record => record.Summary))
            sb.AppendLine($"- {group.Count()}× {group.Key} (first {group.Min(item => item.Timestamp).ToLocalTime():g}, last {group.Max(item => item.Timestamp).ToLocalTime():g})");

        sb.AppendLine();
        sb.AppendLine("Counts describe what Windows and this tool recorded. Frequency alone does not establish a cause.");
        return sb.ToString();
    }

    public static string BuildHtml(CrashSummary summary)
    {
        static string H(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? string.Empty);
        var sb = new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Crash Summary Report</title><style>");
        sb.Append("body{font-family:'Segoe UI',sans-serif;background:#f4f7fb;color:#18202b;margin:0;line-height:1.45}header{background:#14233b;color:white;padding:26px 5vw}main{max-width:1240px;margin:24px auto;padding:0 24px}.card{background:white;border:1px solid #dce3ec;border-radius:12px;padding:20px;margin:14px 0}h1,h2{margin-top:0}h2{color:#17375e}.metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px}.metric{background:#eaf1fa;border-radius:9px;padding:12px}.metric small{display:block;color:#52677f}.metric strong{font-size:1.5rem}table{border-collapse:collapse;width:100%;font-size:.94rem}td,th{border-bottom:1px solid #e4e8ee;text-align:left;padding:8px;vertical-align:top}th{color:#44566d}code{font-family:Consolas,monospace}.meta{color:#5b6675}.uncollected{color:#8a6d1f}</style></head><body>");
        sb.Append($"<header><h1>Crash Summary Report</h1><div>{H(summary.ComputerName)}</div><div>{H(summary.From.ToLocalTime().ToString("d MMM yyyy"))} — {H(summary.To.ToLocalTime().ToString("d MMM yyyy"))}</div><div class='meta'>Generated {H(summary.GeneratedAt.ToLocalTime().ToString("F"))}</div></header><main>");

        sb.Append("<section class='card'><h2>Totals</h2><div class='metrics'>");
        foreach (var metric in new (string, int)[] { ("Incidents", summary.Entries.Count), ("Bugchecks", summary.BugChecks), ("Power / shutdown", summary.PowerOrShutdown), ("Hardware errors", summary.HardwareErrors), ("Application crashes", summary.ApplicationCrashes), ("Evidence collected", summary.Collected) })
            sb.Append($"<div class='metric'><small>{H(metric.Item1)}</small><strong>{metric.Item2}</strong></div>");
        sb.Append("</div>");
        sb.Append($"<p class='meta'><strong>Filter:</strong> {H(summary.Filter.Describe())}</p>");
        if (summary.ExcludedByFilter > 0)
            sb.Append($"<p class='uncollected'><strong>{summary.ExcludedByFilter} recorded incident(s)</strong> in this date range are not listed because of the filter above.</p>");
        sb.Append("</section>");

        sb.Append("<section class='card'><h2>Incidents</h2><table><tr><th>When</th><th>Type</th><th>Code</th><th>Debugger findings</th><th>Confidence / quality</th></tr>");
        if (summary.Entries.Count == 0) sb.Append("<tr><td colspan='5'>No incidents were recorded in this range.</td></tr>");
        foreach (var entry in summary.Entries)
        {
            var findings = string.Join("<br>", new[]
            {
                entry.FailureBucket is null ? null : $"bucket <code>{H(entry.FailureBucket)}</code>",
                entry.Module is null ? null : $"module <code>{H(entry.Module)}</code>",
                entry.Process is null ? null : $"process {H(entry.Process)}",
                entry.ExceptionCode is null ? null : $"exception <code>{H(entry.ExceptionCode)}</code>",
                entry.Collected ? null : "<span class='uncollected'>No evidence was collected for this incident.</span>"
            }.Where(value => value is not null));
            sb.Append($"<tr><td>{H(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))}</td><td>{H(entry.Kind)}</td><td>{H(entry.BugCheckCode ?? "—")}<br><span class='meta'>{H(entry.BugCheckName)}</span></td><td>{findings}</td><td>{H(entry.Confidence?.ToString() ?? "—")}{(entry.QualityScore is null ? string.Empty : $"<br><span class='meta'>{entry.QualityScore}/100</span>")}</td></tr>");
        }
        sb.Append("</table></section>");

        sb.Append("<section class='card'><h2>Repeated patterns</h2><ul>");
        foreach (var line in DescribePatterns(summary)) sb.Append($"<li>{H(line)}</li>");
        sb.Append("</ul><p class='meta'>Counts describe what Windows and this tool recorded. Frequency alone does not establish a cause.</p></section>");

        sb.Append("<section class='card'><h2>Hardware events recorded by background monitoring</h2>");
        if (summary.HardwareEvents.Count == 0) sb.Append("<p>None recorded in this range. Background monitoring only records while the app is running.</p>");
        else
        {
            sb.Append("<table><tr><th>Count</th><th>Event</th><th>First</th><th>Last</th></tr>");
            foreach (var group in summary.HardwareEvents.GroupBy(record => record.Summary))
                sb.Append($"<tr><td>{group.Count()}</td><td>{H(group.Key)}</td><td>{H(group.Min(item => item.Timestamp).ToLocalTime().ToString("g"))}</td><td>{H(group.Max(item => item.Timestamp).ToLocalTime().ToString("g"))}</td></tr>");
            sb.Append("</table>");
        }
        sb.Append("</section></main></body></html>");
        return sb.ToString();
    }

    public static IReadOnlyList<string> DescribePatterns(CrashSummary summary)
    {
        var lines = new List<string>();
        foreach (var group in summary.Entries.Where(entry => entry.BugCheckCode is not null).GroupBy(entry => entry.BugCheckCode!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).OrderByDescending(group => group.Count()))
            lines.Add($"{group.Key} ({group.First().BugCheckName ?? "unnamed"}) occurred {group.Count()} times.");
        foreach (var group in summary.Entries.Where(entry => entry.Module is not null).GroupBy(entry => entry.Module!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).OrderByDescending(group => group.Count()))
            lines.Add($"Module {group.Key} was named by the debugger in {group.Count()} incidents.");
        foreach (var group in summary.Entries.Where(entry => entry.FailureBucket is not null).GroupBy(entry => entry.FailureBucket!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).OrderByDescending(group => group.Count()))
            lines.Add($"Failure bucket {group.Key} repeated {group.Count()} times, which usually means the same defect.");

        var distinctCodes = summary.Entries.Where(entry => entry.Kind == IncidentKind.BugCheck && entry.BugCheckCode is not null).Select(entry => entry.BugCheckCode!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctCodes.Count >= 4)
            lines.Add($"{distinctCodes.Count} different bugcheck codes occurred in this range ({string.Join(", ", distinctCodes.Take(8))}). Varied stop codes across unrelated kernel components are more consistent with a systemic cause — memory, processor, firmware, or power — than with a single defective driver, but this pattern alone does not identify which.");
        var uncollected = summary.Entries.Count(entry => !entry.Collected);
        if (uncollected > 0) lines.Add($"{uncollected} incident(s) have no collected evidence, so their cause was never analysed.");
        if (lines.Count == 0) lines.Add("No repeated code, module, or failure bucket was found in this range.");
        return lines;
    }
}
