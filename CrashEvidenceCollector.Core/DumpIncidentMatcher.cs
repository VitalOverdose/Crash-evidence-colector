namespace CrashEvidenceCollector.Core;

/// <summary>
/// Correlates analysed dumps to the incident the user actually selected. An
/// explicit bugcheck mismatch is a veto: analysis quality must never allow a
/// coherent but unrelated dump to replace the selected incident's evidence.
/// </summary>
public static class DumpIncidentMatcher
{
    private static readonly TimeSpan MaximumExactMatchDistance = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaximumTimestampOnlyDistance = TimeSpan.FromMinutes(15);

    public static DumpAnalysisResult? AssignPrimary(Incident incident, IReadOnlyList<DumpAnalysisResult> analyses)
    {
        var selectedHasCode = NumericParser.TryParse(incident.BugCheckCode, out var selectedCode) && selectedCode != 0;
        foreach (var analysis in analyses)
        {
            // Prefer the debugger's dump-header session time for proximity. The
            // filesystem timestamp is only a fallback and is never described as
            // the crash time: Windows can finish/register a dump after reboot.
            var proximityTime = analysis.Dump.DumpHeaderTime ?? analysis.Dump.ModificationTime;
            var proximitySource = analysis.Dump.DumpHeaderTime is not null ? "dump-header time" : "dump source final-modified time";
            var delta = (proximityTime - incident.Timestamp).Duration();
            ulong dumpCode = 0;
            var dumpHasCode = !analysis.Analyze.BugCheckCodeFromSelectedIncidentFallback
                && NumericParser.TryParse(analysis.Analyze.BugCheckCode, out dumpCode)
                && dumpCode != 0;
            var usable = analysis.Dump.Completion is DebuggerCompletion.Completed or DebuggerCompletion.Partial;
            var timedOutWithCorrelationEvidence = analysis.Dump.Completion == DebuggerCompletion.TimedOut
                && delta <= MaximumTimestampOnlyDistance
                && IsPlausibleAroundReboot(analysis.Dump, incident);

            // A debugger-observed code mismatch is always a veto, including when
            // later commands timed out. Rich analysis must never select a wrong dump.
            if (selectedHasCode && dumpHasCode && selectedCode != dumpCode)
            {
                analysis.Association = new(DumpAssociationStatus.RejectedBugCheckMismatch, false, -10000, delta, $"Rejected for the selected incident: incident code 0x{selectedCode:X} does not match dump code 0x{dumpCode:X}. The analysis is preserved as unrelated evidence.");
            }
            else if ((usable || analysis.Dump.Completion == DebuggerCompletion.TimedOut) && selectedHasCode && dumpHasCode && selectedCode == dumpCode && delta <= MaximumExactMatchDistance)
            {
                var completionPenalty = analysis.Dump.Completion == DebuggerCompletion.TimedOut ? 500 : 0;
                var score = 10000 - (int)Math.Min(delta.TotalSeconds, 1800) + analysis.Quality.Score - completionPenalty;
                var completionNote = analysis.Dump.Completion == DebuggerCompletion.TimedOut
                    ? " CDB timed out after exposing the matching code; partial evidence is retained and analysis limitations remain explicit."
                    : string.Empty;
                analysis.Association = new(DumpAssociationStatus.ExactBugCheckMatch, false, score, delta, $"Debugger-observed bugcheck 0x{selectedCode:X} matches and the {proximitySource} is {FormatDelta(delta)} from the selected crash time. This file/header time is supporting association evidence, not the definition of the crash time.{completionNote}");
            }
            else if ((usable || analysis.Dump.Completion == DebuggerCompletion.TimedOut) && selectedHasCode && dumpHasCode)
            {
                analysis.Association = new(DumpAssociationStatus.RejectedTooDistant, false, -5000, delta, $"The bugcheck code matches, but the {proximitySource} is {FormatDelta(delta)} from the selected crash time, beyond the 30-minute safety window.");
            }
            else if (usable && delta <= MaximumTimestampOnlyDistance)
            {
                var score = 1000 - (int)Math.Min(delta.TotalSeconds, 900) + analysis.Quality.Score;
                analysis.Association = new(DumpAssociationStatus.TimestampOnlyMatch, false, score, delta, $"No comparable bugcheck code was available; the dump is a {proximitySource}-only candidate {FormatDelta(delta)} from the selected crash time. This does not establish the crash time.");
            }
            else if (timedOutWithCorrelationEvidence)
            {
                var score = 500 - (int)Math.Min(delta.TotalSeconds, 900);
                analysis.Association = new(DumpAssociationStatus.TimestampOnlyMatch, false, score, delta,
                    $"CDB timed out before exposing a debugger-confirmed bugcheck code, but this dump's {proximitySource} is {FormatDelta(delta)} from the selected incident and is consistent with the reboot boundary. Associated provisionally by timestamp; the incident code comes from Windows event evidence, not from this dump analysis.");
            }
            else if (!usable)
            {
                analysis.Association = new(DumpAssociationStatus.InsufficientEvidence, false, -2000, delta, $"Debugger completion was {analysis.Dump.Completion}, and the available timestamp/reboot evidence was not strong enough to associate this dump safely.");
            }
            else
            {
                analysis.Association = new(DumpAssociationStatus.InsufficientEvidence, false, -1000, delta, $"No matching bugcheck code was available and the {proximitySource} is {FormatDelta(delta)} from the selected crash time.");
            }
        }

        var primary = analyses
            .Where(analysis => analysis.Association.Status is DumpAssociationStatus.ExactBugCheckMatch or DumpAssociationStatus.TimestampOnlyMatch)
            .OrderByDescending(analysis => analysis.Association.Status == DumpAssociationStatus.ExactBugCheckMatch)
            .ThenByDescending(analysis => analysis.Association.Score)
            .FirstOrDefault();
        if (primary is not null) primary.Association = primary.Association with { IsPrimaryForIncident = true, Explanation = primary.Association.Explanation + " Selected as the primary dump." };
        return primary;
    }

    private static bool IsPlausibleAroundReboot(DumpEvidence dump, Incident incident)
    {
        if (incident.RebootTime is null) return true;
        var evidenceTime = dump.DumpHeaderTime ?? dump.ModificationTime;
        // Dump headers describe the pre-reboot session. Files can be finalized
        // shortly after startup, so allow a small post-boot write margin only
        // when no header time was available.
        var margin = dump.DumpHeaderTime is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1);
        return evidenceTime <= incident.RebootTime.Value + margin;
    }

    private static string FormatDelta(TimeSpan delta) => delta.TotalSeconds < 90 ? $"{delta.TotalSeconds:0} seconds" : $"{delta.TotalMinutes:0.0} minutes";
}
