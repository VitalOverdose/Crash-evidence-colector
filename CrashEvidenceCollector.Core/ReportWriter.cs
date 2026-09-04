using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed class ReportWriter
{
    // 64px logo embedded so report.html stays fully self-contained.
    private static readonly Lazy<string?> LogoBase64 = new(() =>
    {
        try
        {
            using var stream = typeof(ReportWriter).Assembly.GetManifestResourceStream("CrashEvidenceCollector.Core.Assets.report-logo.png");
            if (stream is null) return null;
            using var memory = new MemoryStream(); stream.CopyTo(memory);
            return Convert.ToBase64String(memory.ToArray());
        }
        catch { return null; }
    });

    public async Task<(string Json, string Html, string Text, string Zip)> WriteAsync(EvidenceReport sourceReport, string outputDirectory, CollectionOptions options, CancellationToken cancellationToken)
    {
        var report = options.RedactAccountName ? new Redactor(Environment.UserName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Redact(sourceReport) : sourceReport;
        var jsonPath = Path.Combine(outputDirectory, "report.json");
        var htmlPath = Path.Combine(outputDirectory, "report.html");
        var textPath = Path.Combine(outputDirectory, "report.txt");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonDefaults.Indented), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(htmlPath, BuildHtml(report), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(textPath, BuildPlainText(report), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        var zipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        if (File.Exists(zipPath)) File.Delete(zipPath);
        await Task.Run(() => ZipFile.CreateFromDirectory(outputDirectory, zipPath, CompressionLevel.Optimal, false), cancellationToken).ConfigureAwait(false);
        return (jsonPath, htmlPath, textPath, zipPath);
    }

    public static string BuildPlainText(EvidenceReport report)
    {
        var primary = report.DumpAnalyses.FirstOrDefault(analysis => analysis.Association.IsPrimaryForIncident)
            ?? (report.DumpAnalyses.Count == 1 && report.DumpAnalyses[0].Association.Status == DumpAssociationStatus.Unassessed ? report.DumpAnalyses[0] : null);
        var code = primary?.Analyze.BugCheckCode ?? report.Incident.BugCheckCode ?? "Not recorded";
        NumericParser.TryParse(code, out var numericCode);
        // Resolve through the deep definitions, then the breadth catalogue, then
        // whatever the debugger printed. The plain title travels with the name.
        var name = BugCheckKnowledge.Find(numericCode)?.Name ?? BugCheckCatalog.Find(numericCode)?.Name ?? primary?.Analyze.BugCheckString ?? "Unresolved bugcheck";
        var plainTitle = BugCheckCatalog.Find(numericCode)?.PlainTitle;
        var codeHeadline = plainTitle is null ? name : $"{plainTitle} ({name})";
        var crashTime = report.CrashTimestamp.SelectedCrashTime ?? report.Incident.Timestamp;
        var sb = new StringBuilder();
        void Section(string title) { sb.AppendLine(); sb.AppendLine(title); sb.AppendLine(new string('=', title.Length)); }

        sb.AppendLine("CRASH EVIDENCE REPORT");
        foreach (var blocker in report.Observations.Where(item => item.Severity.Equals("Attention", StringComparison.OrdinalIgnoreCase) && item.Title.StartsWith("No dump evidence", StringComparison.OrdinalIgnoreCase)))
        { sb.AppendLine($"** {blocker.Title.ToUpperInvariant()} **"); sb.AppendLine(blocker.Detail); sb.AppendLine(); }
        sb.AppendLine($"Selected crash time: {crashTime.ToLocalTime():F}");
        sb.AppendLine($"Time source: {report.CrashTimestamp.SelectedCrashTimeSource} ({report.CrashTimestamp.Confidence})");
        sb.AppendLine(report.Incident.Kind == IncidentKind.ApplicationCrash ? $"Incident type: {report.Incident.Title}" : $"{code} — {codeHeadline}");
        sb.AppendLine($"Failure type: {report.Conclusion.FailureType}");
        sb.AppendLine($"Detection location: {report.Conclusion.DetectionLocation}");
        sb.AppendLine($"Active process context: {report.Conclusion.ActiveProcessContext} (context is not causation)");
        sb.AppendLine($"Underlying cause: {report.Conclusion.UnderlyingCause}");
        sb.AppendLine($"Culprit confidence: {report.Conclusion.CulpritConfidence}");
        sb.AppendLine($"Analysis quality: {report.AnalysisQuality.Level} ({report.AnalysisQuality.Score}/100)");

        Section("WHAT HAPPENED");
        sb.AppendLine(BugCheckKnowledge.Find(numericCode)?.DetailedExplanation ?? CodeDecoder.DescribeIncident(report.Incident));
        sb.AppendLine(report.OverallAssessment.Explanation);

        Section("CRASH TIME EVIDENCE");
        sb.AppendLine(report.CrashTimestamp.Explanation);
        sb.AppendLine($"Selected crash time: {FormatTime(report.CrashTimestamp.SelectedCrashTime)}");
        sb.AppendLine($"Previous / next boot: {FormatTime(report.CrashTimestamp.PreviousBootTime)} / {FormatTime(report.CrashTimestamp.NextBootTime)}");
        sb.AppendLine($"Event 6008 previous shutdown / recorded: {FormatTime(report.CrashTimestamp.Event6008PreviousShutdownTime)} / {FormatTime(report.CrashTimestamp.Event6008RecordedTime)}");
        sb.AppendLine($"Kernel-Power / WER record: {FormatTime(report.CrashTimestamp.KernelPowerEventTime)} / {FormatTime(report.CrashTimestamp.WerSystemErrorEventTime)}");
        sb.AppendLine($"Dump header / source modified: {FormatTime(report.CrashTimestamp.DumpHeaderTime)} / {FormatTime(report.CrashTimestamp.DumpSourceModifiedTime)}");

        Section(primary is null && report.Incident.Kind == IncidentKind.ApplicationCrash ? "APPLICATION CRASH EVIDENCE" : "PRIMARY DUMP");
        if (primary is null && report.Incident.Kind == IncidentKind.ApplicationCrash)
        {
            sb.AppendLine("Windows Application and WER records are the direct evidence for this application-level failure. A kernel crash dump is not expected or required.");
            if (report.ApplicationFailure is { } failure)
            {
                sb.AppendLine($"Failing application: {failure.Application ?? "Not recorded"}{(failure.ApplicationVersion is null ? string.Empty : $" version {failure.ApplicationVersion}")}");
                sb.AppendLine($"Faulting module: {failure.FaultingModule ?? "Not recorded"}{(failure.FaultingModuleVersion is null ? string.Empty : $" version {failure.FaultingModuleVersion}")}");
                sb.AppendLine($"Exception code: {failure.ExceptionCode ?? "Not recorded"}; fault offset: {failure.FaultOffset ?? "Not recorded"}; process id: {failure.ProcessId ?? "Not recorded"}");
                if (failure.ExceptionMeaning is not null) sb.AppendLine($"Exception meaning: {failure.ExceptionMeaning}");
                if (failure.ApplicationPath is not null) sb.AppendLine($"Application path: {failure.ApplicationPath}");
                if (failure.FaultingModulePath is not null) sb.AppendLine($"Faulting module path: {failure.FaultingModulePath}");
                if (failure.ReportId is not null) sb.AppendLine($"WER report id: {failure.ReportId}");
                foreach (var dump in failure.LocalDumpFiles)
                    sb.AppendLine($"Process crash dump available: {dump} — open it in a debugger for the exception type and stack this record does not contain.");
            }
        }
        else if (primary is null) sb.AppendLine("No dump was safely associated with the selected kernel incident. See the per-dump association reasons below.");
        else
        {
            sb.AppendLine($"Evidence path: {primary.Dump.CopiedPath}");
            sb.AppendLine($"Source path: {primary.Dump.SourcePath}");
            sb.AppendLine($"Dump header time: {FormatTime(primary.Dump.DumpHeaderTime)}");
            sb.AppendLine($"Source created / modified: {primary.Dump.CreationTime.ToLocalTime():F} / {primary.Dump.ModificationTime.ToLocalTime():F}");
            sb.AppendLine($"Copied-file created / modified: {primary.Dump.CopiedFileCreationTime.ToLocalTime():F} / {primary.Dump.CopiedFileModificationTime.ToLocalTime():F}");
            sb.AppendLine($"SHA-256: {primary.Dump.Sha256}");
            sb.AppendLine($"Association: {primary.Association.Status} — {primary.Association.Explanation}");
            sb.AppendLine(primary.Analyze.BugCheckCodeFromSelectedIncidentFallback
                ? $"Incident-event bugcheck (CDB did not confirm it): {primary.Analyze.BugCheckCode}"
                : $"Debugger-observed dump bugcheck: {primary.Analyze.BugCheckCode} {primary.Analyze.BugCheckString}");
            sb.AppendLine($"Parameters: {string.Join(", ", primary.Analyze.BugCheckParameters)}");
            sb.AppendLine($"Process context: {primary.Analyze.ProcessName ?? "Unavailable"} (context is not automatic causation)");
            sb.AppendLine($"Detection address: {primary.Analyze.ExceptionAddress ?? primary.Analyze.FaultingIp ?? "Unavailable"}");
            sb.AppendLine($"Module/symbol: {primary.Analyze.ModuleName ?? "Unavailable"} / {primary.Analyze.SymbolName ?? "Unavailable"}");
            sb.AppendLine($"Failure bucket: {primary.Analyze.FailureBucketId ?? "Unavailable"}");
            if (FailureBucketKnowledge.Describe(primary.Analyze.FailureBucketId) is { } bucketMeaning) sb.AppendLine($"Bucket meaning: {bucketMeaning}");
            foreach (var parameter in BugCheckKnowledge.DecodeParameters(numericCode, primary.Analyze.BugCheckParameters).Where(item => item.SymbolicValue is not null))
                sb.AppendLine($"Parameter {parameter.Index} ({parameter.Name}): {parameter.RawValue} — {parameter.SymbolicValue}");
        }

        Section("RECOMMENDATIONS");
        foreach (var item in report.Recommendations.OrderBy(item => item.Urgency))
        {
            sb.AppendLine($"- {item.Action}");
            sb.AppendLine($"  Why: {item.Reason}");
            sb.AppendLine($"  Urgency: {item.Urgency}; risk: {item.Risk}; reversible: {item.Reversible}; purpose: {item.Purpose}");
        }

        Section("INSTALLED SOFTWARE");
        sb.AppendLine($"{report.InstalledPrograms.Count} installed program(s) were recorded from the registry uninstall inventory; the full list is in report.html and report.json.");
        var recentInstalls = InstalledProgramInventory.RecentInstalls(report.InstalledPrograms, crashTime);
        if (recentInstalls.Count > 0)
        {
            sb.AppendLine($"Installed within {InstalledProgramInventory.RecentInstallWindow.TotalDays:0} days before the crash (installation timing alone does not establish causation):");
            foreach (var program in recentInstalls) sb.AppendLine($"- {string.Join(" ", new[] { program.Name, program.Version }.Where(value => !string.IsNullOrWhiteSpace(value)))} ({program.Publisher ?? "publisher not recorded"}) installed {program.InstallDate:yyyy-MM-dd}");
        }
        else sb.AppendLine($"No program installation date falls within the {InstalledProgramInventory.RecentInstallWindow.TotalDays:0} days before the crash. Registry install dates are day-resolution and can be absent, so this is not proof that nothing was installed.");
        foreach (var info in report.InstalledProgramWebInfo)
            sb.AppendLine(info.MatchedId is null
                ? $"  Web: {info.ProgramName} — {info.Note ?? "no winget match"}"
                : $"  Web: {info.ProgramName} matched winget package {info.MatchedId}; source-listed version {info.LatestVersion ?? "unknown"}; {info.Homepage ?? "no homepage recorded"} (online metadata by name match, not local evidence){(info.Note is null ? string.Empty : " — " + info.Note)}");

        Section("SIGNIFICANT TIMELINE");
        foreach (var item in SignificantTimeline(report.CorrelatedTimeline).Take(100))
            sb.AppendLine($"{item.RelativeLabel,-16} {item.Phase,-12} {item.Timestamp.ToLocalTime():G}  {item.Title} — {item.Explanation}");
        sb.AppendLine("Full timestamp-window events remain in report.json and Raw/*-events.json.");

        Section("ALL COPIED DUMP ANALYSES");
        foreach (var analysis in report.DumpAnalyses)
            sb.AppendLine($"- {Path.GetFileName(analysis.Dump.CopiedPath)}: dump code {analysis.Analyze.BugCheckCode ?? "unavailable"}; {analysis.Association.Status}; primary={analysis.Association.IsPrimaryForIncident}; delta={analysis.Association.TimestampDelta}; {analysis.Association.Explanation}");

        Section("EVIDENCE STATUS");
        foreach (var item in report.Evidence) sb.AppendLine($"- [{item.State}] {item.Category}: {item.Summary}{(item.Error is null ? string.Empty : " — " + item.Error)}");

        Section("OBSERVATIONS AND LIMITATIONS");
        foreach (var item in report.Observations) sb.AppendLine($"- [{item.Severity}] {item.Title}: {item.Detail}");
        foreach (var error in report.Errors) sb.AppendLine($"- Error preserved: {error}");
        return sb.ToString();
    }

    public static string BuildHtml(EvidenceReport report)
    {
        static string H(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? string.Empty);
        static string RelativeEvidencePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Unavailable";
            var marker = $"{Path.DirectorySeparatorChar}Raw{Path.DirectorySeparatorChar}";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? path[(index + 1)..].Replace('\\', '/') : Path.GetFileName(path);
        }
        static string OriginClass(EvidenceOrigin origin) => origin switch { EvidenceOrigin.DirectObservation => "direct", EvidenceOrigin.DebuggerConclusion => "debugger", EvidenceOrigin.AnalyzerInference => "inference", EvidenceOrigin.PossibleCorrelation => "correlation", _ => "unknown" };

        // Never choose a dump merely because its debugger output is richer. The
        // incident matcher must designate it; the one-item Unassessed fallback is
        // retained only for older JSON reports and isolated report-generation tests.
        var primary = report.DumpAnalyses.FirstOrDefault(analysis => analysis.Association.IsPrimaryForIncident)
            ?? (report.DumpAnalyses.Count == 1 && report.DumpAnalyses[0].Association.Status == DumpAssociationStatus.Unassessed ? report.DumpAnalyses[0] : null);
        var code = primary?.Analyze.BugCheckCode ?? report.Incident.BugCheckCode ?? "Not recorded";
        NumericParser.TryParse(code, out var numericCode);
        var definition = BugCheckKnowledge.Find(numericCode);
        var bugcheckName = definition?.Name ?? BugCheckCatalog.Find(numericCode)?.Name ?? primary?.Analyze.BugCheckString ?? "Unresolved bugcheck";
        // The number is what the reader searches for; the sentence is what they
        // understand. The report never shows one without the other.
        var bugcheckPlainTitle = BugCheckCatalog.Find(numericCode)?.PlainTitle;
        var bugcheckHeadline = bugcheckPlainTitle is null ? bugcheckName : $"{bugcheckPlainTitle} ({bugcheckName})";
        var assessment = report.OverallAssessment;
        var crashTime = report.CrashTimestamp.SelectedCrashTime ?? report.Incident.Timestamp;
        var isApplicationIncident = report.Incident.Kind == IncidentKind.ApplicationCrash;

        var sb = new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Crash Evidence Report</title><style>");
        sb.Append("body{font-family:'Segoe UI',sans-serif;background:#f4f7fb;color:#18202b;margin:0;line-height:1.45}header{background:#14233b;color:white;padding:30px 5vw}main{max-width:1320px;margin:24px auto;padding:0 24px}.card{background:white;border:1px solid #dce3ec;border-radius:12px;padding:20px;margin:14px 0;box-shadow:0 2px 8px #13233b10}h1,h2,h3{margin-top:0}h2{color:#17375e}.meta{color:#5b6675}.headline{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px}.metric{background:#eaf1fa;border-radius:9px;padding:12px}.metric small{display:block;color:#52677f}.metric strong{font-size:1.08rem}.badge{display:inline-block;padding:3px 9px;border-radius:12px;background:#e8eef8}.direct{border-left:5px solid #238552}.debugger{border-left:5px solid #2869be}.inference{border-left:5px solid #8b5fbf}.correlation{border-left:5px solid #d48a00}.unknown{border-left:5px solid #7d8793}.item{padding:10px 12px;margin:8px 0;background:#f9fbfd}.warning{background:#fff8e8;border-left:5px solid #d48a00;padding:12px}.failure{background:#fff0f0;border-left:5px solid #c73838;padding:12px}table{border-collapse:collapse;width:100%;font-size:.94rem}td,th{border-bottom:1px solid #e4e8ee;text-align:left;padding:8px;vertical-align:top}th{color:#44566d}code,pre{font-family:Consolas,monospace}pre{white-space:pre-wrap;word-break:break-word;max-height:620px;overflow:auto;background:#111c2d;color:#dce8f8;padding:14px;border-radius:8px}details{margin:10px 0}summary{cursor:pointer;font-weight:600;color:#245b98}.recommendation{border:1px solid #dce3ec;border-radius:9px;padding:14px;margin:10px 0}.timeline-phase{white-space:nowrap}.raw-ref{font-family:Consolas,monospace;font-size:.9rem}ul{padding-left:22px}</style></head><body>");
        var logo = LogoBase64.Value is null ? string.Empty : $"<img src='data:image/png;base64,{LogoBase64.Value}' alt='' style='width:64px;height:64px;float:left;margin-right:18px;border-radius:12px'>";
        sb.Append($"<header>{logo}<h1>Crash Evidence Report</h1><div>{H(report.Incident.Title)}</div><div>Selected crash time: {H(crashTime.ToLocalTime().ToString("F"))}</div><div>{H(report.CrashTimestamp.SelectedCrashTimeSource)} — {H(report.CrashTimestamp.Confidence)}</div></header><main>");

        // A blocking evidence failure belongs above the headline: without it the
        // reader cannot tell "inconclusive analysis" from "analysis never ran".
        foreach (var blocker in report.Observations.Where(item => item.Severity.Equals("Attention", StringComparison.OrdinalIgnoreCase) && item.Title.StartsWith("No dump evidence", StringComparison.OrdinalIgnoreCase)))
            sb.Append($"<section class='card failure'><h2>{H(blocker.Title)}</h2><p>{H(blocker.Detail)}</p></section>");

        // A. Headline: compact answers first, before technical evidence.
        sb.Append("<section class='card'><h2>A. Headline</h2><div class='headline'>");
        var firstMetric = isApplicationIncident ? ("Incident type", "Application crash") : ("Bugcheck", $"{code} — {bugcheckHeadline}");
        foreach (var metric in new[] { firstMetric, ("Failure type", report.Conclusion.FailureType), ("Detection location", report.Conclusion.DetectionLocation), ("Active process context", report.Conclusion.ActiveProcessContext), ("Underlying cause", report.Conclusion.UnderlyingCause), ("Culprit confidence", report.Conclusion.CulpritConfidence.ToString()), ("Analysis quality", $"{report.AnalysisQuality.Level} ({report.AnalysisQuality.Score}/100)") })
            sb.Append($"<div class='metric'><small>{H(metric.Item1)}</small><strong>{H(metric.Item2)}</strong></div>");
        sb.Append("</div><p class='meta'>Failure type and detection location describe where Windows detected the failure. Active process context is not automatic causation. A good dump can still leave the underlying cause undetermined.</p></section>");
        sb.Append("<section class='card'><h2>Crash-time evidence</h2>");
        sb.Append($"<p>{H(report.CrashTimestamp.Explanation)}</p><table>");
        foreach (var row in new[]
        {
            ("Selected crash time", FormatTime(report.CrashTimestamp.SelectedCrashTime)),
            ("Selected source / confidence", $"{report.CrashTimestamp.SelectedCrashTimeSource} / {report.CrashTimestamp.Confidence}"),
            ("Previous / next boot", $"{FormatTime(report.CrashTimestamp.PreviousBootTime)} / {FormatTime(report.CrashTimestamp.NextBootTime)}"),
            ("Event 6008 previous shutdown / record written", $"{FormatTime(report.CrashTimestamp.Event6008PreviousShutdownTime)} / {FormatTime(report.CrashTimestamp.Event6008RecordedTime)}"),
            ("Kernel-Power / WER record written", $"{FormatTime(report.CrashTimestamp.KernelPowerEventTime)} / {FormatTime(report.CrashTimestamp.WerSystemErrorEventTime)}"),
            ("Dump header / source modified", $"{FormatTime(report.CrashTimestamp.DumpHeaderTime)} / {FormatTime(report.CrashTimestamp.DumpSourceModifiedTime)}"),
            ("Copied-file created / modified", $"{FormatTime(report.CrashTimestamp.CopiedFileCreationTime)} / {FormatTime(report.CrashTimestamp.CopiedFileModifiedTime)}")
        }) sb.Append($"<tr><th>{H(row.Item1)}</th><td>{H(row.Item2)}</td></tr>");
        sb.Append("</table><p class='meta'>File creation/modification and post-boot reporting times are shown for provenance; they are not silently substituted for the crash time.</p></section>");

        // B. What happened: concise and intentionally distinguishes failure from cause.
        sb.Append("<section class='card'><h2>B. What happened</h2>");
        sb.Append($"<p>{H(definition?.DetailedExplanation ?? CodeDecoder.DescribeIncident(report.Incident))}</p>");
        if (!string.IsNullOrWhiteSpace(primary?.Analyze.ProcessName)) sb.Append($"<p>The dump records <strong>{H(primary.Analyze.ProcessName)}</strong> as the active process context. That fact does not show the process caused the kernel failure.</p>");
        sb.Append($"<p>The underlying cause is <strong>{H(report.Conclusion.UnderlyingCause)}</strong> at <strong>{H(report.Conclusion.CulpritConfidence)}</strong> confidence. {H(assessment.Explanation)}</p></section>");

        // C. Direct proof only; no inferred culprit language belongs in this list.
        sb.Append("<section class='card'><h2>C. What the evidence proves</h2><ul>");
        if (primary is null && isApplicationIncident)
        {
            sb.Append("<li>Windows recorded an application-level failure in the Application/WER evidence window. A kernel crash dump is not expected or required for this incident type, so historical BSOD dumps were not attached to it.</li>");
            if (report.ApplicationFailure is { } failure)
            {
                foreach (var row in new[]
                {
                    ("Failing application", $"{failure.Application ?? "Not recorded"}{(failure.ApplicationVersion is null ? string.Empty : $" version {failure.ApplicationVersion}")}"),
                    ("Faulting module", $"{failure.FaultingModule ?? "Not recorded"}{(failure.FaultingModuleVersion is null ? string.Empty : $" version {failure.FaultingModuleVersion}")}"),
                    ("Exception code", failure.ExceptionCode ?? "Not recorded"),
                    ("Fault offset / process id", $"{failure.FaultOffset ?? "Not recorded"} / {failure.ProcessId ?? "Not recorded"}"),
                    ("Application path", failure.ApplicationPath ?? "Not recorded"),
                    ("Faulting module path", failure.FaultingModulePath ?? "Not recorded"),
                    ("WER report id", failure.ReportId ?? "Not recorded")
                }.Where(row => row.Item2 != "Not recorded"))
                    sb.Append($"<li>{H(row.Item1)}: <code>{H(row.Item2)}</code>.</li>");
                if (failure.ExceptionMeaning is not null) sb.Append($"<li>Exception meaning: {H(failure.ExceptionMeaning)}</li>");
                foreach (var dump in failure.LocalDumpFiles)
                    sb.Append($"<li>A process crash dump for this application exists at <code>{H(dump)}</code> ({new FileInfo(dump).Length:N0} bytes). Open it in a debugger to obtain the exception type and stack this record does not contain.</li>");
            }
        }
        else if (primary is null)
            sb.Append("<li>No copied dump was safely associated with this selected kernel incident. The report preserves the exact rejection reason for every candidate instead of attaching a potentially wrong dump.</li>");
        else
        {
            sb.Append($"<li>The copied dump SHA-256 is <code>{H(primary.Dump.Sha256)}</code>; size {primary.Dump.FileSize:N0} bytes; classified as {H(primary.Dump.DumpType)}.</li>");
            if (primary.Analyze.BugCheckCodeFromSelectedIncidentFallback)
                sb.Append($"<li>Windows event evidence records bugcheck <code>{H(code)}</code> ({H(bugcheckName)}). CDB did not expose a dump-confirmed code before ending; the dump association is explicitly timestamp-only.</li>");
            else
                sb.Append($"<li>The debugger recorded bugcheck <code>{H(code)}</code> ({H(bugcheckName)}).</li>");
            if (primary.Analyze.BugCheckParameters.Count > 0) sb.Append($"<li>Raw parameters: {H(string.Join(", ", primary.Analyze.BugCheckParameters))}.</li>");
            if (!string.IsNullOrWhiteSpace(primary.Analyze.ExceptionCode)) sb.Append($"<li>Exception code: <code>{H(primary.Analyze.ExceptionCode)}</code>.</li>");
            if (!string.IsNullOrWhiteSpace(primary.Analyze.ExceptionAddress)) sb.Append($"<li>Exception/detection location from the exception record: <code>{H(primary.Analyze.ExceptionSymbol ?? primary.Analyze.ExceptionAddress)}</code> ({H(primary.Analyze.ExceptionAddress)}).</li>");
            if (!string.IsNullOrWhiteSpace(primary.Analyze.FaultingIp)) sb.Append($"<li>Faulting instruction: <code>{H(primary.Analyze.FaultingIp)}</code>.</li>");
            if (!string.IsNullOrWhiteSpace(primary.Analyze.FailureBucketId)) sb.Append($"<li>Debugger failure bucket: <code>{H(primary.Analyze.FailureBucketId)}</code>.{(FailureBucketKnowledge.Describe(primary.Analyze.FailureBucketId) is { } bucketMeaning ? " " + H(bucketMeaning) : string.Empty)}</li>");
            foreach (var parameter in BugCheckKnowledge.DecodeParameters(numericCode, primary.Analyze.BugCheckParameters).Where(item => item.SymbolicValue is not null))
                sb.Append($"<li>Parameter {parameter.Index} ({H(parameter.Name)}): <code>{H(parameter.RawValue)}</code> — {H(parameter.SymbolicValue)}.</li>");
            sb.Append($"<li>Symbol quality: {H(primary.Symbols.Quality)} — {H(primary.Symbols.Explanation)}</li>");
        }
        sb.Append("</ul></section>");

        // D/E. Inference and alternatives are visually distinct from observed facts.
        sb.Append($"<section class='card {OriginClass(assessment.Origin)}'><h2>D. Most likely explanation</h2><p><strong>{H(assessment.HeadlineComponent)} — {H(assessment.Confidence)}</strong></p><p>{H(assessment.Explanation)}</p><p class='meta'>This section is analyzer inference unless explicitly marked debugger-confirmed.</p></section>");
        sb.Append("<section class='card'><h2>E. Other plausible explanations</h2>");
        var alternatives = assessment.Candidates.Skip(1).Take(6).ToList();
        if (alternatives.Count == 0) sb.Append(isApplicationIncident
            ? "<p>The collected application event does not establish the lower-level reason for the process failure. Application logs, WER details, and repeated failure patterns remain the relevant leads.</p>"
            : "<p>The available evidence does not support a ranked alternative. Unknown remains possible.</p>");
        foreach (var candidate in alternatives) sb.Append($"<div class='item {OriginClass(EvidenceOrigin.AnalyzerInference)}'><strong>{H(candidate.Entity)} — {H(candidate.Confidence)}</strong><br>{H(candidate.Explanation)}</div>");
        sb.Append("</section>");

        // F. Only stack-relevant or debugger-named modules appear here.
        sb.Append("<section class='card'><h2>F. Involved modules and drivers</h2><table><tr><th>Module / product</th><th>Vendor / version</th><th>Role</th><th>Stack relationship</th><th>Evidence</th></tr>");
        var involved = primary?.Modules.Where(module => module.StackRelationship != StackRelationship.LoadedNotOnStack || module.ModuleName.Equals(primary.Analyze.ModuleName, StringComparison.OrdinalIgnoreCase) || module.ModuleName.Equals(primary.Analyze.ImageName, StringComparison.OrdinalIgnoreCase)).Take(20).ToList() ?? [];
        if (involved.Count == 0) sb.Append($"<tr><td colspan='5'>{(isApplicationIncident ? "Kernel dump module analysis does not apply to this application-level incident." : "No module relationship could be reliably parsed.")}</td></tr>");
        foreach (var module in involved)
        {
            var role = module.Category == ModuleCategory.MicrosoftKernelCore ? "Microsoft crash location/detecting framework unless stronger evidence shows origin." : module.Category.ToString();
            sb.Append($"<tr><td><strong>{H(module.ModuleName)}</strong><br>{H(module.ProductName)}</td><td>{H(module.CompanyName ?? "Unverified")}<br>{H(module.FileVersion)}</td><td>{H(role)}</td><td>{H(module.StackRelationship)}</td><td>{H(string.Join(" ", module.Evidence.Take(3)))}</td></tr>");
        }
        sb.Append("</table></section>");

        // G. Context and stack quality are expandable because most users need only the summary.
        sb.Append("<section class='card'><h2>G. Stack and exception details</h2>");
        if (primary is null) sb.Append(isApplicationIncident ? "<p>A kernel debugger stack is not expected for this application-level incident.</p>" : "<p>No debugger stack was available.</p>");
        else
        {
            var operation = primary.Analyze.ExceptionCode?.Equals("0xC0000005", StringComparison.OrdinalIgnoreCase) == true ? TypedCodeDecoders.DecodeAccessViolationOperation(primary.Analyze.ExceptionParameters, out var target) + $"; target {target} ({TypedCodeDecoders.ClassifyAddress(target)})" : "No access-violation operation was decoded.";
            sb.Append($"<p><strong>Exception:</strong> {H(primary.Analyze.ExceptionCode ?? "Unavailable")} &nbsp; <strong>Operation:</strong> {H(operation)}</p><p><strong>Context:</strong> {H(primary.Analyze.ContextRecord ?? "Unavailable")} &nbsp; <strong>Trap:</strong> {H(primary.Analyze.TrapFrame ?? "Unavailable")} &nbsp; <strong>Exception record:</strong> {H(primary.Analyze.ExceptionRecord ?? "Unavailable")}</p>");
            sb.Append($"<h3>{H(primary.FamilyAnalysis.Family)}</h3><ul>{string.Join(string.Empty, primary.FamilyAnalysis.Facts.Select(value => $"<li>{H(value)}</li>"))}</ul>");
            foreach (var limitation in primary.FamilyAnalysis.Limitations) sb.Append($"<div class='item correlation'>{H(limitation)}</div>");
            sb.Append($"<p><strong>Unwind quality:</strong> {H(primary.Symbols.Explanation)}</p><details><summary>Parsed stack frames ({primary.StackFrames.Count})</summary><table><tr><th>#</th><th>Module!symbol</th><th>Relationship</th><th>Transition</th><th>Raw frame</th></tr>");
            foreach (var frame in primary.StackFrames) sb.Append($"<tr><td>{frame.Index}</td><td>{H(frame.Module)}!{H(frame.Symbol)}{(frame.Displacement is null ? string.Empty : "+" + H(frame.Displacement))}</td><td>{H(frame.Relationship)}</td><td>{H(frame.TransitionKind)}</td><td><code>{H(frame.RawLine)}</code></td></tr>");
            sb.Append("</table></details>");
            foreach (var key in new[] { "r", ".exr", "exr", ".cxr", "cxr" }.Where(primary.Analyze.RawSections.ContainsKey).Distinct(StringComparer.OrdinalIgnoreCase))
                sb.Append($"<details><summary>Debugger context: {H(key)}</summary><pre>{H(primary.Analyze.RawSections[key])}</pre></details>");
        }
        sb.Append("</section>");

        // H. Report live health accurately without claiming Healthy/OK exhausts SMART.
        sb.Append("<section class='card'><h2>H. Storage and hardware evidence</h2><table>");
        foreach (var row in new[] { ("BIOS", report.Machine.Bios), ("CPU", report.Machine.Cpu), ("RAM", report.Machine.Memory), ("GPU", report.Machine.Gpu), ("Storage / Windows health", report.Machine.Storage), ("Virtualization / VBS", report.Machine.Virtualization), ("Page files", report.Machine.PageFiles), ("Dump configuration", report.Machine.CrashDumpConfiguration) }) sb.Append($"<tr><th>{H(row.Item1)}</th><td>{H(row.Item2)}</td></tr>");
        sb.Append("</table><p class='meta'>Windows Healthy/OK status is not proof that all vendor SMART attributes are healthy. Raw values are vendor-specific and are not treated as proof of the bugcheck cause.</p>");
        foreach (var device in report.StorageDevices)
        {
            sb.Append($"<details><summary>{H(device.Device)} — {H(device.HealthStatus ?? "health not reported")}</summary><p>{H(device.Source)}; operational: {H(device.OperationalStatus)}; temperature: {H(device.TemperatureCelsius?.ToString() ?? "unavailable")} °C; wear: {H(device.WearPercent?.ToString() ?? "unavailable")}%.</p>");
            if (device.Warnings.Count > 0) sb.Append($"<div class='warning'><strong>Health warning:</strong><ul>{string.Join(string.Empty, device.Warnings.Select(value => $"<li>{H(value)}</li>"))}</ul></div>");
            if (device.SmartAttributes.Count > 0)
            {
                sb.Append("<table><tr><th>ID</th><th>Attribute</th><th>Current / worst</th><th>Raw</th><th>Interpretation</th></tr>");
                foreach (var attribute in device.SmartAttributes) sb.Append($"<tr><td>{attribute.Id}</td><td>{H(attribute.Name)}</td><td>{H(attribute.Current)} / {H(attribute.Worst)}</td><td>{attribute.RawValue}</td><td>{H(attribute.Interpretation)}</td></tr>");
                sb.Append("</table>");
            }
            sb.Append("</details>");
        }
        sb.Append("</section>");

        // Installed software is inventory evidence only. Recent installs are shown
        // as a correlation lead; the full list stays collapsed to keep the report readable.
        var recentInstalls = InstalledProgramInventory.RecentInstalls(report.InstalledPrograms, crashTime);
        sb.Append($"<section class='card'><h2>Installed software</h2><p class='meta'>{report.InstalledPrograms.Count} program(s) were recorded from the registry uninstall inventory (Windows updates and hidden system components excluded). Presence in this list is inventory evidence only and does not indicate involvement in the crash.</p>");
        if (recentInstalls.Count > 0)
        {
            sb.Append($"<div class='item correlation'><strong>Installed within {InstalledProgramInventory.RecentInstallWindow.TotalDays:0} days before the crash</strong> — installation timing alone does not establish causation.<table><tr><th>Program</th><th>Version</th><th>Publisher</th><th>Installed</th></tr>");
            foreach (var program in recentInstalls) sb.Append($"<tr><td>{H(program.Name)}</td><td>{H(program.Version ?? "Not recorded")}</td><td>{H(program.Publisher ?? "Not recorded")}</td><td>{H(program.InstallDate?.ToString("yyyy-MM-dd") ?? "Not recorded")}</td></tr>");
            sb.Append("</table></div>");
        }
        else sb.Append($"<p>No program installation date falls within the {InstalledProgramInventory.RecentInstallWindow.TotalDays:0} days before the crash. Registry install dates are day-resolution and can be absent, so this is not proof that nothing was installed.</p>");
        if (report.InstalledProgramWebInfo.Count > 0)
        {
            sb.Append("<details open><summary>Online package metadata for recently installed programs</summary><p class='meta'>Matched by name against the winget community source. A name match is investigative context — it is not proof of identity and is not local evidence. Version differences are informational; vendors use differing version schemes.</p><table><tr><th>Program</th><th>Matched package</th><th>Source-listed version</th><th>Publisher</th><th>Description</th><th>Homepage</th></tr>");
            foreach (var info in report.InstalledProgramWebInfo)
                sb.Append($"<tr><td>{H(info.ProgramName)}</td><td>{(info.MatchedId is null ? H(info.Note ?? "No match") : $"{H(info.MatchedName)}<br><code>{H(info.MatchedId)}</code>{(info.Note is null ? string.Empty : $"<br><span class='meta'>{H(info.Note)}</span>")}")}</td><td>{H(info.LatestVersion ?? "—")}</td><td>{H(info.Publisher ?? "—")}</td><td>{H(info.Description ?? "—")}</td><td>{H(info.Homepage ?? "—")}</td></tr>");
            sb.Append("</table></details>");
        }
        sb.Append($"<details><summary>All installed programs ({report.InstalledPrograms.Count})</summary><table><tr><th>Program</th><th>Version</th><th>Publisher</th><th>Installed</th><th>Location</th><th>Source</th></tr>");
        foreach (var program in report.InstalledPrograms) sb.Append($"<tr><td>{H(program.Name)}</td><td>{H(program.Version ?? "—")}</td><td>{H(program.Publisher ?? "—")}</td><td>{H(program.InstallDate?.ToString("yyyy-MM-dd") ?? "—")}</td><td>{H(program.InstallLocation ?? "—")}</td><td>{H(program.Source)}</td></tr>");
        sb.Append("</table></details></section>");

        // I. Relative timeline prevents post-boot service noise being implied as pre-crash cause.
        sb.Append("<section class='card'><h2>I. Significant timeline</h2><p class='meta'>Crash-relative labels are used before reboot; boot-relative labels are used from Windows startup onward. Post-boot records are never displayed as occurring before reboot.</p><table><tr><th>Relative</th><th>Local time</th><th>Phase</th><th>Event</th><th>Interpretation</th></tr>");
        foreach (var item in SignificantTimeline(report.CorrelatedTimeline).Take(100)) sb.Append($"<tr><td class='timeline-phase'>{H(item.RelativeLabel)}</td><td>{H(item.Timestamp.ToLocalTime().ToString("G"))}</td><td>{H(item.Phase)}</td><td>{H(item.Title)}</td><td>{H(item.Explanation)}</td></tr>");
        sb.Append("</table><p class='meta'>The complete unfiltered event window is retained in the technical appendix, report.json, and Raw/*-events.json.</p></section>");

        // J. Quality and limitations remain explicit even when a candidate scores highly.
        sb.Append($"<section class='card'><h2>J. Confidence and limitations</h2><p><strong>{H(report.AnalysisQuality.Level)} ({report.AnalysisQuality.Score}/100)</strong> — {H(report.AnalysisQuality.Explanation)}</p><h3>Strengths</h3><ul>");
        foreach (var value in report.AnalysisQuality.Strengths) sb.Append($"<li>{H(value)}</li>");
        sb.Append("</ul><h3>Limitations</h3><ul>");
        foreach (var value in report.AnalysisQuality.Limitations.Concat(report.Errors).Distinct()) sb.Append($"<li>{H(value)}</li>");
        sb.Append("</ul>");
        if (report.ComparisonFindings.Count > 0) { sb.Append("<h3>Cross-incident comparison</h3>"); foreach (var finding in report.ComparisonFindings) sb.Append($"<div class='item {OriginClass(finding.Origin)}'><strong>{H(finding.Title)} — {H(finding.Confidence)}</strong><br>{H(finding.Explanation)}</div>"); }
        sb.Append("</section>");

        // K. Recommendations include why, risk, reversibility and diagnostic value.
        sb.Append("<section class='card'><h2>K. Recommendations</h2>");
        foreach (var recommendation in report.Recommendations) sb.Append($"<div class='recommendation'><strong>{H(recommendation.Action)}</strong><p>{H(recommendation.Reason)}</p><p class='meta'>Urgency: {H(recommendation.Urgency)} &middot; Risk: {H(recommendation.Risk)} &middot; Reversible: {H(recommendation.Reversible)} &middot; Purpose: {H(recommendation.Purpose)}</p><p><strong>Expected diagnostic value:</strong> {H(recommendation.ExpectedDiagnosticValue)}</p><details><summary>Supporting evidence</summary><ul>{string.Join(string.Empty, recommendation.SupportingEvidence.Select(value => $"<li>{H(value)}</li>"))}</ul></details></div>");
        sb.Append("</section>");

        // L. Raw references and hashes allow independent debugger verification.
        sb.Append("<section class='card'><h2>L. Raw evidence references</h2><table><tr><th>Dump / hash</th><th>Incident association</th><th>Debugger</th><th>Raw output</th><th>Status</th></tr>");
        foreach (var analysis in report.DumpAnalyses) sb.Append($"<tr><td><span class='raw-ref'>{H(RelativeEvidencePath(analysis.Dump.CopiedPath))}</span><br><code>SHA-256 {H(analysis.Dump.Sha256)}</code><br><span class='meta'>Source modified {H(analysis.Dump.ModificationTime.ToLocalTime().ToString("G"))}</span></td><td><strong>{H(analysis.Association.Status)}</strong>{(analysis.Association.IsPrimaryForIncident ? " — PRIMARY" : string.Empty)}<br>{H(analysis.Association.Explanation)}</td><td>{H(analysis.Dump.DebuggerVersion)}<br><span class='meta'>{H(analysis.Dump.SanitizedCommandLine)}</span></td><td class='raw-ref'>{H(RelativeEvidencePath(analysis.Dump.RawOutputPath))}<br>{H(RelativeEvidencePath(analysis.Dump.RawErrorPath))}</td><td>{H(analysis.Dump.Completion)}<br>Exit {H(analysis.Dump.ExitCode)}</td></tr>");
        sb.Append("</table></section>");

        // Compact appendices preserve V1 collection detail without overwhelming the headline.
        sb.Append("<section class='card'><h2>Technical appendices</h2><details><summary>Decoded codes</summary><table><tr><th>Category</th><th>Raw value</th><th>Name</th><th>Meaning</th></tr>");
        foreach (var item in report.CodeInterpretations) sb.Append($"<tr><td>{H(item.Category)}</td><td><code>{H(item.RawValue)}</code></td><td>{H(item.Name)}</td><td>{H(item.Explanation)} {H(item.ConvertedValue)}</td></tr>");
        sb.Append("</table></details><details><summary>Automated observations</summary>");
        foreach (var observation in report.Observations) sb.Append($"<div class='item correlation'><strong>{H(observation.Title)}</strong><br>{H(observation.Detail)}</div>");
        sb.Append("</details><details><summary>Evidence category status</summary><table><tr><th>Category</th><th>Status</th><th>Summary</th><th>Error</th></tr>");
        foreach (var item in report.Evidence) sb.Append($"<tr><td>{H(item.Category)}</td><td>{H(item.State)}</td><td>{H(item.Summary)}</td><td>{H(item.Error)}</td></tr>");
        sb.Append("</table></details><details><summary>All timestamp-window events</summary><table><tr><th>Time</th><th>Provider / ID</th><th>Interpretation</th><th>Original details</th></tr>");
        foreach (var item in report.Events.OrderBy(item => item.Timestamp)) sb.Append($"<tr><td>{H(item.Timestamp.ToLocalTime().ToString("G"))}</td><td>{H(item.Provider)} / {item.EventId}</td><td>{H(CodeDecoder.DescribeEvent(item))}</td><td>{H(item.Message)}</td></tr>");
        sb.Append("</table></details><details><summary>Preserved collection/parser errors</summary><pre>"); sb.Append(H(report.Errors.Count == 0 ? "None" : string.Join(Environment.NewLine, report.Errors))); sb.Append("</pre></details></section>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static string FormatTime(DateTimeOffset? value)
        => value is null ? "Unavailable" : value.Value.ToLocalTime().ToString("F");

    private static IEnumerable<CorrelatedTimelineEntry> SignificantTimeline(IEnumerable<CorrelatedTimelineEntry> entries)
        => CollapseBursts(entries.Where(item =>
            item.Title.Equals("Crash time anchor", StringComparison.OrdinalIgnoreCase)
            || item.Phase is TimelinePhase.Crash or TimelinePhase.Reboot or TimelinePhase.DumpCreation
            || item.Title.Contains("WHEA", StringComparison.OrdinalIgnoreCase)
            || item.Source.Contains("disk", StringComparison.OrdinalIgnoreCase)
            || item.Source.Contains("stor", StringComparison.OrdinalIgnoreCase)
            || item.Source.Contains("ntfs", StringComparison.OrdinalIgnoreCase)
            || item.Source.Contains("Kernel-PnP", StringComparison.OrdinalIgnoreCase)
            || item.Explanation.Contains("error", StringComparison.OrdinalIgnoreCase)
            || item.Explanation.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || item.Explanation.Contains("unexpected", StringComparison.OrdinalIgnoreCase)));

    // Bugcheck aftermath floods the log (WER can classify 70+ reports in seconds).
    // Three or more consecutive same-title records with small gaps collapse to one
    // row; every original record remains in report.json and the raw event files.
    public static IReadOnlyList<CorrelatedTimelineEntry> CollapseBursts(IEnumerable<CorrelatedTimelineEntry> entries, int minimumRun = 3, int maximumGapSeconds = 30)
    {
        var ordered = entries.ToList(); var result = new List<CorrelatedTimelineEntry>();
        for (var index = 0; index < ordered.Count;)
        {
            var run = 1;
            while (index + run < ordered.Count
                   && ordered[index + run].Title.Equals(ordered[index].Title, StringComparison.OrdinalIgnoreCase)
                   && (ordered[index + run].Timestamp - ordered[index + run - 1].Timestamp).Duration() <= TimeSpan.FromSeconds(maximumGapSeconds)) run++;
            if (run < minimumRun) { result.AddRange(ordered.Skip(index).Take(run)); index += run; continue; }
            var first = ordered[index]; var last = ordered[index + run - 1];
            result.Add(first with
            {
                Title = $"{first.Title} (×{run})",
                Explanation = $"{run} similar records between {first.Timestamp.ToLocalTime():HH:mm:ss} and {last.Timestamp.ToLocalTime():HH:mm:ss} were collapsed into this row. First record: {first.Explanation} All {run} original records remain in report.json and Raw/*-events.json."
            });
            index += run;
        }
        return result;
    }
}
