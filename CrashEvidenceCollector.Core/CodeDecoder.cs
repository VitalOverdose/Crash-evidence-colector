using System.Globalization;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace CrashEvidenceCollector.Core;

public static partial class CodeDecoder
{
    private static readonly IReadOnlyDictionary<ulong, (string Name, string Meaning)> BugChecks = new Dictionary<ulong, (string, string)>
    {
        [0x01] = ("APC_INDEX_MISMATCH", "A driver or system call left asynchronous procedure call state unbalanced."),
        [0x0A] = ("IRQL_NOT_LESS_OR_EQUAL", "Kernel-mode code accessed invalid or pageable memory at an elevated interrupt level; a driver or memory corruption is often involved."),
        [0x1A] = ("MEMORY_MANAGEMENT", "Windows detected severe memory-management corruption. Despite the name this does not prove faulty RAM: drivers, storage-backed paging and CPU instability all produce it, and the memory manager is usually the detector rather than the cause."),
        [0x24] = ("NTFS_FILE_SYSTEM", "The NTFS filesystem driver encountered a condition it could not safely handle."),
        [0x3B] = ("SYSTEM_SERVICE_EXCEPTION", "An exception occurred while Windows was executing a system service, commonly involving a driver or corrupted memory."),
        [0x50] = ("PAGE_FAULT_IN_NONPAGED_AREA", "Kernel code referenced memory that should always have been resident but was invalid."),
        [0x7A] = ("KERNEL_DATA_INPAGE_ERROR", "Windows could not read required kernel data from the paging file; storage and cabling errors are important evidence."),
        [0x7E] = ("SYSTEM_THREAD_EXCEPTION_NOT_HANDLED", "A system thread raised an exception that its error handler did not catch."),
        [0x9C] = ("MACHINE_CHECK_EXCEPTION", "The processor reported a machine-check hardware error."),
        [0x9F] = ("DRIVER_POWER_STATE_FAILURE", "A driver failed to complete a power-state transition in time."),
        [0xC2] = ("BAD_POOL_CALLER", "Kernel code made an invalid memory-pool request, usually indicating a driver defect or corruption."),
        [0xD1] = ("DRIVER_IRQL_NOT_LESS_OR_EQUAL", "A driver accessed invalid or pageable memory at an elevated interrupt level."),
        [0xEF] = ("CRITICAL_PROCESS_DIED", "A process required for Windows to operate terminated unexpectedly."),
        [0xF4] = ("CRITICAL_OBJECT_TERMINATION", "A process or thread essential to Windows operation terminated."),
        [0x101] = ("CLOCK_WATCHDOG_TIMEOUT", "A processor core missed an expected clock interrupt; firmware, CPU stability, power, or hardware can be involved."),
        [0x116] = ("VIDEO_TDR_FAILURE", "Windows tried and failed to reset the display driver after the GPU stopped responding."),
        [0x117] = ("VIDEO_TDR_TIMEOUT_DETECTED", "The display stack detected a GPU timeout and attempted recovery."),
        [0x119] = ("VIDEO_SCHEDULER_INTERNAL_ERROR", "The video scheduler detected a fatal consistency error."),
        [0x124] = ("WHEA_UNCORRECTABLE_ERROR", "WHEA reported an uncorrectable hardware error. The WHEA record is more informative than the stop code alone."),
        [0x133] = ("DPC_WATCHDOG_VIOLATION", "A deferred procedure call or interrupt routine exceeded its allowed execution time."),
        [0x139] = ("KERNEL_SECURITY_CHECK_FAILURE", "The kernel detected corruption of a critical data structure."),
        [0x154] = ("UNEXPECTED_STORE_EXCEPTION", "The kernel storage component encountered an unexpected exception; storage health and filesystem events deserve attention."),
        [0x18B] = ("SECURE_KERNEL_ERROR", "The secure kernel encountered a fatal error, potentially involving VBS, firmware, or a low-level driver."),
        [0x20001] = ("HYPERVISOR_ERROR", "The Windows hypervisor encountered a fatal error.")
    };

