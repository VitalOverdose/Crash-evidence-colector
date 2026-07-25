namespace CrashEvidenceCollector.Core;

/// <summary>
/// Keeps the observed failure, the location where Windows detected it, the
/// active process context, and any evidence-backed cause in separate fields.
/// This prevents a symptom such as "invalid stack" or a process name from
/// being accidentally promoted into a claimed culprit.
/// </summary>
public static class IncidentConclusionEngine
{
    public static IncidentConclusion Build(Incident incident, DumpAnalysisResult? primary, CulpritAssessment assessment)
    {
        NumericParser.TryParse(primary?.Analyze.BugCheckCode ?? incident.BugCheckCode, out var code);
        var failureType = code == 0x1AA
            ? "Invalid kernel stack"
            : BugCheckKnowledge.Find(code)?.Name.Replace('_', ' ') ?? incident.Title;
        var detectionLocation = primary?.Analyze.ExceptionSymbol
            ?? primary?.Analyze.ExceptionAddress
            ?? primary?.Analyze.FaultingIp
            ?? primary?.Analyze.SymbolName
            ?? "Unavailable";
        var context = primary?.Analyze.ProcessName ?? "Unavailable";
        var supportedCause = assessment.Confidence is ConfidenceLevel.ConfirmedByDebuggerEvidence or ConfidenceLevel.High or ConfidenceLevel.Moderate
            && !assessment.HeadlineComponent.Equals("Undetermined", StringComparison.OrdinalIgnoreCase)
            && !assessment.HeadlineComponent.Equals("Kernel stack integrity", StringComparison.OrdinalIgnoreCase);
        return new(failureType, detectionLocation, context,
            supportedCause ? assessment.HeadlineComponent : "Undetermined",
            supportedCause ? assessment.Confidence : ConfidenceLevel.InsufficientEvidence);
    }
}
