using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

/// <summary>
/// Extracts family-specific facts only from parsed parameters and deterministic
/// debugger command sections. It does not invent missing IRP, WHEA, TDR, stack,
/// storage, or hypervisor details.
/// </summary>
public static partial class BugCheckFamilyAnalyzer
{
    public static BugCheckFamilyAnalysis Analyze(DumpAnalysisResult analysis)
    {
        var result = new BugCheckFamilyAnalysis();
        if (!NumericParser.TryParse(analysis.Analyze.BugCheckCode, out var code))
        {
            result.Limitations.Add("The bugcheck code was unavailable or not numeric, so no family-specific decoder was selected.");
            return result;
        }

        switch (code)
        {
            case 0x9F:
                AnalyzePower(analysis, result);
                break;
            case 0x124:
                AnalyzeWhea(analysis, result);
                break;
            case 0x116:
                AnalyzeGraphics(analysis, result);
                break;
            case 0x1AA:
                AnalyzeInvalidStack(analysis, result);
                break;
            case 0x154:
                AnalyzeStorage(analysis, result);
                break;
            case 0x20001:
            case 0x18B:
                AnalyzeVirtualization(analysis, result);
                break;
            default:
                result.Family = BugCheckKnowledge.Find(code)?.Name ?? $"Bugcheck 0x{code:X}";
                result.Limitations.Add("No optional family analyzer applies; the generic structured fields remain available.");
                break;
        }
        return result;
    }

    private static void AnalyzePower(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result)
    {
        result.Family = "Power transition / blocked IRP";
        var subtype = analysis.DecodedParameters.FirstOrDefault(item => item.Index == 1);
        result.Facts.Add($"Power violation subtype: {subtype?.RawValue ?? "unavailable"}{(subtype?.SymbolicValue is null ? string.Empty : $" ({subtype.SymbolicValue})")}.");
        AddSection(analysis, result, "!poaction"); AddSection(analysis, result, "!irp"); AddSection(analysis, result, "!devstack");
        var combined = Sections(analysis, "!irp", "!devstack", "!poaction");
        foreach (Match match in DriverObjectRegex().Matches(combined))
        {
            var driver = match.Groups[1].Value;
            result.CandidateEvidence.Add(new(driver, CandidateKind.Driver, "The power/IRP debugger output associates this driver with the blocked request or device stack.", 35, EvidenceOrigin.DirectObservation, match.Value.Trim()));
        }
        if (combined.Length == 0) result.Limitations.Add("No readable !irp, !devstack, or !poaction section was available; a loaded power driver cannot be named as the blocker.");
    }

    private static void AnalyzeWhea(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result)
    {
        result.Family = "WHEA uncorrectable hardware report";
        AddSection(analysis, result, "!errrec");
        var errrec = Section(analysis, "!errrec");
        if (errrec.Length == 0) { result.Limitations.Add("The WHEA error record was not readable; the stop code alone cannot identify a CPU, memory, PCIe, or interconnect component."); return; }
        foreach (var line in errrec.Split('\n').Where(line => line.Contains("Error Type", StringComparison.OrdinalIgnoreCase) || line.Contains("Error Source", StringComparison.OrdinalIgnoreCase) || line.Contains("Bank", StringComparison.OrdinalIgnoreCase) || line.Contains("Status", StringComparison.OrdinalIgnoreCase) || line.Contains("Section", StringComparison.OrdinalIgnoreCase)).Take(20))
            result.Facts.Add(line.Trim());
        var entity = errrec.Contains("PCI Express", StringComparison.OrdinalIgnoreCase) || errrec.Contains("PCIe", StringComparison.OrdinalIgnoreCase) ? "PCI Express error source"
            : errrec.Contains("Cache", StringComparison.OrdinalIgnoreCase) ? "Processor cache error source"
            : errrec.Contains("Memory", StringComparison.OrdinalIgnoreCase) ? "Memory error source"
            : "WHEA-reported hardware path";
        result.CandidateEvidence.Add(new(entity, CandidateKind.Hardware, "The parsed !errrec section identifies this error-source class. It does not by itself prove a replaceable component is defective.", 25, EvidenceOrigin.DebuggerConclusion, "!errrec"));
    }

    private static void AnalyzeGraphics(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result)
    {
        result.Family = "Graphics timeout detection and recovery";
        var status = analysis.DecodedParameters.FirstOrDefault(item => item.Index == 3);
        if (status is not null) result.Facts.Add($"Last graphics operation status: {status.RawValue}{(status.SymbolicValue is null ? string.Empty : $" ({status.SymbolicValue})")}.");
        var graphics = analysis.StackFrames.FirstOrDefault(frame => frame.ModuleCategory == ModuleCategory.GraphicsDriver);
        if (graphics?.Module is not null)
            result.CandidateEvidence.Add(new(graphics.Module, CandidateKind.Driver, $"The non-Microsoft display driver appears at stack frame {graphics.Index} in the failed TDR path.", graphics.Relationship == StackRelationship.ExecutingAtFault ? 45 : 20, EvidenceOrigin.DirectObservation, graphics.RawLine));
        else result.Limitations.Add("No non-Microsoft display miniport was resolved on the parsed stack.");
        result.RawReferences.AddRange(analysis.Dump.DumpType is DumpType.GraphicsLiveDump or DumpType.WatchdogLiveDump ? ["Graphics/watchdog live dump"] : []);
    }