    private static readonly IReadOnlyDictionary<ulong, (string Name, string Meaning)> StatusCodes = new Dictionary<ulong, (string, string)>
    {
        [0xC0000005] = ("STATUS_ACCESS_VIOLATION", "Code attempted to read, write, or execute an invalid memory address."),
        [0xC000000D] = ("STATUS_INVALID_PARAMETER", "A function received an invalid parameter."),
        [0xC0000017] = ("STATUS_NO_MEMORY", "Windows could not allocate the required virtual memory or paging resources."),
        [0xC000001D] = ("STATUS_ILLEGAL_INSTRUCTION", "The processor encountered an invalid or unsupported instruction."),
        [0xC0000094] = ("STATUS_INTEGER_DIVIDE_BY_ZERO", "An integer calculation attempted division by zero."),
        [0xC00000FD] = ("STATUS_STACK_OVERFLOW", "The process exhausted its thread stack, often because of runaway recursion."),
        [0xC000021A] = ("STATUS_SYSTEM_PROCESS_TERMINATED", "A critical user-mode Windows subsystem terminated."),
        [0xC0000374] = ("STATUS_HEAP_CORRUPTION", "The process heap detected structural corruption."),
        [0xC0000409] = ("STATUS_STACK_BUFFER_OVERRUN / FAIL_FAST", "The process terminated after detecting security-critical corruption or a fail-fast condition."),
        [0x80000003] = ("STATUS_BREAKPOINT", "The process encountered a breakpoint exception."),
        [0x80070005] = ("E_ACCESSDENIED", "Access was denied. This HRESULT wraps Windows error 5."),
        [0x8007000E] = ("E_OUTOFMEMORY", "Memory allocation failed. This HRESULT wraps Windows error 14."),
        [0x80004005] = ("E_FAIL", "An unspecified COM or Windows component failure occurred.")
    };

