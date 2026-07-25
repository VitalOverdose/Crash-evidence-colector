using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public static class AnalysisQualityScorer
{
    public static AnalysisQuality Score(DumpAnalysisResult analysis)
    {
        var score = 0; var strengths = new List<string>(); var limitations = new List<string>();
        var dumpPoints = analysis.Dump.DumpType switch { DumpType.CompleteMemory => 25, DumpType.KernelMemory or DumpType.ActiveMemory => 22, DumpType.AutomaticMemory => 18, DumpType.SmallMemory or DumpType.WerMinidump => 10, _ => 6 };
        score += dumpPoints; strengths.Add($"{analysis.Dump.DumpType} supplied {dumpPoints}/25 dump-completeness points.");
        score += analysis.Symbols.Quality switch { SymbolQuality.Good => 20, SymbolQuality.Partial => 12, SymbolQuality.Poor => 4, _ => 0 };
        if (analysis.Symbols.Quality == SymbolQuality.Good) strengths.Add("Core symbols and most stack frames resolved."); else limitations.Add(analysis.Symbols.Explanation);
        if (!string.IsNullOrWhiteSpace(analysis.Analyze.BugCheckCode)) { score += 15; strengths.Add("!analyze produced a structured bugcheck result."); } else limitations.Add("No structured !analyze bugcheck result was parsed.");
        if (!string.IsNullOrWhiteSpace(analysis.Analyze.ContextRecord) || !string.IsNullOrWhiteSpace(analysis.Analyze.TrapFrame)) { score += 10; strengths.Add("A context or trap record was available."); } else limitations.Add("No readable context/trap record was available.");
        var resolved = analysis.StackFrames.Count(frame => frame.Resolved); var ratio = analysis.StackFrames.Count == 0 ? 0 : resolved / (double)analysis.StackFrames.Count;
        if (ratio >= .75) { score += 15; strengths.Add("The parsed stack was mostly resolved."); } else if (ratio >= .35) { score += 8; limitations.Add("The stack was only partially resolved."); } else limitations.Add("The stack was absent or poorly unwindable.");
        if (analysis.Analyze.BlackBoxes.Count > 0) { score += 5; strengths.Add("One or more black-box records were present."); }
        if (analysis.Modules.Any(module => !string.IsNullOrWhiteSpace(module.FileVersion) || !string.IsNullOrWhiteSpace(module.BaseAddress))) { score += 5; strengths.Add("Module metadata was available."); }
        if (analysis.Dump.Completion == DebuggerCompletion.Completed) score += 5; else limitations.Add($"Debugger completion was {analysis.Dump.Completion}.");
        score = Math.Clamp(score, 0, 100);
        var level = score switch { >= 85 => AnalysisQualityLevel.Excellent, >= 65 => AnalysisQualityLevel.Good, >= 40 => AnalysisQualityLevel.Limited, >= 20 => AnalysisQualityLevel.Poor, _ => AnalysisQualityLevel.Inconclusive };
        return new(level, score, $"Analysis quality is {level} ({score}/100). This measures evidence completeness, not certainty about a culprit.", strengths, limitations);
    }
}

