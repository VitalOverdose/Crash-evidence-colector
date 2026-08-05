using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

public sealed record DebuggerCommand(string Command, string Purpose, bool AddressSpecific = false);
public sealed record DebuggerExecutionResult(CommandResult Process, DateTimeOffset Started, DateTimeOffset Ended, string SanitizedCommandLine, string ScriptPath, IReadOnlyList<DebuggerCommand> Commands, IReadOnlyList<string> UnsupportedCommands);

public static class DebuggerLocator
{
    public static string? FindCdb()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Windows Kits\10\Debuggers\x64\cdb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Windows Kits\10\Debuggers\x64\cdb.exe"),
            Environment.GetEnvironmentVariable("CDB_PATH")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }
}

public static class DumpClassifier
{
    public static DumpType ClassifyPath(string path)
    {
        var normalized = path.Replace('/', '\\'); var file = Path.GetFileName(path);
        if (normalized.Contains(@"\LiveKernelReports\WATCHDOG\", StringComparison.OrdinalIgnoreCase)) return DumpType.WatchdogLiveDump;
        if (normalized.Contains(@"\LiveKernelReports\", StringComparison.OrdinalIgnoreCase) && (normalized.Contains("DISPLAY", StringComparison.OrdinalIgnoreCase) || normalized.Contains("GRAPHICS", StringComparison.OrdinalIgnoreCase))) return DumpType.GraphicsLiveDump;
        if (normalized.Contains(@"\LiveKernelReports\", StringComparison.OrdinalIgnoreCase)) return DumpType.LiveKernelReport;
        if (normalized.Contains(@"\WER\", StringComparison.OrdinalIgnoreCase)) return DumpType.WerMinidump;
        if (normalized.Contains(@"\Minidump\", StringComparison.OrdinalIgnoreCase) || file.StartsWith("Mini", StringComparison.OrdinalIgnoreCase)) return DumpType.SmallMemory;
        if (file.Equals("MEMORY.DMP", StringComparison.OrdinalIgnoreCase)) return DumpType.AutomaticMemory;
        return DumpType.Unknown;
    }

    public static DumpType Refine(DumpType initial, string debuggerOutput)
    {
        if (debuggerOutput.Contains("Active Memory Dump", StringComparison.OrdinalIgnoreCase)) return DumpType.ActiveMemory;
        if (debuggerOutput.Contains("Complete Memory Dump", StringComparison.OrdinalIgnoreCase) || debuggerOutput.Contains("Full Memory Dump", StringComparison.OrdinalIgnoreCase)) return DumpType.CompleteMemory;
        if (debuggerOutput.Contains("Kernel Bitmap Dump", StringComparison.OrdinalIgnoreCase) || debuggerOutput.Contains("Kernel Memory Dump", StringComparison.OrdinalIgnoreCase)) return DumpType.KernelMemory;
        if (debuggerOutput.Contains("Mini Kernel Dump", StringComparison.OrdinalIgnoreCase) || debuggerOutput.Contains("Small Memory Dump", StringComparison.OrdinalIgnoreCase)) return DumpType.SmallMemory;
        return initial;
    }

    public static string DetectArchitecture(string output) => output.Contains("AMD64", StringComparison.OrdinalIgnoreCase) || output.Contains("x64", StringComparison.OrdinalIgnoreCase) ? "x64" : output.Contains("ARM64", StringComparison.OrdinalIgnoreCase) ? "ARM64" : output.Contains("x86", StringComparison.OrdinalIgnoreCase) ? "x86" : "Unknown";
}

public static class DebuggerCommandPlanner
{
    private static readonly Regex Placeholder = new(@"\{P([1-4])\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<DebuggerCommand> Plan(DumpType dumpType, ulong? bugCheckCode, IReadOnlyList<string> parameters)
    {
        // Baseline commands are deterministic constants. No command text is ever
        // read from the dump, debugger output, event log, or user input.
        var commands = new List<DebuggerCommand>
        {
            new(".symfix+", "Add the Microsoft public symbol server to the configured local cache"),
            // Capture the dump's own code before any network-dependent symbol
            // operation so a slow symbol server cannot erase association proof.
            new(".bugcheck", "Raw bugcheck and parameters"),
            // Reload only the Windows kernel before the essential analysis. A
            // forced reload of every third-party image can consume the complete
            // timeout before !analyze runs, especially on the first new dump.
            new(".reload /f nt", "Load and validate Windows kernel symbols"),
            new("!analyze -v", "Verbose debugger analysis"),
            new("vertarget", "Target OS and architecture"),
            new("!sysinfo machineid", "Machine identity"), new("!sysinfo smbios", "Firmware information"), new("!sysinfo cpuinfo", "Processor information"),
            new(".exr -1", "Current exception record"), new(".cxr -1", "Current context record"), new(".ecxr", "Exception context when available"),
            new("r", "Registers"), new("kv", "Verbose stack"), new("kp", "Stack with parameters"), new("kP", "Expanded stack parameters"),
            new("!thread", "Current thread"), new("!process 0 1", "Process summary"), new("lm", "Loaded modules"), new("lm t n", "Module timestamps and names"),
            new("!blackboxbsd", "Boot status black box"), new("!blackboxntfs", "NTFS black box"), new("!blackboxpnp", "PnP black box"),
            new("!blackboxwinlogon", "Winlogon black box"), new("!blackboxstorage", "Storage black box"),
            new("!fltkd.filters", "Filesystem minifilters"), new("!fltkd.instances", "Filesystem minifilter instances")
        };
        if (dumpType is not (DumpType.SmallMemory or DumpType.WerMinidump)) commands.Add(new("!stacks 2", "Kernel thread stacks"));

        var definition = bugCheckCode is null ? null : BugCheckKnowledge.Find(bugCheckCode.Value);
        if (definition is not null)
        {
            foreach (var template in definition.RelevantCommands)
            {
                var valid = true;
                var command = Placeholder.Replace(template, match =>
                {
                    var index = int.Parse(match.Groups[1].Value) - 1;
                    if (index >= parameters.Count || !NumericParser.TryParse(parameters[index], out var address) || !NumericParser.IsCanonicalAddress(address) || address < 0x10000) { valid = false; return "0"; }
                    return $"0x{address:X}";
                });
                if (valid && !command.Contains("{MODULE}", StringComparison.Ordinal)) commands.Add(new(command, $"Bugcheck-specific command for {definition.Name}", template.Contains("{P", StringComparison.Ordinal)));
            }
            if (bugCheckCode == 0x9F) { commands.Add(new("!poaction", "Power action and blocked requests")); commands.Add(new("!locks", "Locks relevant to power transition")); }
            if (bugCheckCode == 0x133) { commands.Add(new("!dpcs", "Queued DPCs")); commands.Add(new("!timer", "Timers")); }
        }
        // The same display command may intentionally run again after .cxr/.trap.
        // Deduplicate only identical command/purpose pairs so reconstructed-context
        // registers and stacks are not lost behind the baseline stack.
        return commands.DistinctBy(command => $"{command.Command}|{command.Purpose}", StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class DumpDebugger(StructuredLog log)
{
    public async Task<DebuggerExecutionResult> ExecuteAsync(string dumpPath, string rawDirectory, ulong? bugCheckCode, IReadOnlyList<string> parameters, TimeSpan timeout, CancellationToken cancellationToken, IProgress<CommandExecutionProgress>? commandProgress = null)
    {
        var cdb = DebuggerLocator.FindCdb() ?? throw new FileNotFoundException("CDB was not found. Install Debugging Tools for Windows or set CDB_PATH.");
        Directory.CreateDirectory(rawDirectory);
        var cache = Path.Combine(AppPaths.StateDirectory, "Symbols"); Directory.CreateDirectory(cache);
        var symbolPath = $"srv*{cache}*https://msdl.microsoft.com/download/symbols";
        var dumpType = DumpClassifier.ClassifyPath(dumpPath);
        var commands = DebuggerCommandPlanner.Plan(dumpType, bugCheckCode, parameters);
        var scriptPath = Path.Combine(rawDirectory, Path.GetFileNameWithoutExtension(dumpPath) + "-commands.txt");
        var script = new StringBuilder();
        foreach (var command in commands)
        {
            script.AppendLine($".echo === CEC COMMAND: {command.Command} ===");
            script.AppendLine(command.Command);
        }
        script.AppendLine(".echo === CEC ANALYSIS END ==="); script.AppendLine("q");
        await File.WriteAllTextAsync(scriptPath, script.ToString(), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

        var arguments = new[] { "-z", dumpPath, "-y", symbolPath, "-cf", scriptPath };
        var sanitized = $"\"{cdb}\" -z \"[DUMP]\\{Path.GetFileName(dumpPath)}\" -y \"srv*[LOCAL-SYMBOL-CACHE]*https://msdl.microsoft.com/download/symbols\" -cf \"[EVIDENCE]\\{Path.GetFileName(scriptPath)}\"";
        var started = DateTimeOffset.Now;
        await log.WriteAsync("information", "Debugger analysis started", new { dump = Path.GetFileName(dumpPath), timeout, commands = commands.Count }, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await CommandRunner.RunAsync(cdb, arguments, timeout, cancellationToken, commandProgress).ConfigureAwait(false);
            var ended = DateTimeOffset.Now;
            var unsupported = ParseUnsupportedCommands(result.StandardOutput + Environment.NewLine + result.StandardError, commands);
            return new(result, started, ended, sanitized, scriptPath, commands, unsupported);
        }
        catch (OperationCanceledException)
        {
            await log.WriteAsync("warning", "Debugger analysis cancelled", new { dump = Path.GetFileName(dumpPath) }, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static IReadOnlyList<string> ParseUnsupportedCommands(string output, IReadOnlyList<DebuggerCommand> commands)
    {
        var result = new List<string>();
        var sections = Regex.Split(output, @"(?m)^=== CEC COMMAND: ");
        foreach (var section in sections.Skip(1))
        {
            var newline = section.IndexOfAny(['\r','\n']); var command = newline > 0 ? section[..newline].TrimEnd('=', ' ') : section;
            if (section.Contains("not a recognized", StringComparison.OrdinalIgnoreCase) || section.Contains("extension not found", StringComparison.OrdinalIgnoreCase) || section.Contains("not available in this dump", StringComparison.OrdinalIgnoreCase) || section.Contains("unable to read", StringComparison.OrdinalIgnoreCase)) result.Add(command);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public static class DumpEvidenceFactory
{
    public static async Task<DumpEvidence> CreateAsync(string sourcePath, string copiedPath, TimeSpan timeout, CancellationToken token, IProgress<long>? byteProgress = null)
    {
        var info = new FileInfo(copiedPath);
        await using var stream = new FileStream(copiedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        // Incremental hashing provides byte-level feedback for multi-gigabyte
        // kernel/full dumps while retaining constant memory usage.
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024]; long totalRead = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) break;
            hasher.AppendData(buffer, 0, read); totalRead += read; byteProgress?.Report(totalRead);
        }
        var hash = hasher.GetHashAndReset();
        var cdb = DebuggerLocator.FindCdb();
        return new()
        {
            SourcePath = sourcePath, CopiedPath = copiedPath, FileSize = info.Length, CreationTime = info.CreationTimeUtc,
            ModificationTime = info.LastWriteTimeUtc, CopiedFileCreationTime = info.CreationTimeUtc, CopiedFileModificationTime = info.LastWriteTimeUtc,
            Sha256 = Convert.ToHexString(hash), DumpType = DumpClassifier.ClassifyPath(sourcePath),
            DebuggerPath = cdb ?? string.Empty, DebuggerVersion = cdb is null ? "Unavailable" : FileVersionInfo.GetVersionInfo(cdb).FileVersion ?? "Unknown", CommandTimeout = timeout
        };
    }
}
