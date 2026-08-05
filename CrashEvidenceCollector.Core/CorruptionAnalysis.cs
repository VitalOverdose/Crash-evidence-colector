using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

/// <summary>
/// Plain-English meaning for the debugger's own failure-bucket identifiers. The
/// bucket is a debugger conclusion, so these describe what the debugger observed;
/// they never name a component.
/// </summary>
public static partial class FailureBucketKnowledge
{
    private static readonly (string Token, string Meaning)[] Tokens =
    [
        ("ZEROED_STACK", "The kernel stack contained zeros where call frames were expected, so the debugger could not reconstruct who was executing. A live kernel stack is never zeroed; this indicates the stack contents were destroyed, and it is why no module can be attributed from this dump."),
        ("CORRUPT", "The debugger classified this failure as corruption of a kernel structure."),
        ("INVALID_POINTER_READ", "A read used a pointer the debugger classified as invalid."),
        ("INVALID_POINTER_WRITE", "A write used a pointer the debugger classified as invalid."),
        ("_AV", "The failure was an access violation: code read, wrote, or executed an address it could not."),
        ("NULL_", "The failure involved a null or near-null address, which usually means an uninitialised or already-cleared pointer."),
        ("_ANALYSIS_INCONCLUSIVE", "The debugger could not attribute the failure from the evidence in this dump."),
        ("DOUBLE_FAULT", "A fault occurred while handling another fault, commonly after stack exhaustion or corruption."),
        ("STACKIMMUNE", "The debugger considered the stack unreliable for attribution."),
        ("TRAP_FRAME", "Attribution relied on a trap frame the debugger could not fully validate.")
    ];

    public static string? Describe(string? failureBucket)
    {
        if (string.IsNullOrWhiteSpace(failureBucket)) return null;
        var matched = Tokens.Where(item => failureBucket.Contains(item.Token, StringComparison.OrdinalIgnoreCase)).Select(item => item.Meaning).Distinct().ToList();
        return matched.Count == 0 ? null : string.Join(" ", matched);
    }

    /// <summary>True when the bucket itself shows the evidence needed for attribution was destroyed.</summary>
    public static bool IndicatesDestroyedEvidence(string? failureBucket)
        => !string.IsNullOrWhiteSpace(failureBucket)
           && (failureBucket.Contains("ZEROED_STACK", StringComparison.OrdinalIgnoreCase) || failureBucket.Contains("STACKIMMUNE", StringComparison.OrdinalIgnoreCase));

    /// <summary>Placeholders the debugger emits when it has nothing to name.</summary>
    public static bool IsPlaceholderModule(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && (value.Equals("Unknown_Module", StringComparison.OrdinalIgnoreCase)
               || value.Equals("ANALYSIS_INCONCLUSIVE", StringComparison.OrdinalIgnoreCase)
               || value.Equals("Unknown_Image", StringComparison.OrdinalIgnoreCase)
               || value.Equals("memory_corruption", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Detects unrelated user-mode processes failing with the same memory-access
/// exception shortly before a kernel fault. One application crash is ordinary;
/// several unrelated ones converging on a bugcheck is a cross-layer pattern that
/// a single dump cannot show, so it is reported as correlation with its evidence.
/// </summary>
public static partial class CrossLayerCorruptionAnalyzer
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);
    private const int MinimumDistinctProcesses = 2;

    public static IReadOnlyList<Observation> Analyze(IReadOnlyList<EvidenceEvent> events, DateTimeOffset crashTime, string? kernelExceptionCode)
    {
        var faults = events
            .Where(entry => entry.Timestamp <= crashTime && crashTime - entry.Timestamp <= Window)
            .Where(IsAccessViolationFault)
            .Select(entry => (Process: ExtractProcessName(entry) ?? "unnamed process", entry.Timestamp))
            .Where(item => !string.IsNullOrWhiteSpace(item.Process))
            .ToList();
        if (faults.Count == 0) return [];

        var distinct = faults.GroupBy(item => item.Process, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Process: group.Key, Count: group.Count(), First: group.Min(item => item.Timestamp), Last: group.Max(item => item.Timestamp)))
            .OrderBy(item => item.First)
            .ToList();
        if (distinct.Count < MinimumDistinctProcesses) return [];

        var kernelAlsoAccessViolation = !string.IsNullOrWhiteSpace(kernelExceptionCode) && NumericParser.TryParse(kernelExceptionCode, out var code) && (uint)code == 0xC0000005;
        var listed = string.Join(", ", distinct.Select(item => $"{item.Process} (×{item.Count})"));
        var detail =
            $"{distinct.Count} unrelated user-mode processes failed with access violations (0xC0000005) in the {Window.TotalMinutes:0} minutes before the kernel fault: {listed}. "
            + (kernelAlsoAccessViolation ? "The kernel failure was also an access violation. " : string.Empty)
            + "Independent processes faulting on memory access in the same period, followed by a kernel-mode fault, is a pattern consistent with memory contents not being held reliably — for example failing RAM, an unstable processor or cache, or a driver corrupting shared memory. "
            + "It is correlation across layers, not proof of any one cause: a single common component (an injected shell extension, a security product's user-mode hooks, or a shared runtime) can also produce it, so the faulting modules of each application report should be compared before concluding.";
        return [new("Warning", "Multiple unrelated processes failed with access violations before the crash", detail)];
    }

    public static bool IsAccessViolationFault(EvidenceEvent entry)
    {
        var isFaultRecord = (entry.Provider.Equals("Application Error", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1000)
                            || (entry.Provider.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001 && Mentions(entry, "APPCRASH"));
        return isFaultRecord && Mentions(entry, "c0000005");
    }

    public static string? ExtractProcessName(EvidenceEvent entry)
    {
        foreach (var key in new[] { "AppName", "ApplicationName", "FaultingApplicationName", "P1", "Data0" })
            if (entry.Data?.TryGetValue(key, out var value) == true && LooksLikeProcess(value)) return value.Trim();
        var fromData = entry.Data?.Values.FirstOrDefault(LooksLikeProcess);
        if (fromData is not null) return fromData.Trim();
        var match = ProcessNameRegex().Match(entry.Message);
        return match.Success ? match.Value : null;
    }

    private static bool LooksLikeProcess(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static bool Mentions(EvidenceEvent entry, string token)
        => entry.Message.Contains(token, StringComparison.OrdinalIgnoreCase)
           || entry.Data?.Values.Any(value => value?.Contains(token, StringComparison.OrdinalIgnoreCase) == true) == true;

    [GeneratedRegex(@"[\w.\-]+\.exe", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)] private static partial Regex ProcessNameRegex();
}