public static class CulpritAssessmentEngine
{
    public static CulpritAssessment Assess(DumpAnalysisResult analysis, IReadOnlyList<ComparisonFinding>? comparisons = null)
    {
        var candidates = new Dictionary<string, CandidateAssessment>(StringComparer.OrdinalIgnoreCase);
        CandidateAssessment Candidate(string entity, CandidateKind kind)
        {
            if (!candidates.TryGetValue(entity, out var candidate)) candidates[entity] = candidate = new() { Entity = entity, Kind = kind };
            return candidate;
        }
        void Evidence(CandidateAssessment candidate, string description, int weight, EvidenceOrigin origin, string source)
        {
            candidate.SupportingEvidence.Add(new(description, weight, origin, source)); candidate.Score += weight;
        }

        var faultModule = NormalizeModule(analysis.Analyze.ModuleName ?? analysis.Analyze.ImageName ?? analysis.StackFrames.FirstOrDefault()?.Module);
        if (!string.IsNullOrWhiteSpace(faultModule))
        {
            var category = ModuleClassifier.Classify(faultModule, null, null); var candidate = Candidate(faultModule, ModuleClassifier.IsMicrosoft(category) ? CandidateKind.WindowsSubsystem : CandidateKind.Driver);
            if (!ModuleClassifier.IsMicrosoft(category) && analysis.StackFrames.FirstOrDefault()?.Module?.Equals(faultModule, StringComparison.OrdinalIgnoreCase) == true) Evidence(candidate, "The faulting instruction/top frame is inside this non-Microsoft module.", 55, EvidenceOrigin.DirectObservation, "STACK_TEXT / FAULTING_IP");
            else if (!ModuleClassifier.IsMicrosoft(category)) Evidence(candidate, "The debugger named this non-Microsoft module and it appears on the stack.", 28, EvidenceOrigin.DebuggerConclusion, "MODULE_NAME / IMAGE_NAME");
            else
            {
                Evidence(candidate, "Windows detected or handled the failure in this core component.", 8, EvidenceOrigin.DirectObservation, "MODULE_NAME / IMAGE_NAME");
                candidate.CounterEvidence.Add($"{faultModule} is a Microsoft framework/core component; its presence is usually a crash location or detector, not proof it created the defect.");
            }
        }

        foreach (var frame in analysis.StackFrames.Where(frame => frame.ModuleCategory is not (ModuleCategory.MicrosoftKernelCore or ModuleCategory.MicrosoftInboxDriver or ModuleCategory.UnknownUnverifiable)).Take(8))
        {
            var candidate = Candidate(frame.Module ?? "Unknown third-party frame", CandidateKind.Driver);
            var weight = frame.Relationship switch { StackRelationship.ExecutingAtFault => 50, StackRelationship.DirectCaller => 25, _ => 8 };
            Evidence(candidate, $"Module appears at stack frame {frame.Index} ({frame.Relationship}).", weight, EvidenceOrigin.DirectObservation, frame.RawLine);
        }

        if (!string.IsNullOrWhiteSpace(analysis.Analyze.ProcessName))
        {
            var process = Candidate(analysis.Analyze.ProcessName, CandidateKind.ApplicationContext);
            Evidence(process, "The process was active when Windows detected the kernel failure.", 2, EvidenceOrigin.DirectObservation, "PROCESS_NAME");
            process.CounterEvidence.Add("Process context alone does not show that the application caused the kernel failure.");
        }

        if (NumericParser.TryParse(analysis.Analyze.BugCheckCode, out var code))
        {
            if (code == 0x124) Evidence(Candidate("Hardware error reported through WHEA", CandidateKind.Hardware), "The bugcheck directly records an uncorrectable WHEA condition; the error record is required to identify its source.", 35, EvidenceOrigin.DirectObservation, "Bugcheck 0x124");
            if (code == 0x20001) Evidence(Candidate("Windows hypervisor", CandidateKind.WindowsSubsystem), "The bugcheck directly records a fatal hypervisor condition, but its underlying trigger is not identified by the code alone.", 35, EvidenceOrigin.DirectObservation, "Bugcheck 0x20001");
            if (code == 0x116) Evidence(Candidate("Graphics timeout/recovery path", CandidateKind.Hardware), "Windows failed to recover the graphics stack from a timeout.", 30, EvidenceOrigin.DirectObservation, "Bugcheck 0x116");
        }

        foreach (var familyEvidence in analysis.FamilyAnalysis.CandidateEvidence)
            Evidence(Candidate(familyEvidence.Entity, familyEvidence.Kind), familyEvidence.Description, familyEvidence.Weight, familyEvidence.Origin, familyEvidence.SourceReference);

        if (comparisons is not null)
            foreach (var finding in comparisons.Where(finding => finding.Confidence is ConfidenceLevel.ConfirmedByDebuggerEvidence or ConfidenceLevel.High or ConfidenceLevel.Moderate))
                foreach (var candidate in candidates.Values.Where(candidate => finding.Explanation.Contains(candidate.Entity, StringComparison.OrdinalIgnoreCase))) Evidence(candidate, finding.Explanation, 15, EvidenceOrigin.PossibleCorrelation, "Cross-incident comparison");

        foreach (var candidate in candidates.Values)
        {
            candidate.Score = Math.Clamp(candidate.Score, 0, 100);
            candidate.Confidence = candidate.Score switch { >= 75 => ConfidenceLevel.High, >= 45 => ConfidenceLevel.Moderate, >= 15 => ConfidenceLevel.Low, _ => ConfidenceLevel.InsufficientEvidence };
            // Poor symbols cap module attribution even when textual names appear persuasive.
            if (candidate.Kind == CandidateKind.Driver && analysis.Symbols.Quality is SymbolQuality.Poor or SymbolQuality.Unavailable && candidate.Confidence is ConfidenceLevel.ConfirmedByDebuggerEvidence or ConfidenceLevel.High or ConfidenceLevel.Moderate) { candidate.Confidence = ConfidenceLevel.Low; candidate.CounterEvidence.Add("Poor symbol quality caps driver attribution at Low confidence."); }
            candidate.Explanation = $"{candidate.Entity} has {candidate.Confidence} confidence from {candidate.SupportingEvidence.Count} supporting item(s). " + (candidate.CounterEvidence.Count > 0 ? string.Join(" ", candidate.CounterEvidence) : string.Empty);
        }
        var ordered = candidates.Values.OrderByDescending(candidate => candidate.Score).ToList(); var best = ordered.FirstOrDefault();
        if (best is null || best.Confidence == ConfidenceLevel.InsufficientEvidence) return new() { Candidates = ordered };
        return new() { HeadlineComponent = best.Entity, Confidence = best.Confidence, Origin = best.SupportingEvidence.Any(evidence => evidence.Origin == EvidenceOrigin.DirectObservation) ? EvidenceOrigin.AnalyzerInference : EvidenceOrigin.PossibleCorrelation, Explanation = best.Explanation, Candidates = ordered };
    }

