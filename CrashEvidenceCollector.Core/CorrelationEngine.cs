namespace CrashEvidenceCollector.Core;

public static class CorrelationEngine
{
    public static IReadOnlyList<Observation> Analyze(Incident selected, IReadOnlyList<Incident> incidents, IReadOnlyList<EvidenceEvent> events)
    {
        var observations = new List<Observation>();
        if (selected.BootTime is { } boot && selected.Timestamp - boot < TimeSpan.FromMinutes(15))
            observations.Add(new("Attention", "Crash shortly after boot", "Observation: this incident occurred within 15 minutes of startup."));
        if (selected.Kind is IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown)
            observations.Add(new("Attention", "Shutdown without a recorded bugcheck", "Observation: no correlated bugcheck was found. Power loss, a hard reset, or a freeze are possible explanations; this is not a proven cause."));
        if (!string.IsNullOrWhiteSpace(selected.BugCheckCode))
        {
            var repeats = incidents.Count(x => string.Equals(x.BugCheckCode, selected.BugCheckCode, StringComparison.OrdinalIgnoreCase));
            if (repeats > 1) observations.Add(new("Attention", "Repeated bugcheck code", $"Possible correlation: {selected.BugCheckCode} appears in {repeats} detected incidents."));
        }
        var whea = events.Where(x => x.Provider.Contains("WHEA", StringComparison.OrdinalIgnoreCase) && Math.Abs((x.Timestamp - selected.Timestamp).TotalMinutes) <= 10).ToList();
        if (whea.Count > 0) observations.Add(new("High", "WHEA event near incident", $"Possible correlation: {whea.Count} hardware-error event(s) occurred within 10 minutes. This alone does not prove faulty hardware."));
        foreach (var group in events.Where(x => x.EventId is 1000 or 1001).SelectMany(x => ExtractTokens(x.Message)).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Take(5))
            observations.Add(new("Info", "Repeated process or module", $"Possible correlation: '{group.Key}' appears {group.Count()} times in application failure records."));
        return observations;
    }

    private static IEnumerable<string> ExtractTokens(string message) => message.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => x.Contains(".exe", StringComparison.OrdinalIgnoreCase) || x.Contains(".dll", StringComparison.OrdinalIgnoreCase)).Select(x => x.Length > 160 ? x[..160] : x);
}