    private static void AnalyzeInvalidStack(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result)
    {
        result.Family = "Exception dispatch on an invalid kernel stack";
        var type = analysis.DecodedParameters.FirstOrDefault(item => item.Index == 2);
        result.Facts.Add($"Expected stack category: {type?.SymbolicValue ?? type?.RawValue ?? "unavailable"}.");
        result.Facts.Add($"Context record: {analysis.Analyze.ContextRecord ?? "unavailable"}; exception record: {analysis.Analyze.ExceptionRecord ?? "unavailable"}.");
        if (!string.IsNullOrWhiteSpace(analysis.Analyze.ExceptionCode)) result.Facts.Add($"Exception: {analysis.Analyze.ExceptionCode}.");
        if (!string.IsNullOrWhiteSpace(analysis.Analyze.ExceptionAddress)) result.Facts.Add($"Original exception instruction: {analysis.Analyze.ExceptionAddress}.");
        if (analysis.Analyze.RawSections.TryGetValue("r", out var registers))
        {
            result.RawReferences.Add("r (registers after supplied context)");
            var rsp = RegisterRspRegex().Match(registers);
            if (rsp.Success)
            {
                var originalRsp = "0x" + rsp.Groups[1].Value.Replace("`", string.Empty);
                var bugcheckSp = analysis.DecodedParameters.FirstOrDefault(item => item.Index == 1)?.RawValue ?? "unavailable";
                result.Facts.Add($"Reconstructed RSP: {originalRsp}; bugcheck stack pointer: {bugcheckSp}.");
            }
        }
        var coherent = analysis.StackFrames.TakeWhile(frame => frame.Resolved).Take(6).Select(frame => $"{frame.Module}!{frame.Symbol}").ToList();
        if (coherent.Count > 0) result.Facts.Add("Last parsed coherent frames: " + string.Join(" <- ", coherent));
        else result.Limitations.Add("The stack did not contain a coherent resolved prefix.");
        var thirdParty = analysis.StackFrames.Where(frame => frame.ModuleCategory is not (ModuleCategory.MicrosoftKernelCore or ModuleCategory.MicrosoftInboxDriver or ModuleCategory.UnknownUnverifiable)).Select(frame => frame.Module).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.Facts.Add(thirdParty.Count == 0 ? "No resolved third-party module appeared in the reconstructed stack." : "Third-party modules in the reconstructed stack: " + string.Join(", ", thirdParty));
        if (analysis.Dump.DumpType is DumpType.SmallMemory or DumpType.WerMinidump && analysis.Analyze.Warnings.Any(warning => warning.Contains("page", StringComparison.OrdinalIgnoreCase) || warning.Contains("memory", StringComparison.OrdinalIgnoreCase)))
            result.Limitations.Add("The small dump lacks one or more pages required to reconstruct the origin reliably.");
        result.Limitations.Add("An invalid stack supports illegal stack use or corruption, but does not prove defective RAM or implicate the active process.");
    }

    private static void AnalyzeStorage(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result)
    {
        result.Family = "Kernel store / paging / storage path";
        foreach (var command in new[] { "!blackboxntfs", "!blackboxstorage", "!irp", "!devstack" }) AddSection(analysis, result, command);
        var drivers = analysis.StackFrames.Where(frame => frame.ModuleCategory is ModuleCategory.StorageDriver or ModuleCategory.FileSystemDriver or ModuleCategory.FileSystemMinifilter).Select(frame => frame.Module).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var driver in drivers) result.CandidateEvidence.Add(new(driver, CandidateKind.Driver, "This storage/filesystem component participated in the parsed stack; event and IRP evidence are required before attribution.", 10, EvidenceOrigin.DirectObservation, "Parsed stack"));
        result.Limitations.Add("UNEXPECTED_STORE_EXCEPTION does not by itself prove a physical disk failure.");
    }

    private static void AnalyzeVirtualization(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result)
    {
        result.Family = "Hypervisor / secure-kernel fatal condition";
        result.Facts.Add($"Debugger bugcheck: {analysis.Analyze.BugCheckCode} {analysis.Analyze.BugCheckString}.");
        var modules = analysis.StackFrames.Where(frame => frame.ModuleCategory == ModuleCategory.VirtualizationHypervisor).Select(frame => frame.Module).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (modules.Count > 0) result.Facts.Add("Virtualization stack participants: " + string.Join(", ", modules));
        result.CandidateEvidence.Add(new("Windows hypervisor or secure-kernel execution path", CandidateKind.WindowsSubsystem, "The bugcheck records a fatal virtualization/secure-kernel condition; firmware or platform instability remains an alternative unless the dump identifies an exact origin.", 20, EvidenceOrigin.DirectObservation, $"Bugcheck {analysis.Analyze.BugCheckCode}"));
        result.Limitations.Add("Normal Hyper-V startup events are not evidence of a hypervisor defect.");
    }

    private static void AddSection(DumpAnalysisResult analysis, BugCheckFamilyAnalysis result, string command)
    {
        if (Section(analysis, command).Length > 0) result.RawReferences.Add(command);
    }

    private static string Sections(DumpAnalysisResult analysis, params string[] commands) => string.Join("\n", commands.Select(command => Section(analysis, command)).Where(value => value.Length > 0));
    private static string Section(DumpAnalysisResult analysis, string command)
    {
        if (analysis.Analyze.RawSections.TryGetValue(command, out var exact)) return exact;
        return analysis.Analyze.RawSections.FirstOrDefault(pair => pair.Key.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
    }

    [GeneratedRegex(@"\\Driver\\([A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DriverObjectRegex();
    [GeneratedRegex(@"\brsp=([0-9a-fA-F`]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RegisterRspRegex();
}