    private static string? NormalizeModule(string? value) => string.IsNullOrWhiteSpace(value) ? null : Path.GetFileName(value.Split('!', '+')[0]).Trim();
}

public static class RecommendationEngine
{
    public static IReadOnlyList<Recommendation> Build(DumpAnalysisResult analysis, MachineSnapshot machine, IReadOnlyList<EvidenceEvent> events, IReadOnlyList<StorageDeviceHealth>? storageDevices = null)
    {
        var recommendations = new List<Recommendation>(); var code = NumericParser.TryParse(analysis.Analyze.BugCheckCode, out var parsed) ? parsed : 0;
        var topDriver = analysis.Assessment.Candidates.FirstOrDefault(candidate => candidate.Kind == CandidateKind.Driver && candidate.Confidence is ConfidenceLevel.ConfirmedByDebuggerEvidence or ConfidenceLevel.High or ConfidenceLevel.Moderate);
        if (topDriver is not null)
            recommendations.Add(new($"Check the vendor release notes and update or roll back {topDriver.Entity} using the device/OEM-supported package.", "The dump places this non-Microsoft driver at or immediately beside the fault with at least moderate evidence.", topDriver.SupportingEvidence.Select(item => item.Description).ToList(), RecommendationUrgency.Soon, RecommendationRisk.Low, true, "A version change tests a specific evidence-backed driver hypothesis.", RecommendationPurpose.IsolationTest));
        if (analysis.Symbols.Quality is SymbolQuality.Poor or SymbolQuality.Unavailable)
            recommendations.Add(new("Keep the current dump and rerun analysis after Microsoft symbols are available; collect a kernel dump if the minidump lacks required pages.", analysis.Symbols.Explanation, [analysis.Symbols.Explanation], RecommendationUrgency.Soon, RecommendationRisk.Low, true, "Better symbols/dump context can turn unresolved addresses into reliable modules and frames.", RecommendationPurpose.FurtherCollection));
        if (code == 0x20001 || code == 0x18B || code == 0x101)
            recommendations.Add(new("Review the motherboard vendor's BIOS, Intel ME/chipset and CPU microcode release notes; first test at vendor-default CPU and JEDEC memory settings if any overclock, undervolt, XMP, or enhanced turbo setting is active.", "The stop family involves hypervisor/secure-kernel/processor execution where firmware or marginal stability is plausible, but not proven.", [$"Bugcheck 0x{code:X}.", machine.Bios, machine.Memory], RecommendationUrgency.Soon, RecommendationRisk.Medium, true, "A reversible baseline test distinguishes configuration instability from a persistent software fault.", RecommendationPurpose.IsolationTest));
        if (code == 0x124)
            recommendations.Add(new("Inspect the parsed WHEA error record before testing or replacing hardware; if it names a processor/cache bank, test firmware defaults and controlled memory/CPU diagnostics.", "0x124 confirms an uncorrectable WHEA report but the component must come from !errrec.", ["Bugcheck 0x124", analysis.Analyze.RawSections.GetValueOrDefault("!errrec") ?? "No !errrec record was readable."], RecommendationUrgency.Soon, RecommendationRisk.Low, true, "The WHEA section prevents guessing which hardware path reported the error.", RecommendationPurpose.FurtherCollection));
        var storageErrors = events.Where(entry => entry.Provider.Contains("disk", StringComparison.OrdinalIgnoreCase) || entry.Provider.Contains("stor", StringComparison.OrdinalIgnoreCase) || entry.Provider.Contains("ntfs", StringComparison.OrdinalIgnoreCase)).Where(entry => entry.Level is "1" or "2").ToList();
        if (storageErrors.Any(entry => entry.EventId is 7 or 51 or 129 or 153 or 157))
            recommendations.Add(new("Back up important data from the affected storage device before intensive repair scans, then inspect controller/NVMe SMART and cabling evidence.", "The incident window contains material storage timeout, bad-block, reset, or removal events.", storageErrors.Take(8).Select(entry => $"{entry.Provider}/{entry.EventId} at {entry.Timestamp:O}").ToList(), RecommendationUrgency.Immediate, RecommendationRisk.Low, true, "Protects data first and separates physical/link errors from filesystem symptoms.", RecommendationPurpose.DataProtection));
        var smartConcerns = storageDevices?.SelectMany(device => device.Warnings.Select(warning => $"{device.Device}: {warning}")).ToList() ?? [];
        if (smartConcerns.Count > 0)
            recommendations.Add(new("Back up important files from storage devices with health or SMART warnings before running intensive diagnostics.", "Windows reliability data or vendor SMART counters contain a warning. These values justify data protection, but they do not by themselves attribute this bugcheck to storage.", smartConcerns.Take(10).ToList(), RecommendationUrgency.Immediate, RecommendationRisk.Low, true, "Protects data while vendor documentation and device-specific diagnostics establish whether the warning is material.", RecommendationPurpose.DataProtection));
        if (recommendations.Count == 0)
            recommendations.Add(new("Preserve the ZIP and compare the next incident before changing drivers or hardware.", "The current dump does not identify a sufficiently strong corrective target.", [analysis.Quality.Explanation, analysis.Assessment.Explanation], RecommendationUrgency.Routine, RecommendationRisk.Low, true, "A repeated fingerprint can strengthen or weaken candidate patterns without speculative changes.", RecommendationPurpose.FurtherCollection));
        return recommendations;
    }
}

