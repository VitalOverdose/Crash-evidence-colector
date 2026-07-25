using System.Text;
using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed class DumpAnalysisService(StructuredLog log)
{
    public async Task<IReadOnlyList<DumpAnalysisResult>> AnalyzeDirectoryAsync(string dumpDirectory, string rawDirectory, Incident incident, MachineSnapshot machine, IReadOnlyList<EvidenceEvent> events, CollectionOptions options, IProgress<CollectionProgress>? progress, CancellationToken token)
    {
        if (!options.AnalyzeCrashDumps || !Directory.Exists(dumpDirectory)) return [];
        var dumps = Directory.EnumerateFiles(dumpDirectory, "*.dmp", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        var sourceManifest = LoadSourceManifest(dumpDirectory);
        var results = new List<DumpAnalysisResult>(); var parser = new DebuggerOutputParser(); var debugger = new DumpDebugger(log);
        for (var index = 0; index < dumps.Count; index++)
        {
            token.ThrowIfCancellationRequested(); var dumpPath = dumps[index];
            progress?.Report(new(79 + Math.Min(8, index * 8 / Math.Max(1, dumps.Count)), "Dump analysis", $"Analyzing {Path.GetFileName(dumpPath)} ({index + 1}/{dumps.Count})"));
            var manifestEntry = sourceManifest.GetValueOrDefault(Path.GetFullPath(dumpPath));
            var sourcePath = manifestEntry?.SourcePath
                ?? (options.IsTestDataMode ? Path.Combine(Path.GetFullPath(options.TestDataDirectory!), "Dumps", Path.GetFileName(dumpPath)) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), Path.GetFileName(dumpPath).Equals("MEMORY.DMP", StringComparison.OrdinalIgnoreCase) ? "MEMORY.DMP" : Path.Combine("Minidump", Path.GetFileName(dumpPath))));
            var totalBytes = new FileInfo(dumpPath).Length;
            long lastHashReport = 0;
            var hashProgress = new InlineProgress<long>(bytes =>
            {
                if (bytes != totalBytes && bytes - Interlocked.Read(ref lastHashReport) < 16L * 1024 * 1024) return;
                Interlocked.Exchange(ref lastHashReport, bytes);
                progress?.Report(new(80, "Dump analysis", $"Hashing {Path.GetFileName(dumpPath)} — {FormatBytes(bytes)} / {FormatBytes(totalBytes)}"));
            });
            var metadata = await DumpEvidenceFactory.CreateAsync(sourcePath, dumpPath, TimeSpan.FromSeconds(options.DebuggerTimeoutSeconds), token, hashProgress).ConfigureAwait(false);
            if (manifestEntry is not null)
            {
                if (manifestEntry.FileSize > 0) metadata.FileSize = manifestEntry.FileSize;
                if (manifestEntry.SourceCreationTime != default) metadata.CreationTime = manifestEntry.SourceCreationTime;
                if (manifestEntry.SourceModificationTime != default) metadata.ModificationTime = manifestEntry.SourceModificationTime;
            }
            var outputDirectory = Path.Combine(rawDirectory, "Debugger"); Directory.CreateDirectory(outputDirectory);
            var stdoutPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(dumpPath) + "-cdb.txt");
            var stderrPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(dumpPath) + "-cdb-errors.txt");
            metadata.RawOutputPath = stdoutPath; metadata.RawErrorPath = stderrPath;
            string stdout = string.Empty; string stderr = string.Empty; IReadOnlyList<string> unsupported = [];
            try
            {
                var fixture = options.IsTestDataMode ? FindDebuggerFixture(options.TestDataDirectory!, dumpPath) : null;
                if (fixture is not null)
                {
                    // Test-data mode consumes saved debugger output verbatim. It never
                    // manufactures production conclusions or invokes CDB on fake data.
                    metadata.AnalysisStarted = DateTimeOffset.Now; stdout = await File.ReadAllTextAsync(fixture, token).ConfigureAwait(false); metadata.AnalysisEnded = DateTimeOffset.Now; metadata.DebuggerPath = "TEST-DATA fixture"; metadata.DebuggerVersion = "Saved debugger output"; metadata.ExitCode = 0; metadata.Completion = DebuggerCompletion.Completed; metadata.SanitizedCommandLine = "[TEST-DATA MODE: saved debugger output]";
                }
                else
                {
                    var incidentCode = NumericParser.TryParse(incident.BugCheckCode, out var code) ? code : (ulong?)null;
                    var outputLines = 0; var errorLines = 0; long capturedCharacters = 0; var lastLine = "Starting CDB";
                    var commandProgress = new InlineProgress<CommandExecutionProgress>(value =>
                    {
                        Volatile.Write(ref outputLines, value.StandardOutputLines);
                        Volatile.Write(ref errorLines, value.StandardErrorLines);
                        Interlocked.Exchange(ref capturedCharacters, value.CapturedCharacters);
                        Volatile.Write(ref lastLine, value.LastLine);
                    });
                    var debuggerTask = debugger.ExecuteAsync(dumpPath, outputDirectory, incidentCode, incident.Parameters ?? [], metadata.CommandTimeout, token, commandProgress);
                    var heartbeat = System.Diagnostics.Stopwatch.StartNew();
                    while (!debuggerTask.IsCompleted)
                    {
                        var completed = await Task.WhenAny(debuggerTask, Task.Delay(TimeSpan.FromSeconds(1), token)).ConfigureAwait(false);
                        if (completed == debuggerTask) break;
                        var currentLine = Volatile.Read(ref lastLine);
                        if (currentLine.Length > 120) currentLine = currentLine[..120] + "…";
                        progress?.Report(new(82, "Dump analysis", $"{Path.GetFileName(dumpPath)} — CDB active {heartbeat.Elapsed:mm\\:ss}; {Volatile.Read(ref outputLines):N0} output lines, {Volatile.Read(ref errorLines):N0} error lines, {FormatBytes(Interlocked.Read(ref capturedCharacters))} captured. Last: {currentLine}"));
                    }
                    var execution = await debuggerTask.ConfigureAwait(false);
                    stdout = execution.Process.StandardOutput; stderr = execution.Process.StandardError; unsupported = execution.UnsupportedCommands;
                    metadata.AnalysisStarted = execution.Started; metadata.AnalysisEnded = execution.Ended; metadata.ExitCode = execution.Process.ExitCode; metadata.SanitizedCommandLine = execution.SanitizedCommandLine;
                    metadata.Completion = execution.Process.ExitCode == 0 && stdout.Contains("CEC ANALYSIS END", StringComparison.OrdinalIgnoreCase) ? DebuggerCompletion.Completed : execution.Process.ExitCode == -1 && stderr.Contains("timed out", StringComparison.OrdinalIgnoreCase) ? DebuggerCompletion.TimedOut : stdout.Contains("BUGCHECK_CODE", StringComparison.OrdinalIgnoreCase) || stdout.Contains("Bugcheck Analysis", StringComparison.OrdinalIgnoreCase) ? DebuggerCompletion.Partial : DebuggerCompletion.Failed;
                }
            }
            catch (OperationCanceledException) { metadata.Completion = DebuggerCompletion.Cancelled; throw; }
            catch (Exception ex) { metadata.AnalysisEnded = DateTimeOffset.Now; metadata.Completion = ex is FileNotFoundException ? DebuggerCompletion.Unsupported : DebuggerCompletion.Failed; metadata.Errors.Add(ex.Message); stderr = ex.ToString(); await log.WriteAsync("error", "Dump analysis failed", new { dump = Path.GetFileName(dumpPath), ex.Message }, token).ConfigureAwait(false); }

            await File.WriteAllTextAsync(stdoutPath, stdout, new UTF8Encoding(false), token).ConfigureAwait(false);
            await File.WriteAllTextAsync(stderrPath, stderr, new UTF8Encoding(false), token).ConfigureAwait(false);
            var analysis = parser.Parse(stdout, stderr, metadata); analysis.UnsupportedCommands.AddRange(unsupported);
            analysis.Symbols.SymbolPath = metadata.DebuggerPath == "TEST-DATA fixture" ? "TEST-DATA saved debugger output" : $"srv*{Path.Combine(AppPaths.StateDirectory, "Symbols")}*https://msdl.microsoft.com/download/symbols";
            metadata.DumpType = DumpClassifier.Refine(metadata.DumpType, stdout); metadata.Architecture = DumpClassifier.DetectArchitecture(stdout);
            if (string.IsNullOrWhiteSpace(analysis.Analyze.BugCheckCode)) analysis.Analyze.BugCheckCode = incident.BugCheckCode;
            if (analysis.Analyze.BugCheckParameters.Count == 0 && incident.Parameters is not null) analysis.Analyze.BugCheckParameters.AddRange(incident.Parameters);
            if (NumericParser.TryParse(analysis.Analyze.BugCheckCode, out var parsedCode)) analysis.DecodedParameters.AddRange(BugCheckKnowledge.DecodeParameters(parsedCode, analysis.Analyze.BugCheckParameters));
            analysis.FamilyAnalysis = BugCheckFamilyAnalyzer.Analyze(analysis);
            EnrichModuleMetadata(analysis.Modules, rawDirectory);
            analysis.Quality = AnalysisQualityScorer.Score(analysis);
            analysis.Assessment = CulpritAssessmentEngine.Assess(analysis);
            analysis.Fingerprint = CrossIncidentEngine.CreateFingerprint(incident.Id, analysis);
            analysis.Recommendations.AddRange(RecommendationEngine.Build(analysis, machine, events));
            results.Add(analysis);
        }
        return results;
    }

    private static string? FindDebuggerFixture(string testDataDirectory, string dumpPath)
    {
        var folder = Path.Combine(Path.GetFullPath(testDataDirectory), "Debugger"); if (!Directory.Exists(folder)) return null;
        var exact = Path.Combine(folder, Path.GetFileNameWithoutExtension(dumpPath) + ".txt"); if (File.Exists(exact)) return exact;
        var fallback = Path.Combine(folder, "default.txt"); return File.Exists(fallback) ? fallback : null;
    }

    private static IReadOnlyDictionary<string, DumpCopyManifestEntry> LoadSourceManifest(string dumpDirectory)
    {
        var path = Path.Combine(dumpDirectory, "dump-manifest.json");
        if (!File.Exists(path)) return new Dictionary<string, DumpCopyManifestEntry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var entries = JsonSerializer.Deserialize<List<DumpCopyManifestEntry>>(File.ReadAllText(path), JsonDefaults.Indented) ?? [];
            return entries.Where(entry => !string.IsNullOrWhiteSpace(entry.CopiedPath) && !string.IsNullOrWhiteSpace(entry.SourcePath))
                .GroupBy(entry => Path.GetFullPath(entry.CopiedPath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch { return new Dictionary<string, DumpCopyManifestEntry>(StringComparer.OrdinalIgnoreCase); }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"]; var value = (double)bytes; var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.0} {units[unit]}";
    }

    private static void EnrichModuleMetadata(List<DriverIdentity> modules, string rawDirectory)
    {
        var metadataPath = Path.Combine(rawDirectory, "driver-metadata.json");
        if (File.Exists(metadataPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
                var entries = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().ToList() : [document.RootElement];
                foreach (var module in modules)
                {
                    var match = entries.FirstOrDefault(entry => Get(entry, "DriverName").Contains(module.ModuleName, StringComparison.OrdinalIgnoreCase) || Path.GetFileNameWithoutExtension(Get(entry, "DriverName")).Equals(Path.GetFileNameWithoutExtension(module.ModuleName), StringComparison.OrdinalIgnoreCase));
                    if (match.ValueKind == JsonValueKind.Undefined) continue;
                    module.CompanyName = Get(match, "Manufacturer"); module.FileVersion = Get(match, "DriverVersion"); module.Signer = Get(match, "DriverProviderName"); module.IsMicrosoftSigned = Get(match, "IsSigned").Equals("true", StringComparison.OrdinalIgnoreCase) && (module.CompanyName?.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) == true || module.Signer?.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) == true); module.PnpDevice = Get(match, "DeviceName"); module.ImagePath = Get(match, "DriverName"); module.MetadataSource = "Dump and live signed-driver inventory"; module.Category = ModuleClassifier.Classify(module.ModuleName, module.CompanyName, module.FileDescription);
                }
            }
            catch (Exception) { }
        }
        var filtersPath = Path.Combine(rawDirectory, "minifilters.txt");
        if (File.Exists(filtersPath))
        {
            foreach (var module in modules)
            {
                var line = File.ReadLines(filtersPath).FirstOrDefault(value => value.Contains(module.ModuleName, StringComparison.OrdinalIgnoreCase)); if (line is null) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); module.MinifilterAltitude = parts.FirstOrDefault(part => part.All(char.IsDigit) && part.Length >= 4); module.Category = module.ModuleName.Equals("WdFilter", StringComparison.OrdinalIgnoreCase) ? ModuleCategory.AntivirusSecurity : ModuleCategory.FileSystemMinifilter; module.Evidence.Add("Present in live minifilter inventory: " + line.Trim());
            }
        }
        static string Get(JsonElement element, string name) => element.TryGetProperty(name, out var property) ? property.ToString() : string.Empty;
    }
}
