using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

public sealed record ApplicationFailureDetails(
    string? Application,
    string? ApplicationVersion,
    string? FaultingModule,
    string? FaultingModuleVersion,
    string? ExceptionCode,
    string? ExceptionMeaning,
    string? FaultOffset,
    string? ProcessId,
    string? ApplicationPath,
    string? FaultingModulePath,
    string? ReportId,
    IReadOnlyList<string> LocalDumpFiles);

/// <summary>
/// Extracts the fields Windows records for an application failure (Application
/// Error 1000 / WER APPCRASH) so the report can state what failed and where,
/// instead of only that something failed. Field positions come from the event's
/// own data array, so extraction does not depend on the message language.
/// </summary>
public static partial class ApplicationFailureAnalyzer
{
    public static string LocalDumpDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");

    public static ApplicationFailureDetails? Extract(Incident incident, IReadOnlyList<EvidenceEvent> events)
    {
        if (incident.Kind != IncidentKind.ApplicationCrash) return null;
        var record = events
            .Where(entry => entry.Provider.Equals("Application Error", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1000)
            .OrderBy(entry => (entry.Timestamp - incident.Timestamp).Duration())
            .FirstOrDefault()
            ?? events.Where(entry => entry.Provider.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase) && entry.EventId == 1001)
                .OrderBy(entry => (entry.Timestamp - incident.Timestamp).Duration())
                .FirstOrDefault();
        if (record is null) return null;

        // Application Error 1000 publishes an ordered, unnamed data array; WER 1001
        // uses P-numbered values. Try both, then fall back to the rendered message.
        string? Positional(int index, params string[] names)
        {
            foreach (var name in names)
                if (record.Data?.TryGetValue(name, out var named) == true && !string.IsNullOrWhiteSpace(named)) return named.Trim();
            return record.Data?.TryGetValue($"Data{index}", out var value) == true && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        }
        string? FromMessage(Regex pattern) { var match = pattern.Match(record.Message); return match.Success ? match.Groups[1].Value.Trim() : null; }

        var application = Positional(0, "AppName", "ApplicationName", "P1") ?? FromMessage(ApplicationRegex());
        var exceptionCode = Positional(6, "ExceptionCode", "P7") ?? FromMessage(ExceptionRegex());
        var module = Positional(3, "ModuleName", "FaultingModule", "P4") ?? FromMessage(ModuleRegex());
        var applicationPath = Positional(10, "AppPath") ?? FromMessage(ApplicationPathRegex());

        return new(
            application,
            Positional(1, "AppVersion", "P2") ?? FromMessage(ApplicationVersionRegex()),
            module,
            Positional(4, "ModuleVersion", "P5"),
            exceptionCode,
            DescribeExceptionCode(exceptionCode),
            Positional(7, "ExceptionOffset", "P8"),
            Positional(8),
            applicationPath,
            Positional(11, "ModulePath"),
            Positional(12, "ReportId"),
            FindLocalDumps(application));
    }

    /// <summary>
    /// Explains exception codes an application failure actually produces. Codes in
    /// the 0xE0000000 range carry the "customer" bit: they are raised by a runtime
    /// or the application itself, so the faulting module is usually only where the
    /// raise surfaced, not the defect.
    /// </summary>
    public static string? DescribeExceptionCode(string? raw)
    {
        if (!NumericParser.TryParse(raw, out var value)) return null;
        var code = (uint)value;
        var known = code switch
        {
            0xE0434352 => ".NET runtime exception (CLR). The managed exception type, message and stack are not in this record; a crash dump or the application's own logging is required to identify them.",
            0xE06D7363 => "C++ exception raised through the Microsoft C++ runtime.",
            0xC0000005 => "STATUS_ACCESS_VIOLATION — the process read, wrote, or executed an address it could not.",
            0xC0000409 => "STATUS_STACK_BUFFER_OVERRUN — a security check failed (also used by fail-fast paths such as .NET Environment.FailFast).",
            0xC00000FD => "STATUS_STACK_OVERFLOW — the thread exhausted its stack, commonly through unbounded recursion.",
            0xC0000374 => "STATUS_HEAP_CORRUPTION — the heap manager detected corrupted structures.",
            0x80000003 => "A breakpoint was hit with no debugger attached.",
            _ => null
        };
        if (known is not null) return known;
        if ((code & 0xF0000000) == 0xE0000000)
            return "An application-defined exception (the 0xE0000000 range carries the customer bit): it was raised by the application or a runtime it uses, not by a processor or memory fault. KERNELBASE.dll commonly appears as the faulting module because that is where the raise surfaces; it is not the defect. The exception type and stack must come from a crash dump or the application's own logging.";
        if ((code & 0xC0000000) == 0xC0000000 && TypedCodeDecoders.DecodeNtStatus(code) is { } status)
            return $"{status.Name} — {status.Explanation}";
        return null;
    }

    public static IReadOnlyList<string> FindLocalDumps(string? application)
    {
        if (string.IsNullOrWhiteSpace(application)) return [];
        try
        {
            var directory = LocalDumpDirectory;
            if (!Directory.Exists(directory)) return [];
            return Directory.EnumerateFiles(directory, Path.GetFileNameWithoutExtension(application) + "*.dmp")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(5)
                .ToList();
        }
        catch { return []; }
    }

    [GeneratedRegex(@"Faulting application name:\s*([^,\r\n]+)", RegexOptions.CultureInvariant)] private static partial Regex ApplicationRegex();
    [GeneratedRegex(@"Faulting application name:[^,\r\n]*,\s*version:\s*([^,\r\n]+)", RegexOptions.CultureInvariant)] private static partial Regex ApplicationVersionRegex();
    [GeneratedRegex(@"Faulting module name:\s*([^,\r\n]+)", RegexOptions.CultureInvariant)] private static partial Regex ModuleRegex();
    [GeneratedRegex(@"Exception code:\s*(0x[0-9a-fA-F]+)", RegexOptions.CultureInvariant)] private static partial Regex ExceptionRegex();
    [GeneratedRegex(@"Faulting application path:\s*(.+)", RegexOptions.CultureInvariant)] private static partial Regex ApplicationPathRegex();
}