public static class EventTimelineEngine
{
    public static IReadOnlyList<CorrelatedTimelineEntry> Build(Incident incident, IReadOnlyList<EvidenceEvent> events, CrashTimestampEvidence? timestampEvidence = null)
    {
        var crashTime = timestampEvidence?.SelectedCrashTime ?? incident.Timestamp;
        var bootTime = timestampEvidence?.NextBootTime ?? incident.RebootTime
            ?? events.Where(entry => IsBoot(entry) && entry.Timestamp > crashTime).OrderBy(entry => entry.Timestamp).FirstOrDefault()?.Timestamp;
        var result = new List<CorrelatedTimelineEntry>();
        result.Add(new(crashTime, TimeSpan.Zero, TimelinePhase.Crash, "Crash-time correlation",
            "Crash time anchor",
            timestampEvidence?.Explanation ?? $"Selected incident time from {incident.TimestampBasis}.",
            EvidenceOrigin.DirectObservation,
            "Crash T±0"));
        foreach (var entry in events.OrderBy(entry => entry.Timestamp))
        {
            var phase = Classify(entry, crashTime, bootTime);
            var title = $"{entry.Provider} event {entry.EventId}";
            var explanation = CodeDecoder.DescribeEvent(entry);
            if (phase == TimelinePhase.AfterReboot && (entry.Provider.Contains("Service Control Manager", StringComparison.OrdinalIgnoreCase) || entry.Provider.Contains("Application", StringComparison.OrdinalIgnoreCase))) explanation += " This post-reboot event is not presented as a cause of the preceding crash.";
            if (IsPostBootCrashRecord(entry, bootTime)) explanation += " This is a post-boot record about the earlier crash; its event time is not the crash time.";
            result.Add(new(entry.Timestamp, entry.Timestamp - crashTime, phase, $"{entry.LogName}:{entry.Provider}/{entry.EventId}", title, explanation,
                phase is TimelinePhase.Crash or TimelinePhase.DumpCreation or TimelinePhase.Reboot ? EvidenceOrigin.DirectObservation : EvidenceOrigin.PossibleCorrelation,
                RelativeLabel(entry.Timestamp, crashTime, bootTime)));
        }
        return result.OrderBy(entry => entry.Timestamp).ThenBy(entry => entry.Title).ToList();
    }

