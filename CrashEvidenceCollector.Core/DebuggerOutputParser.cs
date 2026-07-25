using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

/// <summary>
/// Line-oriented parser for CDB/KD/WinDbg text. It uses small expressions for
/// individual fields and explicit section state; unknown fields and command
/// sections remain available in the result instead of being discarded.
/// </summary>
public sealed partial class DebuggerOutputParser
{
    private static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "BUGCHECK_CODE","BUGCHECK_STR","BUGCHECK_P1","BUGCHECK_P2","BUGCHECK_P3","BUGCHECK_P4","PROCESS_NAME","THREAD","TRAP_FRAME","CONTEXT","CONTEXT_RECORD","EXCEPTION_RECORD","EXCEPTION_CODE","EXCEPTION_PARAMETER1","EXCEPTION_PARAMETER2","EXCEPTION_PARAMETER3","EXCEPTION_PARAMETER4","READ_ADDRESS","WRITE_ADDRESS","EXECUTE_ADDRESS","FAULTING_IP","SYMBOL_NAME","MODULE_NAME","IMAGE_NAME","IMAGE_VERSION","STACK_COMMAND","STACK_TEXT","FAILURE_BUCKET_ID","HASH_STRING","FAILURE_ID_HASH","DEFAULT_BUCKET_ID","CUSTOMER_CRASH_COUNT","BLACKBOXBSD","BLACKBOXNTFS","BLACKBOXPNP","BLACKBOXWINLOGON","DUMP_FILE_ATTRIBUTES"
    };

    public DumpAnalysisResult Parse(string standardOutput, string standardError, DumpEvidence dump)
    {
        var result = new DumpAnalysisResult { Dump = dump };
        var analyze = result.Analyze;
        var normalized = (standardOutput + (standardError.Length > 0 ? Environment.NewLine + "=== STDERR ===" + Environment.NewLine + standardError : string.Empty)).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var currentField = string.Empty; var currentValue = new StringBuilder();
        var currentCommand = "Debugger startup"; var commandValue = new StringBuilder(); string? pendingKey = null;

        void CommitField()
        {
            if (currentField.Length == 0) return;
            AssignField(analyze, currentField, currentValue.ToString().TrimEnd()); currentField = string.Empty; currentValue.Clear();
        }
        void CommitCommand()
        {
            if (commandValue.Length == 0) return;
            analyze.RawSections[currentCommand] = commandValue.ToString().TrimEnd(); commandValue.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var marker = CommandMarkerRegex().Match(line);
            if (marker.Success) { CommitField(); CommitCommand(); currentCommand = marker.Groups[1].Value.Trim(); continue; }
            commandValue.AppendLine(line);

            var keyLine = KeyLineRegex().Match(line);
            if (keyLine.Success) { pendingKey = keyLine.Groups[1].Value.Trim(); continue; }
            var valueLine = ValueLineRegex().Match(line);
            if (valueLine.Success && pendingKey is not null) { analyze.KeyValues[pendingKey] = valueLine.Groups[1].Value.Trim(); pendingKey = null; continue; }
            var arg = ArgumentRegex().Match(line);
            if (arg.Success)
            {
                var index = int.Parse(arg.Groups[1].Value, CultureInfo.InvariantCulture) - 1;
                while (analyze.BugCheckParameters.Count <= index) analyze.BugCheckParameters.Add(string.Empty);
                analyze.BugCheckParameters[index] = NormalizeNumeric(arg.Groups[2].Value);
                continue;
            }
            var field = FieldRegex().Match(line);
            if (field.Success && (KnownFields.Contains(field.Groups[1].Value) || LooksLikeDebuggerField(field.Groups[1].Value)))
            {
                CommitField(); currentField = field.Groups[1].Value.ToUpperInvariant(); currentValue.Append(field.Groups[2].Value); continue;
            }
            if (currentField.Length > 0)
            {
                // STACK_TEXT and several debugger values are multiline. A blank line
                // ends ordinary fields but is retained inside explicit stack output.
                if (line.Length == 0 && !currentField.Equals("STACK_TEXT", StringComparison.OrdinalIgnoreCase)) CommitField();
                else { if (currentValue.Length > 0) currentValue.AppendLine(); currentValue.Append(line); }
            }

            var keyValue = KeyValueRegex().Match(line);
            if (keyValue.Success) analyze.KeyValues[keyValue.Groups[1].Value.Trim()] = keyValue.Groups[2].Value.Trim();
            if (line.Contains("WARNING", StringComparison.OrdinalIgnoreCase) || line.Contains("ERROR:", StringComparison.OrdinalIgnoreCase) || line.Contains("unable to", StringComparison.OrdinalIgnoreCase)) analyze.Warnings.Add(line.Trim());
        }
        CommitField(); CommitCommand();

        // Stack parsing consumes both !analyze STACK_TEXT and explicit kv/kp/kP
        // sections, deduplicating frames without losing each raw source section.
        var stackSources = analyze.RawSections.Where(pair => pair.Key.Equals("kv", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("kp", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("kP", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).ToList();
        if (analyze.RawSections.TryGetValue("STACK_TEXT", out var stackText)) stackSources.Insert(0, stackText);
        if (analyze.UnknownFields.TryGetValue("STACK_TEXT", out var unknownStack)) stackSources.AddRange(unknownStack);
        result.StackFrames = ParseStackFrames(stackSources).ToList();
        result.Modules = ParseModules(analyze.RawSections, result.StackFrames).ToList();
        result.Symbols = ParseSymbols(normalized, result.StackFrames);
        dump.DumpHeaderTime = ParseDumpHeaderTime(normalized);
        ParseExceptionRecord(analyze);
        ParseAnalysisElapsed(analyze);
        return result;
    }

    private static void AssignField(AnalyzeData analyze, string field, string value)
    {
        var first = value.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
        switch (field.ToUpperInvariant())
        {
            case "BUGCHECK_CODE": analyze.BugCheckCode = NormalizeNumeric(first); break;
            case "BUGCHECK_STR": analyze.BugCheckString = first; break;
            case "BUGCHECK_P1": case "BUGCHECK_P2": case "BUGCHECK_P3": case "BUGCHECK_P4":
                var index = field[^1] - '1'; while (analyze.BugCheckParameters.Count <= index) analyze.BugCheckParameters.Add(string.Empty); analyze.BugCheckParameters[index] = NormalizeNumeric(first); break;
            case "PROCESS_NAME": analyze.ProcessName = first; break;
            case "THREAD": analyze.Thread = FirstAddress(first); break;
            case "TRAP_FRAME": analyze.TrapFrame = FirstAddress(first); break;
            case "CONTEXT": case "CONTEXT_RECORD": analyze.ContextRecord = FirstAddress(first); break;
            case "EXCEPTION_RECORD": analyze.ExceptionRecord = FirstAddress(first); break;
            case "EXCEPTION_CODE": analyze.ExceptionCode = NormalizeNumeric(first.Split(' ', '(')[0]); break;
            case "EXCEPTION_PARAMETER1": case "EXCEPTION_PARAMETER2": case "EXCEPTION_PARAMETER3": case "EXCEPTION_PARAMETER4": analyze.ExceptionParameters.Add(NormalizeNumeric(first)); break;
            case "READ_ADDRESS": analyze.ReadAddress = FirstAddress(first); break;
            case "WRITE_ADDRESS": analyze.WriteAddress = FirstAddress(first); break;
            case "EXECUTE_ADDRESS": analyze.ExecuteAddress = FirstAddress(first); break;
            case "FAULTING_IP": analyze.FaultingIp = first; break;
            case "SYMBOL_NAME": analyze.SymbolName = first; break;
            case "MODULE_NAME": analyze.ModuleName = first; break;
            case "IMAGE_NAME": analyze.ImageName = first; break;
            case "IMAGE_VERSION": analyze.ImageVersion = first; break;
            case "STACK_COMMAND": analyze.StackCommand = first; break;
            case "FAILURE_BUCKET_ID": analyze.FailureBucketId = first; break;
            case "HASH_STRING": analyze.HashString = first; break;
            case "FAILURE_ID_HASH": analyze.FailureIdHash = first; break;
            case "DEFAULT_BUCKET_ID": analyze.DefaultBucketId = first; break;
            case "CUSTOMER_CRASH_COUNT": if (int.TryParse(first, out var count)) analyze.CustomerCrashCount = count; break;
            case "BLACKBOXBSD": case "BLACKBOXNTFS": case "BLACKBOXPNP": case "BLACKBOXWINLOGON": analyze.BlackBoxes[field] = value; break;
            case "DUMP_FILE_ATTRIBUTES": analyze.DumpAttributes.AddRange(value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
            case "STACK_TEXT": analyze.RawSections["STACK_TEXT"] = value; break;
            default:
                if (!analyze.UnknownFields.TryGetValue(field, out var values)) analyze.UnknownFields[field] = values = [];
                values.Add(value); break;
        }
    }

    public static IReadOnlyList<StackFrame> ParseStackFrames(IEnumerable<string> sections)
    {
        var frames = new List<StackFrame>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var generatedIndex = 0;
        foreach (var section in sections)
        {
            foreach (var line in section.Replace("\r\n", "\n").Split('\n'))
            {
                var match = StackRegex().Match(line);
                if (!match.Success) continue;
                var combined = match.Groups["target"].Value; var bang = combined.IndexOf('!');
                var module = bang > 0 ? combined[..bang] : null; var symbolPart = bang > 0 ? combined[(bang + 1)..] : combined;
                var plus = symbolPart.LastIndexOf("+0x", StringComparison.OrdinalIgnoreCase); var symbol = plus > 0 ? symbolPart[..plus] : symbolPart; var displacement = plus > 0 ? symbolPart[(plus + 1)..] : null;
                var sourceMatch = SourceRegex().Match(line); var category = ModuleClassifier.Classify(module, null, null);
                var transition = ClassifyTransition(module, symbol);
                var index = match.Groups["index"].Success && int.TryParse(match.Groups["index"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : generatedIndex;
                var key = $"{match.Groups["sp"].Value}|{match.Groups["ret"].Value}|{combined}"; if (!seen.Add(key)) continue;
                frames.Add(new(index, CleanAddress(match.Groups["sp"].Value), CleanAddress(match.Groups["ret"].Value), module, symbol, displacement, sourceMatch.Success ? sourceMatch.Groups["file"].Value : null, sourceMatch.Success && int.TryParse(sourceMatch.Groups["line"].Value, out var sourceLine) ? sourceLine : null, line, bang > 0 && !combined.Contains("???", StringComparison.Ordinal), category, transition is not null, transition, StackRelationship.ParticipatingLowerInStack)); generatedIndex++;
            }
        }
        if (frames.Count > 0) frames[0] = frames[0] with { Relationship = StackRelationship.ExecutingAtFault };
        if (frames.Count > 1) frames[1] = frames[1] with { Relationship = StackRelationship.DirectCaller };
        return frames.OrderBy(frame => frame.Index).ToList();
    }

    public static IReadOnlyList<DriverIdentity> ParseModules(IReadOnlyDictionary<string, string> sections, IReadOnlyList<StackFrame> frames)
    {
        var identities = new Dictionary<string, DriverIdentity>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections.Where(pair => pair.Key.Equals("lm", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("lm t n", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value))
        {
            foreach (var line in section.Replace("\r\n", "\n").Split('\n'))
            {
                var match = ModuleRegex().Match(line); if (!match.Success) continue;
                var name = match.Groups["name"].Value; identities[name] = new() { ModuleName = name, BaseAddress = CleanAddress(match.Groups["start"].Value), EndAddress = CleanAddress(match.Groups["end"].Value), Category = ModuleClassifier.Classify(name, null, null), MetadataSource = "Dump module list" };
            }
        }
        foreach (var frame in frames.Where(frame => !string.IsNullOrWhiteSpace(frame.Module)))
        {
            if (!identities.TryGetValue(frame.Module!, out var identity)) identities[frame.Module!] = identity = new() { ModuleName = frame.Module!, Category = frame.ModuleCategory, MetadataSource = "Dump stack" };
            if (identity.StackRelationship == StackRelationship.LoadedNotOnStack || frame.Relationship < identity.StackRelationship) identity.StackRelationship = frame.Relationship;
            identity.Evidence.Add($"Stack frame {frame.Index}: {frame.RawLine.Trim()}");
        }
        return identities.Values.OrderBy(identity => identity.StackRelationship).ThenBy(identity => identity.ModuleName).ToList();
    }

    public static SymbolDiagnostics ParseSymbols(string output, IReadOnlyList<StackFrame> frames)
    {
        var result = new SymbolDiagnostics();
        foreach (var line in output.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Contains("symbols could not be loaded", StringComparison.OrdinalIgnoreCase) || line.Contains("unable to load image", StringComparison.OrdinalIgnoreCase)) result.MissingSymbols.Add(line.Trim());
            if (line.Contains("wrong symbols", StringComparison.OrdinalIgnoreCase) || line.Contains("mismatched pdb", StringComparison.OrdinalIgnoreCase)) result.MismatchedPdbs.Add(line.Trim());
            if (line.Contains("deferred", StringComparison.OrdinalIgnoreCase) && line.Contains("symbol", StringComparison.OrdinalIgnoreCase)) result.DeferredModules.Add(line.Trim());
            if (line.Contains("export symbols", StringComparison.OrdinalIgnoreCase)) result.ExportOnlyModules.Add(line.Trim());
            if (line.Contains("timestamp", StringComparison.OrdinalIgnoreCase) && (line.Contains("verify", StringComparison.OrdinalIgnoreCase) || line.Contains("wrong", StringComparison.OrdinalIgnoreCase))) result.TimestampWarnings.Add(line.Trim());
            if (line.Contains("extension", StringComparison.OrdinalIgnoreCase) && (line.Contains("failed", StringComparison.OrdinalIgnoreCase) || line.Contains("not found", StringComparison.OrdinalIgnoreCase))) result.ExtensionFailures.Add(line.Trim());
            if ((line.Contains("page", StringComparison.OrdinalIgnoreCase) && (line.Contains("not present", StringComparison.OrdinalIgnoreCase) || line.Contains("not available", StringComparison.OrdinalIgnoreCase) || line.Contains("unable to read", StringComparison.OrdinalIgnoreCase))) || line.Contains("truncated dump", StringComparison.OrdinalIgnoreCase)) result.OtherWarnings.Add(line.Trim());
        }
        result.NtSymbolsLoaded = frames.Any(frame => frame.Module?.Equals("nt", StringComparison.OrdinalIgnoreCase) == true && frame.Resolved) && !result.MissingSymbols.Any(line => line.Contains(" nt", StringComparison.OrdinalIgnoreCase));
        var resolvedRatio = frames.Count == 0 ? 0 : frames.Count(frame => frame.Resolved) / (double)frames.Count;
        result.Quality = result.NtSymbolsLoaded && resolvedRatio >= .75 && result.MismatchedPdbs.Count == 0 && result.OtherWarnings.Count == 0 ? SymbolQuality.Good : result.NtSymbolsLoaded && resolvedRatio >= .35 ? SymbolQuality.Partial : frames.Count > 0 ? SymbolQuality.Poor : SymbolQuality.Unavailable;
        result.Explanation = result.Quality switch { SymbolQuality.Good => "Core symbols loaded and most stack frames resolved.", SymbolQuality.Partial => "Core symbols loaded, but some modules or frames were unresolved.", SymbolQuality.Poor => "Important symbols or stack frames were unresolved; module attribution is unreliable.", _ => "No usable symbol-backed stack was available." };
        return result;
    }

    private static void ParseAnalysisElapsed(AnalyzeData analyze)
    {
        var entry = analyze.KeyValues.FirstOrDefault(pair => pair.Key.Contains("Analysis.Elapsed", StringComparison.OrdinalIgnoreCase));
        if (entry.Value is not null && double.TryParse(entry.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var milliseconds)) analyze.AnalysisElapsed = TimeSpan.FromMilliseconds(milliseconds);
    }

    private static DateTimeOffset? ParseDumpHeaderTime(string output)
    {
        var match = DebugSessionTimeRegex().Match(output);
        if (!match.Success) return null;
        var dateText = Regex.Replace(match.Groups["date"].Value.Trim(), @"\s+", " ");
        var formats = new[] { "ddd MMM d HH:mm:ss.fff yyyy", "ddd MMM dd HH:mm:ss.fff yyyy", "ddd MMM d HH:mm:ss yyyy", "ddd MMM dd HH:mm:ss yyyy" };
        if (!DateTime.TryParseExact(dateText, formats, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out var parsed)
            && !DateTime.TryParse(dateText, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out parsed)) return null;
        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
        var offset = new TimeSpan(hours, minutes, 0);
        if (match.Groups["sign"].Value == "-") offset = -offset;
        return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), offset);
    }

    private static void ParseExceptionRecord(AnalyzeData analyze)
    {
        var section = analyze.RawSections.Where(pair => pair.Key.StartsWith(".exr ", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).LastOrDefault();
        if (string.IsNullOrWhiteSpace(section)) return;
        var address = ExceptionAddressRegex().Match(section);
        if (address.Success)
        {
            analyze.ExceptionAddress = FirstAddress(address.Groups[1].Value);
            if (address.Groups["symbol"].Success) analyze.ExceptionSymbol = address.Groups["symbol"].Value.Trim();
            analyze.FaultingIp ??= address.Groups[1].Value.Trim();
        }
        var code = ExceptionCodeLineRegex().Match(section);
        if (code.Success) analyze.ExceptionCode = NormalizeNumeric(code.Groups[1].Value);
        var parameters = ExceptionParameterLineRegex().Matches(section).Select(match => new { Index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), Value = NormalizeNumeric(match.Groups[2].Value) }).OrderBy(item => item.Index).ToList();
        if (parameters.Count > 0) { analyze.ExceptionParameters.Clear(); analyze.ExceptionParameters.AddRange(parameters.Select(item => item.Value)); }
    }
    private static bool LooksLikeDebuggerField(string key) => key.Length >= 3 && key.All(character => char.IsLetterOrDigit(character) || character is '_' or '.');
    private static string NormalizeNumeric(string value) { var match = AddressRegex().Match(value); if (!match.Success) return value.Trim(); var numeric = match.Value.Replace("`", string.Empty); if (numeric.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) numeric = numeric[2..]; return "0x" + numeric.TrimStart('0').PadLeft(1, '0'); }
    private static string? FirstAddress(string value) { var match = AddressRegex().Match(value); if (!match.Success) return null; var numeric = match.Value.Replace("`", string.Empty); if (numeric.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) numeric = numeric[2..]; return "0x" + numeric; }
    private static string CleanAddress(string value) => "0x" + value.Replace("`", string.Empty);
    private static string? ClassifyTransition(string? module, string? symbol)
    {
        var value = $"{module}!{symbol}";
        if (value.Contains("KiSystemService", StringComparison.OrdinalIgnoreCase)) return "System-call transition";
        if (value.Contains("KiException", StringComparison.OrdinalIgnoreCase) || value.Contains("RtlDispatchException", StringComparison.OrdinalIgnoreCase)) return "Exception dispatch";
        if (value.Contains("KiInterrupt", StringComparison.OrdinalIgnoreCase)) return "Interrupt handling";
        if (value.Contains("KiExecuteAllDpcs", StringComparison.OrdinalIgnoreCase)) return "DPC execution";
        if (value.Contains("ExpWorkerThread", StringComparison.OrdinalIgnoreCase)) return "Worker thread";
        if (value.Contains("Fltp", StringComparison.OrdinalIgnoreCase)) return "Filesystem minifilter callback";
        if (value.Contains("StorPort", StringComparison.OrdinalIgnoreCase)) return "Storport path";
        if (value.Contains("Hvl", StringComparison.OrdinalIgnoreCase) || value.Contains("Hv", StringComparison.OrdinalIgnoreCase)) return "Hypervisor transition";
        return null;
    }

    [GeneratedRegex(@"^\s*=== CEC COMMAND:\s*(.*?)\s*===\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex CommandMarkerRegex();
    [GeneratedRegex(@"^\s*([A-Za-z][A-Za-z0-9_.]+)\s*:\s*(.*)$", RegexOptions.CultureInvariant)] private static partial Regex FieldRegex();
    [GeneratedRegex(@"^\s*Arg([1-4])\s*:\s*([0-9a-fA-F`x]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex ArgumentRegex();
    [GeneratedRegex(@"^\s*Key\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex KeyLineRegex();
    [GeneratedRegex(@"^\s*Value\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex ValueLineRegex();
    [GeneratedRegex(@"^\s*([A-Za-z][A-Za-z0-9_.]+)\s*[:=]\s*(.+)$", RegexOptions.CultureInvariant)] private static partial Regex KeyValueRegex();
    [GeneratedRegex(@"(?:0x)?([0-9a-fA-F]{1,16}(?:`[0-9a-fA-F]{1,16})?)", RegexOptions.CultureInvariant)] private static partial Regex AddressRegex();
    [GeneratedRegex(@"^\s*(?:(?<index>[0-9a-fA-F]{1,3})\s+)?(?<sp>[0-9a-fA-F`]{8,})\s+(?<ret>[0-9a-fA-F`]{8,})(?:\s+.*:\s+|\s+)(?<target>[A-Za-z0-9_.-]+![^\s]+)", RegexOptions.CultureInvariant)] private static partial Regex StackRegex();
    [GeneratedRegex(@"\[(?<file>[^\]]+?)\s*@\s*(?<line>\d+)\]", RegexOptions.CultureInvariant)] private static partial Regex SourceRegex();
    [GeneratedRegex(@"^\s*(?<start>[0-9a-fA-F`]{8,})\s+(?<end>[0-9a-fA-F`]{8,})\s+(?<name>[A-Za-z0-9_.-]+)(?:\s|$)", RegexOptions.CultureInvariant)] private static partial Regex ModuleRegex();
    [GeneratedRegex(@"Debug session time:\s*(?<date>.+?)\s*\(UTC\s*(?<sign>[+-])\s*(?<hours>\d{1,2}):(?<minutes>\d{2})\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex DebugSessionTimeRegex();
    [GeneratedRegex(@"(?mi)^\s*ExceptionAddress:\s*([0-9a-fA-F`x]+)(?:\s+\((?<symbol>[^)]+)\))?")] private static partial Regex ExceptionAddressRegex();
    [GeneratedRegex(@"(?mi)^\s*ExceptionCode:\s*([0-9a-fA-F`x]+)")] private static partial Regex ExceptionCodeLineRegex();
    [GeneratedRegex(@"(?mi)^\s*Parameter\[(\d+)\]:\s*([0-9a-fA-F`x]+)")] private static partial Regex ExceptionParameterLineRegex();
}

public static class ModuleClassifier
{
    private static readonly HashSet<string> Core = new(StringComparer.OrdinalIgnoreCase) { "nt", "ntoskrnl", "hal", "fltmgr", "storport", "disk", "ntfs", "wdf01000", "dxgkrnl", "acpi", "tcpip", "win32k", "win32kbase", "win32kfull", "ci", "clfs" };
    private static readonly HashSet<string> MicrosoftSecurity = new(StringComparer.OrdinalIgnoreCase) { "wdfilter", "mssecflt", "luafv" };

    public static ModuleCategory Classify(string? module, string? company, string? description)
    {
        if (string.IsNullOrWhiteSpace(module)) return ModuleCategory.UnknownUnverifiable;
        var name = Path.GetFileNameWithoutExtension(module); var text = $"{name} {company} {description}";
        if (Core.Contains(name)) return ModuleCategory.MicrosoftKernelCore;
        if (MicrosoftSecurity.Contains(name)) return ModuleCategory.AntivirusSecurity;
        if (text.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.MicrosoftInboxDriver;
        if (name.Equals("nvlddmkm", StringComparison.OrdinalIgnoreCase) || name.StartsWith("amdkmd", StringComparison.OrdinalIgnoreCase) || name.StartsWith("igdkmd", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.GraphicsDriver;
        if (text.Contains("stor", StringComparison.OrdinalIgnoreCase) || text.Contains("nvme", StringComparison.OrdinalIgnoreCase) || text.Contains("sata", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.StorageDriver;
        if (text.Contains("filter", StringComparison.OrdinalIgnoreCase) || text.Contains("flt", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.FileSystemMinifilter;
        if (text.Contains("hyper", StringComparison.OrdinalIgnoreCase) || text.Contains("virtual", StringComparison.OrdinalIgnoreCase) || name.StartsWith("vmb", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.VirtualizationHypervisor;
        if (text.Contains("network", StringComparison.OrdinalIgnoreCase) || text.Contains("ndis", StringComparison.OrdinalIgnoreCase) || text.Contains("wifi", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.NetworkDriver;
        if (text.Contains("audio", StringComparison.OrdinalIgnoreCase) || text.Contains("sound", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.AudioDriver;
        if (text.Contains("rgb", StringComparison.OrdinalIgnoreCase) || text.Contains("overclock", StringComparison.OrdinalIgnoreCase) || text.Contains("monitor", StringComparison.OrdinalIgnoreCase)) return ModuleCategory.RgbMonitoringOverclocking;
        return ModuleCategory.ThirdPartyMiscellaneous;
    }

    public static bool IsMicrosoft(ModuleCategory category) => category is ModuleCategory.MicrosoftKernelCore or ModuleCategory.MicrosoftInboxDriver;
}
