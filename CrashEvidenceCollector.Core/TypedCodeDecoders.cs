using System.ComponentModel;

namespace CrashEvidenceCollector.Core;

public sealed record TypedCodeResult(string Raw, string Name, string Explanation, string Type, long SignedValue, ulong UnsignedValue);

/// <summary>
/// Context-specific status decoders. Callers choose the decoder from the field
/// semantics; arbitrary addresses and flags never pass through this class.
/// </summary>
public static class TypedCodeDecoders
{
    private static readonly IReadOnlyDictionary<ulong, (string Name, string Meaning)> NtStatus = new Dictionary<ulong, (string, string)>
    {
        [0x00000000] = ("STATUS_SUCCESS", "The operation completed successfully."),
        [0x80000003] = ("STATUS_BREAKPOINT", "A breakpoint exception occurred."),
        [0xC0000005] = ("STATUS_ACCESS_VIOLATION", "Invalid memory was read, written, or executed."),
        [0xC0000017] = ("STATUS_NO_MEMORY", "Required virtual memory or paging resources could not be allocated."),
        [0xC000001D] = ("STATUS_ILLEGAL_INSTRUCTION", "The processor encountered an invalid or unsupported instruction."),
        [0xC0000094] = ("STATUS_INTEGER_DIVIDE_BY_ZERO", "An integer calculation attempted division by zero."),
        [0xC0000096] = ("STATUS_PRIVILEGED_INSTRUCTION", "Code attempted a privileged instruction from an invalid execution level."),
        [0xC000009A] = ("STATUS_INSUFFICIENT_RESOURCES", "Windows lacked a required system resource."),
        [0xC00000B5] = ("STATUS_IO_TIMEOUT", "An I/O operation timed out."),
        [0xC00000FD] = ("STATUS_STACK_OVERFLOW", "The thread exhausted its available stack."),
        [0xC000009D] = ("STATUS_DEVICE_NOT_CONNECTED", "The target device was not connected."),
        [0xC00000A3] = ("STATUS_DEVICE_NOT_READY", "The target device was not ready."),
        [0xC0000185] = ("STATUS_IO_DEVICE_ERROR", "An I/O device reported an error."),
        [0xC000021A] = ("STATUS_SYSTEM_PROCESS_TERMINATED", "A critical Windows subsystem terminated."),
        [0xC0000374] = ("STATUS_HEAP_CORRUPTION", "Heap metadata corruption was detected."),
        [0xC0000409] = ("STATUS_STACK_BUFFER_OVERRUN / FAIL_FAST", "The process terminated after detecting a fail-fast or security-critical corruption condition.")
    };

    private static readonly IReadOnlyDictionary<int, string> CmProblems = new Dictionary<int, string>
    {
        [1]="Device is not configured correctly.",[10]="Device cannot start.",[12]="Device cannot find enough free resources.",[14]="Device cannot work properly until the computer restarts.",[19]="Configuration information is incomplete or damaged.",[21]="Windows is removing this device.",[22]="Device is disabled.",[24]="Device is not present, not working, or lacks installed drivers.",[28]="Drivers for this device are not installed.",[31]="Device is not working properly because Windows cannot load its drivers.",[43]="Windows stopped this device because it reported problems.",[45]="Device is not currently connected.",[48]="Software for this device was blocked from starting." 
    };

    public static TypedCodeResult? DecodeNtStatus(ulong value) => NtStatus.TryGetValue(value & 0xFFFFFFFF, out var item) ? Result(value, item.Name, item.Meaning, "NTSTATUS/exception") : null;

    public static TypedCodeResult DecodeWin32(uint value)
    {
        var message = new Win32Exception(unchecked((int)value)).Message;
        return Result(value, $"WIN32_ERROR_{value}", message, "Win32");
    }

    public static TypedCodeResult DecodeHResult(ulong value)
    {
        var low = (uint)(value & 0xFFFF); var fromWin32 = (value & 0xFFFF0000UL) == 0x80070000UL;
        return fromWin32 ? Result(value, $"HRESULT_FROM_WIN32({low})", new Win32Exception((int)low).Message, "HRESULT") : Result(value, $"HRESULT 0x{value:X8}", new Win32Exception(unchecked((int)value)).Message, "HRESULT");
    }

    public static TypedCodeResult DecodeCmProblem(int value) => Result((ulong)value, $"CM_PROB {value}", CmProblems.TryGetValue(value, out var message) ? message : "Device Manager problem code is not in the local definition set.", "CM_PROB");

    public static string DecodeAccessViolationOperation(IReadOnlyList<string> parameters, out string? targetAddress)
    {
        targetAddress = parameters.Count > 1 && NumericParser.TryParse(parameters[1], out var target) ? $"0x{target:X}" : parameters.Count > 1 ? parameters[1] : null;
        if (parameters.Count == 0 || !NumericParser.TryParse(parameters[0], out var operation)) return "Operation was not available.";
        return operation switch { 0 => "Attempted read", 1 => "Attempted write", 8 => "Attempted execution", _ => $"Unknown access operation {parameters[0]}" };
    }

    public static string ClassifyAddress(string? raw)
    {
        if (!NumericParser.TryParse(raw, out var value)) return "Address unavailable";
        if (value < 0x10000) return "Near-null address";
        if (!NumericParser.IsCanonicalAddress(value)) return "Noncanonical address";
        if ((value & 0xFFFF000000000000UL) == 0xFFFF000000000000UL) return "Canonical kernel address";
        return "Canonical user-mode address";
    }

    private static TypedCodeResult Result(ulong value, string name, string explanation, string type) => new($"0x{value:X8}", name, explanation, type, unchecked((long)value), value);
}