    private static TimelinePhase Classify(EvidenceEvent entry, DateTimeOffset crashTime, DateTimeOffset? boot)
    {
        if (IsBoot(entry)) return TimelinePhase.Reboot;
        if (IsPostBootCrashRecord(entry, boot)) return entry.Provider.Contains("WER", StringComparison.OrdinalIgnoreCase) ? TimelinePhase.DumpCreation : TimelinePhase.AfterReboot;
        if (entry.Provider.Equals("Microsoft-Windows-WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001) return TimelinePhase.Crash;
        if ((entry.Provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) && entry.EventId == 41)
            || (entry.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && entry.EventId == 6008)) return TimelinePhase.Crash;
        if (entry.Provider.Equals("volmgr", StringComparison.OrdinalIgnoreCase) && entry.EventId is 161 or 162) return TimelinePhase.DumpCreation;
        if (entry.Provider.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001 && entry.Message.Contains("BlueScreen", StringComparison.OrdinalIgnoreCase)) return TimelinePhase.DumpCreation;
        if (boot is not null) return entry.Timestamp < boot ? TimelinePhase.BeforeCrash : TimelinePhase.AfterReboot;
        return entry.Timestamp < crashTime ? TimelinePhase.BeforeCrash : TimelinePhase.AfterReboot;
    }

    private static bool IsPostBootCrashRecord(EvidenceEvent entry, DateTimeOffset? boot)
        => boot is not null && entry.Timestamp >= boot
           && ((entry.Provider.Equals("Microsoft-Windows-WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001)
               || (entry.Provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) && entry.EventId == 41)
               || (entry.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && entry.EventId == 6008));

    private static string RelativeLabel(DateTimeOffset value, DateTimeOffset crash, DateTimeOffset? boot)
    {
        var anchor = boot is not null && value >= boot ? "Boot" : "Crash";
        var offset = value - (anchor == "Boot" ? boot!.Value : crash);
        if (offset.Duration() < TimeSpan.FromMilliseconds(500)) return $"{anchor} T±0";
        return offset < TimeSpan.Zero
            ? $"{anchor} T−{Math.Abs(offset.TotalSeconds):0}s"
            : $"{anchor} T+{offset.TotalSeconds:0}s";
    }
    private static bool IsBoot(EvidenceEvent entry) => (entry.Provider.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase) && entry.EventId == 12) || (entry.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && entry.EventId == 6005);
}

public static class CrossIncidentEngine
{
    public static IncidentFingerprint CreateFingerprint(string incidentId, DumpAnalysisResult analysis)
    {
        var thirdParty = analysis.StackFrames.Where(frame => frame.ModuleCategory is not (ModuleCategory.MicrosoftKernelCore or ModuleCategory.MicrosoftInboxDriver or ModuleCategory.UnknownUnverifiable)).Select(frame => frame.Module ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        var top = analysis.StackFrames.Take(8).Select(frame => $"{frame.Module}!{frame.Symbol}").ToList();
        var fields = new[] { analysis.Analyze.BugCheckCode, analysis.Analyze.FailureBucketId, analysis.Analyze.FaultingIp, analysis.Analyze.ModuleName, analysis.Analyze.SymbolName, analysis.Analyze.ProcessName, analysis.Analyze.ExceptionCode, string.Join("|", top), string.Join("|", thirdParty) };
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", fields))));
        return new(incidentId, analysis.Analyze.BugCheckCode ?? "Unknown", analysis.Analyze.BugCheckString, analysis.Analyze.FailureBucketId, analysis.Analyze.FaultingIp, analysis.Analyze.ModuleName ?? analysis.Analyze.ImageName, analysis.Analyze.SymbolName, analysis.Analyze.ProcessName, analysis.Analyze.ExceptionCode, top, thirdParty, hash);
    }

    public static IReadOnlyList<ComparisonFinding> Compare(IncidentFingerprint current, IReadOnlyList<IncidentFingerprint> history)
    {
        var findings = new List<ComparisonFinding>();
        var sameCodeModule = history.Where(item => item.BugCheckCode.Equals(current.BugCheckCode, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(current.Module) && item.Module?.Equals(current.Module, StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (sameCodeModule.Count > 0) findings.Add(new("Repeated bugcheck and module", $"The same {current.BugCheckCode} bugcheck and module {current.Module} appear in {sameCodeModule.Count + 1} incidents. Repetition strengthens this module as an investigative lead but does not by itself prove causation.", EvidenceOrigin.PossibleCorrelation, ConfidenceLevel.Moderate, sameCodeModule.Select(item => item.IncidentId).Append(current.IncidentId).ToList()));
        foreach (var driver in current.ThirdPartyFrames)
        {
            var matches = history.Where(item => item.ThirdPartyFrames.Contains(driver, StringComparer.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0) findings.Add(new("Repeated third-party stack participant", $"{driver} appears on the parsed stack in {matches.Count + 1} incidents across bugcheck evidence. Its exact stack relationship determines whether this is moderate or weak evidence.", EvidenceOrigin.PossibleCorrelation, ConfidenceLevel.Moderate, matches.Select(item => item.IncidentId).Append(current.IncidentId).ToList()));
        }
        var varied = history.Where(item => !item.BugCheckCode.Equals(current.BugCheckCode, StringComparison.OrdinalIgnoreCase) && item.TopFrames.Count > 0).ToList();
        if (varied.Count >= 2 && history.Append(current).Count(item => item.ExceptionCode is "0xC0000005" or "0xC000001D") >= 2) findings.Add(new("Varied failures with memory-corruption indicators", "Different bugchecks or modules contain repeated access/illegal-instruction evidence. This pattern can be consistent with systemic instability or earlier corruption, but does not prove faulty RAM.", EvidenceOrigin.PossibleCorrelation, ConfidenceLevel.Low, varied.Select(item => item.IncidentId).Append(current.IncidentId).ToList()));
        if (!string.IsNullOrWhiteSpace(current.Process))
        {
            var matches = history.Where(item => item.Process?.Equals(current.Process, StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (matches.Count > 0) findings.Add(new("Repeated process context", $"{current.Process} was active in {matches.Count + 1} incidents. Process context is weak evidence because it may only be where Windows detected earlier kernel corruption.", EvidenceOrigin.PossibleCorrelation, ConfidenceLevel.Low, matches.Select(item => item.IncidentId).Append(current.IncidentId).ToList()));
        }
        return findings;
    }

    public static async Task<IReadOnlyList<IncidentFingerprint>> LoadHistoryAsync(string outputRoot, string excludeDirectory, CancellationToken token)
    {
        var result = new List<IncidentFingerprint>(); if (!Directory.Exists(outputRoot)) return result;
        foreach (var path in Directory.EnumerateFiles(outputRoot, "report.json", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(path => !path.StartsWith(excludeDirectory, StringComparison.OrdinalIgnoreCase)).Take(100))
        {
            try { var report = JsonSerializer.Deserialize<EvidenceReport>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false), JsonDefaults.Indented); result.AddRange(report?.DumpAnalyses.Select(item => item.Fingerprint).OfType<IncidentFingerprint>() ?? []); } catch (Exception) { }
        }
        return result;
    }
}
