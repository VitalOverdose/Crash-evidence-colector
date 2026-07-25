using CrashEvidenceCollector.Core;

if (args.Length > 0 && args[0].Equals("--dump-smoke-test", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2 || !File.Exists(args[1])) { Console.Error.WriteLine("Usage: --dump-smoke-test <dump-file> [output-folder]"); return 6; }
    var source = Path.GetFullPath(args[1]);
    var smokeRoot = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestRuns", "RealDebuggerSmoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
    var dumpFolder = Path.Combine(smokeRoot, "Raw", "Dumps"); var raw = Path.Combine(smokeRoot, "Raw");
    Directory.CreateDirectory(dumpFolder);
    var copied = Path.Combine(dumpFolder, Path.GetFileName(source));
    Console.WriteLine("REAL DEBUGGER SMOKE TEST — read-only source; analysis runs against a streamed copy");
    Console.WriteLine($"Source: {source}"); Console.WriteLine($"Output: {smokeRoot}");
    await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
    await using (var outputStream = new FileStream(copied, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await input.CopyToAsync(outputStream, 1024 * 1024);
    var smokeIncident = new Incident("real-debugger-smoke", new FileInfo(source).LastWriteTime, IncidentKind.BugCheck, "Real dump smoke test", "Local verification against a copied Windows dump");
    var smokeOptions = new CollectionOptions { AnalyzeCrashDumps = true, DebuggerTimeoutSeconds = 240, OutputRoot = smokeRoot };
    var analyses = await new DumpAnalysisService(new StructuredLog(Path.Combine(smokeRoot, "smoke.jsonl"))).AnalyzeDirectoryAsync(dumpFolder, raw, smokeIncident, new(), [], smokeOptions, null, CancellationToken.None);
    var analysis = analyses.Single();
    await File.WriteAllTextAsync(Path.Combine(smokeRoot, "analysis-summary.json"), System.Text.Json.JsonSerializer.Serialize(analysis, JsonDefaults.Indented));
    Console.WriteLine($"Completion: {analysis.Dump.Completion}; exit: {analysis.Dump.ExitCode}; bugcheck: {analysis.Analyze.BugCheckCode}; symbols: {analysis.Symbols.Quality}; quality: {analysis.Quality.Level} ({analysis.Quality.Score}/100)");
    Console.WriteLine($"Raw output: {analysis.Dump.RawOutputPath}");
    foreach (var error in analysis.Dump.Errors) Console.Error.WriteLine(error);
    return analysis.Dump.Completion is DebuggerCompletion.Completed or DebuggerCompletion.Partial ? 0 : 7;
}

if (args.Length > 0 && args[0].Equals("--helper-smoke-test", StringComparison.OrdinalIgnoreCase))
{
    var helper = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CrashEvidenceCollector.App", "bin", "Release", "net10.0-windows", "CrashEvidenceCollector.Helper.exe"));
    var destination = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestRuns", "ElevatedHelperSmokeTest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
    Directory.CreateDirectory(destination);
    Console.WriteLine("ELEVATED HELPER SMOKE TEST — copies minidumps only; does not change Windows configuration");
    var response = await ElevatedHelperIpc.RequestDumpCopyAsync(helper, destination, false, CancellationToken.None);
    Console.WriteLine($"Success: {response.Success}; copied: {response.CopiedFiles.Count}; errors: {response.Errors.Count}");
    foreach (var file in response.CopiedFiles) Console.WriteLine(file);
    foreach (var error in response.Errors) Console.Error.WriteLine(error);
    return response.Success && response.CopiedFiles.Count > 0 ? 0 : 5;
}

var root = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestData"));
var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestRuns"));
Console.WriteLine("TEST-DATA MODE — copied local fixtures only"); Console.WriteLine($"Input:  {root}"); Console.WriteLine($"Output: {output}");
if (!Directory.Exists(root)) { Console.Error.WriteLine("Test-data folder not found."); return 2; }
var log = new StructuredLog(Path.Combine(output, "harness.jsonl"));
var detector = new IncidentDetector(new EventLogReader(), log); var incidents = await detector.DetectAsync(DateTimeOffset.MinValue, root, CancellationToken.None);
var incident = incidents.FirstOrDefault(x => x.Kind is IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown or IncidentKind.BugCheck);
if (incident is null) { Console.Error.WriteLine("No shutdown-family incident was read from the copied logs."); return 3; }
Console.WriteLine($"Selected: {incident.Timestamp:O} {incident.Title}");
var options = new CollectionOptions { OutputRoot = output, TestDataDirectory = root, MinutesBefore = 10, MinutesAfterStartup = 5, IncludeFullMemoryDump = false, RedactAccountName = true };
var engine = new EvidenceCollectionEngine(new EventLogReader(), new SystemEvidenceCollector(), new FileEvidenceCollector(), new ReportWriter(), log);
var progress = new Progress<CollectionProgress>(value => Console.WriteLine($"{value.Percent,3}% {value.Category}: {value.Message}"));
var result = await engine.CollectAsync(incident, incidents, options, progress, CancellationToken.None);
Console.WriteLine($"HTML: {result.HtmlPath}"); Console.WriteLine($"JSON: {result.JsonPath}"); Console.WriteLine($"ZIP:  {result.ZipPath}"); Console.WriteLine($"Evidence categories: {result.Report.Evidence.Count}; preserved errors: {result.Report.Errors.Count}");
return File.Exists(result.ZipPath) && File.Exists(result.HtmlPath) && File.Exists(result.JsonPath) ? 0 : 4;
