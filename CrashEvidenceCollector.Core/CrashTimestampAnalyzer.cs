namespace CrashEvidenceCollector.Core;

/// <summary>
/// Selects crash time independently from dump association. Post-reboot WER,
/// Kernel-Power, and dump-finalisation times remain evidence but are never
/// silently promoted into the crash anchor.
/// </summary>
public static class CrashTimestampAnalyzer
{
    public static CrashTimestampEvidence Build(Incident incident, IReadOnlyList<EvidenceEvent> events, DumpAnalysisResult? primary)
    {
        var result = new CrashTimestampEvidence
        {
            DumpHeaderTime = primary?.Dump.DumpHeaderTime,
            DumpSourceCreationTime = primary?.Dump.CreationTime,
            DumpSourceModifiedTime = primary?.Dump.ModificationTime,
            CopiedFileCreationTime = primary?.Dump.CopiedFileCreationTime,
            CopiedFileModifiedTime = primary?.Dump.CopiedFileModificationTime,
            PreviousBootTime = incident.BootTime,
            NextBootTime = incident.RebootTime
        };

        result.WerSystemErrorEventTime = events.Where(IsWerBugcheck).Select(item => (DateTimeOffset?)item.Timestamp).OrderBy(item => item).FirstOrDefault();
        result.KernelPowerEventTime = events.Where(item => item.Provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) && item.EventId == 41).Select(item => (DateTimeOffset?)item.Timestamp).OrderBy(item => item).FirstOrDefault();
        var event6008 = events.Where(item => item.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && item.EventId == 6008).OrderBy(item => item.Timestamp).FirstOrDefault();
        result.Event6008RecordedTime = event6008?.Timestamp;
        if (event6008 is not null && IncidentDetector.TryReadPreviousShutdownTime(event6008, out var previousShutdown)) result.Event6008PreviousShutdownTime = previousShutdown;

        if (IsBeforeNextBoot(result.DumpHeaderTime, result.NextBootTime))
        {
            result.SelectedCrashTime = result.DumpHeaderTime;
            result.SelectedCrashTimeSource = "Crash time stored in the dump header";
            result.Confidence = CrashTimeConfidence.ExactDumpHeader;
            result.Explanation = "The debugger exposed a dump-header crash time and it occurs before the confirmed next boot. Dump file creation/modification times were not used as the crash time.";
            return result;
        }

        if (IsBeforeNextBoot(result.Event6008PreviousShutdownTime, result.NextBootTime))
        {
            result.SelectedCrashTime = result.Event6008PreviousShutdownTime;
            result.SelectedCrashTimeSource = "EventLog 6008 previous unexpected-shutdown time";
            result.Confidence = CrashTimeConfidence.WindowsReported;
            result.Explanation = $"Windows reported the previous unexpected shutdown at {result.SelectedCrashTime:O}. Event 6008 was recorded later at {result.Event6008RecordedTime:O}; WER and dump finalisation occurred after reboot and are shown separately.";
            return result;
        }

        // A SystemErrorReporting timestamp is usable only if it precedes the next
        // confirmed boot. On normal systems it is written after boot and therefore
        // describes registration/report processing rather than the bugcheck instant.
        if (IsBeforeNextBoot(result.WerSystemErrorEventTime, result.NextBootTime))
        {
            result.SelectedCrashTime = result.WerSystemErrorEventTime;
            result.SelectedCrashTimeSource = "Pre-boot WER SystemErrorReporting event";
            result.Confidence = CrashTimeConfidence.Low;
            result.Explanation = "WER supplied the only pre-boot bugcheck timestamp. It is retained with low confidence because WER is often written during post-reboot processing.";
            return result;
        }

        if (result.NextBootTime is not null)
        {
            var finalPreBoot = events.Where(item => item.Timestamp < result.NextBootTime && !IsBoot(item)).OrderByDescending(item => item.Timestamp).FirstOrDefault();
            result.EstimatedRangeStart = finalPreBoot?.Timestamp ?? incident.BootTime;
            result.EstimatedRangeEnd = result.NextBootTime;
            result.SelectedCrashTimeSource = "Estimated between final pre-crash evidence and next boot";
            result.Confidence = CrashTimeConfidence.EstimatedRange;
            result.Explanation = result.EstimatedRangeStart is null
                ? $"The exact bugcheck time is unavailable. It occurred before the confirmed next boot at {result.NextBootTime:O}."
                : $"The exact bugcheck time is unavailable. It occurred between the final pre-crash event at {result.EstimatedRangeStart:O} and the confirmed next boot at {result.NextBootTime:O}.";
            return result;
        }

        result.Explanation = "No dump-header time, Event 6008 previous-shutdown value, or confirmed next-boot boundary was available. Post-reboot WER and file timestamps were not used as a crash time.";
        return result;
    }

    private static bool IsBeforeNextBoot(DateTimeOffset? candidate, DateTimeOffset? nextBoot)
        => candidate is not null && (nextBoot is null || candidate.Value < nextBoot.Value);

    private static bool IsWerBugcheck(EvidenceEvent item)
        => item.Provider.Equals("Microsoft-Windows-WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) && item.EventId == 1001;

    private static bool IsBoot(EvidenceEvent item)
        => (item.Provider.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase) && item.EventId == 12)
           || (item.Provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase) && item.EventId == 6005);
}
