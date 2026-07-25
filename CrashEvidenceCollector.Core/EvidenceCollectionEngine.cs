using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed class EvidenceCollectionEngine(EventLogReader eventReader, SystemEvidenceCollector systemCollector, FileEvidenceCollector fileCollector, ReportWriter reportWriter, StructuredLog log)
{
    private static readonly (string Log, string Category)[] Logs = [("System", "System events"), ("Application", "Application and WER events"), ("Microsoft-Windows-Kernel-PnP/Configuration", "Device installation events")];

    public async Task<CollectionResult> CollectAsync(Incident incident, IReadOnlyList<Incident> timeline, CollectionOptions options, IProgress<CollectionProgress>? progress, CancellationToken cancellationToken, Func<string, bool, CancellationToken, Task<HelperResponse>>? privilegedCopy = null)
    {
        options.Validate();
        var stamp = $"{incident.Timestamp:yyyyMMdd-HHmmss}-{incident.Id}";
        var outputDirectory = FileEvidenceCollector.UniquePath(options.OutputRoot, stamp);
        Directory.CreateDirectory(outputDirectory);
        var raw = Path.Combine(outputDirectory, "Raw"); Directory.CreateDirectory(raw);
        var report = new EvidenceReport { Incident = incident, Options = options, RelatedIncidents = timeline.ToList() };
        await log.WriteAsync("information", "Collection started", new { incident.Id, outputDirectory, options.IsTestDataMode }, cancellationToken).ConfigureAwait(false);

        progress?.Report(new(5, "Events", "Reading relevant event logs"));
        var from = incident.Timestamp.AddMinutes(-options.MinutesBefore);
        var to = await ResolveEvidenceEndAsync(incident, options, cancellationToken).ConfigureAwait(false);
        foreach (var (logName, category) in Logs)
        {
            try
            {
                var batch = await eventReader.ReadBatchAsync(logName, from, to, options.TestDataDirectory, cancellationToken).ConfigureAwait(false);
                var events = batch.Events;
                report.Events.AddRange(events);
                var path = Path.Combine(raw, logName.Replace('/', '_') + "-events.xml");
                var parsedPath = Path.Combine(raw, logName.Replace('/', '_') + "-events.json");
                await File.WriteAllTextAsync(path, batch.RawXml, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(parsedPath, JsonSerializer.Serialize(events, JsonDefaults.Indented), cancellationToken).ConfigureAwait(false);
                report.Evidence.Add(new(category, EvidenceState.Success, $"Collected {events.Count} event(s) from {from.ToLocalTime():G} to {to.ToLocalTime():G}; preserved raw XML and parsed JSON.", [path, parsedPath]));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { report.Errors.Add($"{category}: {ex.Message}"); report.Evidence.Add(new(category, EvidenceState.Warning, "Log unavailable or access denied.", Error: ex.Message)); }
        }

        progress?.Report(new(30, "System", "Collecting hardware, Windows and configuration inventory"));
        report.Machine = await systemCollector.CollectAsync(raw, options.TestDataDirectory, report.Errors, cancellationToken).ConfigureAwait(false);
        report.Evidence.Add(new("System and hardware", report.Machine.Windows == "Unavailable" ? EvidenceState.Warning : EvidenceState.Success, "Collected OS, BIOS, board, CPU, RAM, GPU, storage, boot and virtualization information."));
        report.Evidence.AddRange(await systemCollector.CollectTextInventoriesAsync(raw, options.TestDataDirectory, report.Errors, cancellationToken).ConfigureAwait(false));
        report.StorageDevices.AddRange(await StorageHealthAnalyzer.LoadAsync(raw, cancellationToken).ConfigureAwait(false));

        progress?.Report(new(62, "WER", "Copying Reliability Monitor and WER records"));
        var wer = await fileCollector.CopyWerAsync(incident, Path.Combine(raw, "WER"), options, cancellationToken).ConfigureAwait(false); report.Evidence.Add(wer); if (wer.Error is not null) report.Errors.Add(wer.Error);
        progress?.Report(new(72, "Dumps", "Streaming available crash dumps"));
        var dumpDirectory = Path.Combine(raw, "Dumps");
        var dumps = await fileCollector.CopyDumpsAsync(incident, dumpDirectory, options, cancellationToken, progress).ConfigureAwait(false); report.Evidence.Add(dumps); var dumpEvidenceIndex = report.Evidence.Count - 1; if (dumps.Error is not null) report.Errors.Add(dumps.Error);
        if (!options.IsTestDataMode && privilegedCopy is not null && dumps.State is EvidenceState.Warning or EvidenceState.Failure)
        {
            try
            {
                progress?.Report(new(78, "Administrator access", "Requesting access only for protected dump files"));
                var elevated = await privilegedCopy(dumpDirectory, options.IncludeFullMemoryDump, cancellationToken).ConfigureAwait(false);
                if (elevated.CopiedFiles.Count > 0)
                {
                    var fullDumpNote = options.IncludeFullMemoryDump ? "Full MEMORY.DMP was requested." : "Full MEMORY.DMP remained excluded by the privacy/size setting.";
                    report.Evidence[dumpEvidenceIndex] = new("Crash dumps", elevated.Errors.Count == 0 ? EvidenceState.Success : EvidenceState.Warning, $"Normal access was denied, then the elevated helper successfully copied {elevated.CopiedFiles.Count} dump(s). {fullDumpNote}", elevated.CopiedFiles, elevated.Errors.Count == 0 ? null : string.Join(Environment.NewLine, elevated.Errors), elevated.CopiedFiles.Sum(path => new FileInfo(path).Length));
                }
                report.Evidence.Add(new("Elevated dump access", elevated.Success ? EvidenceState.Success : EvidenceState.Warning, $"The helper copied {elevated.CopiedFiles.Count} protected file(s).", elevated.CopiedFiles, elevated.Errors.Count == 0 ? null : string.Join(Environment.NewLine, elevated.Errors)));
                report.Errors.AddRange(elevated.Errors);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { report.Errors.Add("Elevated helper: " + ex.Message); report.Evidence.Add(new("Elevated dump access", EvidenceState.Warning, "Administrator access was declined or unavailable; collection continued.", Error: ex.Message)); }
        }

        // Dump analysis runs only after copying/elevation has finished, so CDB never
        // needs administrative rights and never opens the protected source in place.
        progress?.Report(new(79, "Dump analysis", "Hashing and analyzing copied dumps with CDB"));
        var analyzer = new DumpAnalysisService(log);
        report.DumpAnalyses.AddRange(await analyzer.AnalyzeDirectoryAsync(dumpDirectory, raw, incident, report.Machine, report.Events, options, progress, cancellationToken).ConfigureAwait(false));
        DumpAnalysisResult? primary = null;
        if (report.DumpAnalyses.Count > 0)
        {
            primary = DumpIncidentMatcher.AssignPrimary(incident, report.DumpAnalyses);
            var history = await CrossIncidentEngine.LoadHistoryAsync(options.OutputRoot, outputDirectory, cancellationToken).ConfigureAwait(false);
            foreach (var analysis in report.DumpAnalyses)
            {
                analysis.Assessment = CulpritAssessmentEngine.Assess(analysis);
                analysis.Recommendations.Clear();
            }
            if (primary is not null)
            {
                if (primary.Fingerprint is not null) report.ComparisonFindings.AddRange(CrossIncidentEngine.Compare(primary.Fingerprint, history));
                primary.Assessment = CulpritAssessmentEngine.Assess(primary, report.ComparisonFindings);
                primary.Recommendations.AddRange(RecommendationEngine.Build(primary, report.Machine, report.Events, report.StorageDevices));
                report.OverallAssessment = primary.Assessment; report.AnalysisQuality = primary.Quality;
                report.Recommendations.AddRange(primary.Recommendations);
            }
            else
            {
                report.OverallAssessment = new() { HeadlineComponent = "No matching dump", Confidence = ConfidenceLevel.InsufficientEvidence, Explanation = "Copied dumps were preserved, but none safely matched the selected incident by bugcheck code and timestamp." };
                report.AnalysisQuality = new(AnalysisQualityLevel.Inconclusive, 0, "No dump was safely associated with the selected incident. Unrelated dump analyses are preserved separately.", [], report.DumpAnalyses.Select(item => item.Association.Explanation).ToList());
            }
            foreach (var unrelated in report.DumpAnalyses.Where(item => !item.Association.IsPrimaryForIncident))
                report.Observations.Add(new("Warning", "Dump excluded from incident headline", $"{Path.GetFileName(unrelated.Dump.CopiedPath)}: {unrelated.Association.Explanation}"));
            var failed = report.DumpAnalyses.Count(item => item.Dump.Completion is DebuggerCompletion.Failed or DebuggerCompletion.TimedOut or DebuggerCompletion.Unsupported);
            var associationState = primary is null ? EvidenceState.Warning : failed == 0 ? EvidenceState.Success : EvidenceState.Warning;
            var associationSummary = primary is null
                ? $"Analyzed {report.DumpAnalyses.Count} copied dump(s), but none matched the selected incident safely. Unrelated analyses were excluded from conclusions."
                : $"Associated {Path.GetFileName(primary.Dump.CopiedPath)} by {primary.Association.Status}; analysis quality: {report.AnalysisQuality.Level} ({report.AnalysisQuality.Score}/100). {report.DumpAnalyses.Count - 1} other dump(s) were retained separately.";
            report.Evidence.Add(new("Crash-dump analysis", associationState, associationSummary, report.DumpAnalyses.SelectMany(item => new[] { item.Dump.RawOutputPath, item.Dump.RawErrorPath }.OfType<string>()).ToList(), failed == 0 ? null : $"{failed} dump analysis attempt(s) were incomplete."));
        }
        else report.Evidence.Add(new("Crash-dump analysis", EvidenceState.Skipped, options.AnalyzeCrashDumps ? "No copied dump was available to analyze." : "Dump analysis was disabled in settings."));

        // Crash time and crash cause are independent conclusions. In particular,
        // post-boot WER and dump file times must never redefine the incident time.
        report.CrashTimestamp = CrashTimestampAnalyzer.Build(incident, report.Events, primary);
        report.Conclusion = IncidentConclusionEngine.Build(incident, primary, report.OverallAssessment);
        report.Observations.AddRange(CorrelationEngine.Analyze(incident, timeline, report.Events));
        report.CodeInterpretations.AddRange(CodeDecoder.Interpret(incident, report.Events));
        var recentChanges = report.Events.Count(e => (e.Provider.Contains("Servicing", StringComparison.OrdinalIgnoreCase) || e.Provider.Contains("Kernel-PnP", StringComparison.OrdinalIgnoreCase)) && e.Timestamp <= incident.Timestamp && incident.Timestamp - e.Timestamp < TimeSpan.FromDays(2));
        if (recentChanges > 0) report.Observations.Add(new("Info", "Recent system changes", $"Possible correlation: {recentChanges} update or device-change event(s) were recorded in the 48 hours before the incident. Timing alone does not establish causation."));
        report.CorrelatedTimeline.AddRange(EventTimelineEngine.Build(incident, report.Events, report.CrashTimestamp));

        progress?.Report(new(88, "Reports", "Generating redacted JSON and HTML reports"));
        var paths = await reportWriter.WriteAsync(report, outputDirectory, options, cancellationToken).ConfigureAwait(false);
        progress?.Report(new(100, "Complete", "Evidence archive created", EvidenceState.Success));
        await log.WriteAsync("information", "Collection completed", new { incident.Id, paths.Zip }, cancellationToken).ConfigureAwait(false);
        return new(report, outputDirectory, paths.Json, paths.Html, paths.Text, paths.Zip);
    }

    private async Task<DateTimeOffset> ResolveEvidenceEndAsync(Incident incident, CollectionOptions options, CancellationToken token)
    {
        try
        {
            // Search forward for the next recorded Windows startup, then include the configured post-startup window.
            var searchEnd = options.IsTestDataMode ? DateTimeOffset.MaxValue : DateTimeOffset.Now;
            var events = await eventReader.ReadAsync("System", incident.Timestamp, searchEnd, options.TestDataDirectory, token).ConfigureAwait(false);
            var startup = events.Where(e => (e.Provider.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase) && e.EventId == 12) || (e.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && e.EventId == 6005)).OrderBy(e => e.Timestamp).FirstOrDefault();
            return (startup?.Timestamp ?? incident.Timestamp).AddMinutes(options.MinutesAfterStartup);
        }
        catch { return incident.Timestamp.AddMinutes(options.MinutesAfterStartup); }
    }
}
