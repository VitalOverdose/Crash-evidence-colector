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
            var dumpHasCode = NumericParser.TryParse(analysis.Analyze.BugCheckCode, out var dumpCode) && dumpCode != 0;
            var usable = analysis.Dump.Completion is DebuggerCompletion.Completed or DebuggerCompletion.Partial;

            if (!usable)
            {
                analysis.Association = new(DumpAssociationStatus.InsufficientEvidence, false, -2000, delta, $"Debugger completion was {analysis.Dump.Completion}; this dump cannot drive the incident headline.");
            }
            else if (selectedHasCode && dumpHasCode && selectedCode != dumpCode)
            {
                analysis.Association = new(DumpAssociationStatus.RejectedBugCheckMismatch, false, -10000, delta, $"Rejected for the selected incident: incident code 0x{selectedCode:X} does not match dump code 0x{dumpCode:X}. The analysis is preserved as unrelated evidence.");
            }
            else if (selectedHasCode && dumpHasCode && selectedCode == dumpCode && delta <= MaximumExactMatchDistance)
            {
                var score = 10000 - (int)Math.Min(delta.TotalSeconds, 1800) + analysis.Quality.Score;
                analysis.Association = new(DumpAssociationStatus.ExactBugCheckMatch, false, score, delta, $"Bugcheck 0x{selectedCode:X} matches and the {proximitySource} is {FormatDelta(delta)} from the selected crash time. This file/header time is supporting association evidence, not the definition of the crash time.");
            }
            else if (selectedHasCode && dumpHasCode)
            {
                analysis.Association = new(DumpAssociationStatus.RejectedTooDistant, false, -5000, delta, $"The bugcheck code matches, but the {proximitySource} is {FormatDelta(delta)} from the selected crash time, beyond the 30-minute safety window.");
            }
            else if (delta <= MaximumTimestampOnlyDistance)
            {
                var score = 1000 - (int)Math.Min(delta.TotalSeconds, 900) + analysis.Quality.Score;
                analysis.Association = new(DumpAssociationStatus.TimestampOnlyMatch, false, score, delta, $"No comparable bugcheck code was available; the dump is a {proximitySource}-only candidate {FormatDelta(delta)} from the selected crash time. This does not establish the crash time.");
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

    private static string FormatDelta(TimeSpan delta) => delta.TotalSeconds < 90 ? $"{delta.TotalSeconds:0} seconds" : $"{delta.TotalMinutes:0.0} minutes";
}