    private static readonly IReadOnlyDictionary<string, string> EventMeanings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft-Windows-Kernel-Power|41"] = "Windows restarted without a clean shutdown. This confirms an abrupt interruption, but not whether power loss, reset, freeze, or bugcheck caused it.",
        ["EventLog|6008"] = "Windows recorded that the previous shutdown was unexpected.",
        ["EventLog|6005"] = "The Windows Event Log service started; this is commonly used as a boot boundary.",
        ["Microsoft-Windows-Kernel-General|12"] = "The operating system started at the recorded time.",
        ["Microsoft-Windows-WER-SystemErrorReporting|1001"] = "Windows Error Reporting saved information about a system bugcheck or fatal error.",
        ["BugCheck|1001"] = "Windows restarted after a bugcheck and recorded the stop code and parameters.",
        ["Microsoft-Windows-WHEA-Logger|18"] = "A fatal hardware error was reported through WHEA. Processor/APIC, bank and machine-check fields help narrow the component.",
        ["Microsoft-Windows-WHEA-Logger|19"] = "A corrected hardware error occurred. Windows recovered, but repetition can be significant.",
        ["disk|7"] = "The storage device reported a bad block.",
        ["disk|51"] = "An I/O operation was retried after a paging or storage error.",
        ["disk|153"] = "A storage I/O operation was retried after exceeding its expected completion time.",
        ["Ntfs|55"] = "NTFS detected filesystem structure corruption or inconsistency.",
        ["Microsoft-Windows-Ntfs|55"] = "NTFS detected filesystem structure corruption or inconsistency.",
        ["Application Error|1000"] = "An application terminated because of an unhandled fault; its module and exception code identify the failure.",
        ["Windows Error Reporting|1001"] = "Windows Error Reporting created or classified a failure report.",
        ["Service Control Manager|7000"] = "A Windows service failed to start.",
        ["Service Control Manager|7009"] = "Windows timed out while waiting for a service to connect.",
        ["Service Control Manager|7031"] = "A Windows service terminated unexpectedly.",
        ["Service Control Manager|7034"] = "A Windows service terminated unexpectedly without a recovery action."
    };

    /// <summary>
    /// Resolves a stop code against the detailed table first and the breadth-first
    /// catalogue second, so a code this app cannot analyse deeply still gets a name
    /// and a sentence instead of raw hexadecimal.
    /// </summary>
    private static (string Name, string Meaning)? LookUpBugCheck(ulong code)
    {
        if (BugChecks.TryGetValue(code, out var known)) return known;
        return BugCheckCatalog.Find(code) is { } summary ? (summary.Name, summary.Meaning) : null;
    }

    public static string DescribeIncident(Incident incident)
    {
        if (TryParseNumber(incident.BugCheckCode, out var code) && code != 0 && LookUpBugCheck(code) is { } known) return $"{FormatHex(code, 8)} — {known.Name}: {known.Meaning}";
        return incident.Kind switch
        {
            IncidentKind.PowerLossOrFreeze => "Abrupt restart recorded. Event 41 does not by itself prove whether power, reset, freeze, or a bugcheck caused it.",
            IncidentKind.UnexpectedShutdown => "Windows detected the previous session ended without a clean shutdown.",
            IncidentKind.HardwareError => "A WHEA hardware-error record was captured. Its processor, bank, device, and error-status fields are the useful details.",
            IncidentKind.ApplicationCrash => DescribeApplicationCrash(incident.Summary),
            _ => "Windows recorded an incident. The original event data is preserved alongside this explanation."
        };
    }

    public static string GetBugCheckLabel(string? rawCode)
    {
        if (!TryParseNumber(rawCode, out var code)) return rawCode ?? "code unavailable";
        return LookUpBugCheck(code) is { } known ? $"{FormatHex(code, 8)} — {known.Name}" : FormatHex(code, 8);
    }

    /// <summary>
    /// A short plain-English title for a stop code — "The graphics card stopped
    /// responding and could not be reset" rather than VIDEO_TDR_FAILURE. Returns
    /// null when the code is not in the catalogue, so callers can stay honest.
    /// </summary>
    public static string? GetBugCheckPlainTitle(string? rawCode)
        => TryParseNumber(rawCode, out var code) && BugCheckCatalog.Find(code) is { } summary ? summary.PlainTitle : null;

    /// <summary>The part of Windows that reported a stop code, for grouping and filtering.</summary>
    public static BugCheckFamily GetBugCheckFamily(string? rawCode)
        => TryParseNumber(rawCode, out var code) && BugCheckCatalog.Find(code) is { } summary ? summary.Family : BugCheckFamily.Unknown;

    public static IReadOnlyList<CodeInterpretation> Interpret(Incident incident, IReadOnlyList<EvidenceEvent> events)
    {
        var result = new List<CodeInterpretation>();
        if (TryParseNumber(incident.BugCheckCode, out var bugCheck) && bugCheck != 0)
        {
            if (LookUpBugCheck(bugCheck) is { } known) result.Add(new("Bugcheck", incident.BugCheckCode!, known.Name, known.Meaning, $"Unsigned decimal: {bugCheck:N0}; padded hex: {FormatHex(bugCheck, 8)}"));
            else result.Add(new("Bugcheck", incident.BugCheckCode!, "Unknown or uncommon bugcheck", "The raw stop code is preserved for WinDbg or Microsoft documentation lookup.", $"Unsigned decimal: {bugCheck:N0}; padded hex: {FormatHex(bugCheck, 8)}"));
        }
        if (incident.Parameters is not null)
            for (var index = 0; index < incident.Parameters.Count; index++)
                if (TryParseNumber(incident.Parameters[index], out var value)) result.Add(new("Bugcheck parameter", incident.Parameters[index], $"Parameter {index + 1}", "Parameters are context-specific addresses, flags, or status values. Conversion is shown without guessing its role.", $"Unsigned decimal: {value:N0}; padded hex: {FormatHex(value, 16)}"));
        foreach (var entry in events)
            result.AddRange(InterpretEvent(entry));
        return result.DistinctBy(x => $"{x.Category}|{x.RawValue}|{x.Name}", StringComparer.OrdinalIgnoreCase).Take(100).ToList();
    }

    public static string DescribeEvent(EvidenceEvent entry)
    {
        var decoded = InterpretEvent(entry);
        if (decoded.Count == 0) return $"This app has no entry for {entry.Provider} event {entry.EventId}. The original record is preserved in full below — use Research to look the pair up.";
        return string.Join(" ", decoded.Take(6).Select(item => $"{item.RawValue}: {item.Name} — {item.Explanation}"));
    }

    /// <summary>
    /// The plain-English title for an event, and how much attention it deserves.
    /// Returns null when the pair is not catalogued, so the caller can say so
    /// rather than implying the event was inspected and found harmless.
    /// </summary>
    public static EventMeaning? LookUpEvent(EvidenceEvent entry) => EventCatalog.Find(entry.Provider, entry.EventId);

    private static IReadOnlyList<CodeInterpretation> InterpretEvent(EvidenceEvent entry)
    {
        var result = new List<CodeInterpretation>();
        if (EventMeanings.TryGetValue($"{entry.Provider}|{entry.EventId}", out var meaning)) result.Add(new("Windows event", $"{entry.Provider} / {entry.EventId}", $"Event {entry.EventId}", meaning));
        else if (EventCatalog.Find(entry.Provider, entry.EventId) is { } catalogued)
            result.Add(new("Windows event", $"{entry.Provider} / {entry.EventId}", catalogued.PlainTitle, catalogued.Meaning, $"{EventCatalog.AreaLabel(catalogued.Area)} · {EventCatalog.ToneLabel(catalogued.Tone)}"));
        foreach (Match match in HexCodeRegex().Matches(entry.Message)) AddNumericInterpretation(result, match.Value);
        if (entry.Data is not null)
        {
            foreach (var pair in entry.Data.Where(pair => IsCodeField(pair.Key)))
            {
                var raw = pair.Value.Trim();
                if (BareHexRegex().IsMatch(raw)) AddNumericInterpretation(result, raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw : "0x" + raw);
            }
        }
        return result.DistinctBy(x => $"{x.Category}|{x.RawValue}|{x.Name}", StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddNumericInterpretation(List<CodeInterpretation> result, string raw)
    {
        if (!TryParseNumber(raw, out var value)) return;
        if (StatusCodes.TryGetValue(value, out var status))
        {
            result.Add(new("Exception/status", raw, status.Name, status.Meaning, $"Unsigned decimal: {value:N0}"));
            return;
        }
        if ((value & 0xFFFF0000UL) == 0x80070000UL)
        {
            var win32 = (int)(value & 0xFFFF); var message = new Win32Exception(win32).Message;
            result.Add(new("HRESULT", raw, $"HRESULT_FROM_WIN32({win32})", message, $"Embedded Windows error: {win32}; unsigned decimal: {value:N0}"));
            return;
        }
        // NTSTATUS values not in the short table above are still worth naming.
        if (TypedCodeDecoders.DecodeNtStatus(value) is { } ntStatus)
        {
            result.Add(new("Exception/status", raw, ntStatus.Name, ntStatus.Explanation, $"Unsigned decimal: {value:N0}"));
            return;
        }
        // A bare Windows error number, as printed by service and installer records.
        if (value is > 0 and <= 15999 && DescribeWin32(unchecked((int)value)) is { } win32Text)
        {
            result.Add(new("Windows error", raw, $"Windows error {value}", win32Text, $"Also written as 0x{value:X8}"));
            return;
        }
        // A failure HRESULT from a facility other than Win32. Only reported when
        // Windows actually has text for it — a generic "Unknown error" is noise.
        if ((value & 0x80000000UL) != 0 && value <= 0xFFFFFFFFUL && DescribeWin32(unchecked((int)value)) is { } hresultText)
        {
            result.Add(new("HRESULT", raw, $"HRESULT 0x{value:X8}", hresultText, $"Unsigned decimal: {value:N0}"));
            return;
        }
        result.Add(new("Hex value", raw, ClassifyBareValue(value), "No symbolic meaning is defined for this value in the code tables, so it is preserved exactly as Windows printed it rather than guessed at.", $"Unsigned decimal: {value:N0}; padded hex: {FormatHex(value, 8)}"));
    }

    /// <summary>
    /// Windows' own text for an error number, or null when Windows has none.
    /// Presenting "Unknown error (0x…)" as a decode is worse than admitting nothing
    /// was found, so that answer is filtered out here.
    /// </summary>
    private static string? DescribeWin32(int code)
    {
        try
        {
            var message = new Win32Exception(code).Message;
            return string.IsNullOrWhiteSpace(message) || message.StartsWith("Unknown error", StringComparison.OrdinalIgnoreCase) ? null : message;
        }
        catch { return null; }
    }

    /// <summary>
    /// Says what shape a number has when its meaning is unknown. "Looks like a
    /// kernel address" is a smaller claim than a decode, but it is still more
    /// than the reader had.
    /// </summary>
    private static string ClassifyBareValue(ulong value) => value switch
    {
        0 => "Zero",
        < 0x1000 => "Small value — a count, index or flag",
        _ when (value >> 48) == 0xFFFF => "Looks like a kernel-mode address",
        _ when value > 0xFFFFFFFFUL => "Looks like a 64-bit address",
        _ => "Unrecognised value"
    };

    private static string DescribeApplicationCrash(string summary)
    {
        var interpretations = new List<CodeInterpretation>();
        foreach (Match match in HexCodeRegex().Matches(summary)) AddNumericInterpretation(interpretations, match.Value);
        var known = interpretations.FirstOrDefault(item => item.Category is "Exception/status" or "HRESULT");
        return known is null ? "An application stopped unexpectedly. The faulting process, module and exception fields in the evidence help identify where it failed." : $"{known.RawValue} — {known.Name}: {known.Explanation}";
    }

    private static bool IsCodeField(string name) => name.Contains("code", StringComparison.OrdinalIgnoreCase) || name.Contains("status", StringComparison.OrdinalIgnoreCase) || name.Contains("hresult", StringComparison.OrdinalIgnoreCase) || name.Contains("exception", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseNumber(string? text, out ulong value)
    {
        value = 0; if (string.IsNullOrWhiteSpace(text)) return false; var clean = text.Trim().Replace("`", string.Empty).Replace("_", string.Empty);
        return clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ulong.TryParse(clean[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) : ulong.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
    private static string FormatHex(ulong value, int digits) => $"0x{value.ToString($"X{digits}", CultureInfo.InvariantCulture)}";
    [GeneratedRegex(@"0x[0-9a-fA-F]{2,16}\b", RegexOptions.CultureInvariant)] private static partial Regex HexCodeRegex();
    [GeneratedRegex(@"^(?:0x)?[0-9a-fA-F]{4,16}$", RegexOptions.CultureInvariant)] private static partial Regex BareHexRegex();
}
