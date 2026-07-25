using CrashEvidenceCollector.Core;
using System.Diagnostics;
using System.Text.Json;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Timestamp correlation identifies nearby WHEA", TimestampCorrelation),
    ("Incident detection correlates duplicate shutdown records", IncidentDetection),
    ("Redaction removes account and profile variants", Redaction),
    ("HTML report generation encodes untrusted evidence", ReportGeneration),
    ("Bugcheck, status and event codes receive plain-language decoding", CodeDecoding),
    ("WER noise is rejected and duplicate app reports are collapsed", WerFiltering),
    ("Saved debugger fixture is parsed without losing fields", DebuggerFixtureParsing),
    ("0x1AA stack-limit and record parameters are decoded", InvalidStackDecoding),
    ("Command planning is deterministic and guards addresses", CommandPlanning),
    ("Small dumps avoid commands that require full memory", SmallDumpCommands),
    ("Poor symbols cap driver attribution confidence", SymbolConfidenceCap),
    ("Third-party faulting instructions produce evidence-backed candidates", ThirdPartyCulprit),
    ("Incomplete debugger output remains reportable", IncompleteOutput),
    ("Typed numeric namespaces do not guess arbitrary hex", TypedDecoders),
    ("Post-reboot service events are separated from crash evidence", PostBootTimeline),
    ("Repeated fingerprints are described as correlations", CrossIncidentComparison),
    ("SMART warnings trigger data protection without causal attribution", SmartHandling),
    ("Command timeout kills the process and preserves a timeout result", CommandTimeout),
    ("User cancellation terminates command execution", CommandCancellation),
    ("V2 report contains analysis, confidence and recommendation sections", V2ReportSections),
    ("Parser tolerates mixed case, spacing and unknown bugchecks", ParserVariations),
    ("Access violations distinguish read, write and execute", AccessOperations),
    ("Microsoft core and active process remain context, not culprit proof", MicrosoftCoreHandling),
    ("0x9F analyzer identifies a blocked IRP owner", PowerFamilyAnalysis),
    ("0x116 analyzer identifies display-driver involvement", GraphicsFamilyAnalysis),
    ("0x124 analyzer preserves WHEA error-record classification", WheaFamilyAnalysis),
    ("Null storage reliability counters are treated as unavailable", NullStorageCounters),
    ("Incident matching rejects a higher-quality dump with the wrong bugcheck", DumpIncidentAssociation)
    ,("Crash detection uses Event 6008 shutdown time instead of post-boot WER", TrueCrashTimeDetection)
    ,("Crash-time selection rejects post-boot dump and report timestamps", CrashTimeSelection)
    ,("Timeline uses separate crash and boot anchors", DualAnchorTimeline)
    ,("0x1AA separates failure location, process context and cause", InvalidStackConclusion)
};
var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS  {test.Name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL  {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static Task TimestampCorrelation()
{
    var time = DateTimeOffset.Parse("2026-07-13T20:00:00Z");
    var selected = new Incident("a", time, IncidentKind.BugCheck, "Bugcheck", "test", "0xA");
    var whea = new EvidenceEvent(time.AddMinutes(-2), "System", "Microsoft-Windows-WHEA-Logger", 18, "2", "hardware");
    var observations = CorrelationEngine.Analyze(selected, [selected], [whea]);
    Assert(observations.Any(x => x.Title.Contains("WHEA", StringComparison.Ordinal)), "Nearby WHEA was not correlated.");
    Assert(CorrelationEngine.Analyze(selected, [selected], [whea with { Timestamp = time.AddHours(-1) }]).All(x => !x.Title.Contains("WHEA", StringComparison.Ordinal)), "Distant WHEA was incorrectly correlated.");
    return Task.CompletedTask;
}

static async Task IncidentDetection()
{
    var folder = Path.Combine(Path.GetTempPath(), "CecTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    try
    {
        var xml = EventXml("Microsoft-Windows-Kernel-Power", 41, "System", "2026-07-13T20:00:00Z", "11") + EventXml("EventLog", 6008, "System", "2026-07-13T20:00:30Z", "12");
        await File.WriteAllTextAsync(Path.Combine(folder, "System.xml"), xml); await File.WriteAllTextAsync(Path.Combine(folder, "Application.xml"), string.Empty);
        var detector = new IncidentDetector(new EventLogReader(), new StructuredLog(Path.Combine(folder, "test.log")));
        var incidents = await detector.DetectAsync(DateTimeOffset.MinValue, folder, CancellationToken.None);
        Assert(incidents.Count == 1, $"Expected correlated incident count 1, got {incidents.Count}."); Assert(incidents[0].Kind == IncidentKind.PowerLossOrFreeze, "The more diagnostic Kernel-Power incident was not retained.");
    }
    finally { Directory.Delete(folder, true); }
}

static Task Redaction()
{
    var redactor = new Redactor("Alice", @"C:\Users\Alice"); var value = redactor.Redact(@"Alice opened C:\Users\Alice\secret.txt and C:/Users/Alice/other.txt");
    Assert(!value.Contains("Alice", StringComparison.OrdinalIgnoreCase), "Account name remained."); Assert(value.Contains("[REDACTED-USER]", StringComparison.Ordinal), "Redaction marker missing."); return Task.CompletedTask;
}

static Task ReportGeneration()
{
    var report = new EvidenceReport { Incident = new("x", DateTimeOffset.Now, IncidentKind.ApplicationCrash, "<script>alert(1)</script>", "unsafe & text") };
    report.Errors.Add("<b>not markup</b>"); var html = ReportWriter.BuildHtml(report);
    Assert(!html.Contains("<script>alert(1)</script>", StringComparison.Ordinal), "Incident text was not HTML encoded."); Assert(html.Contains("&lt;script&gt;", StringComparison.Ordinal), "Encoded incident text missing."); Assert(html.Contains("&lt;b&gt;not markup&lt;/b&gt;", StringComparison.Ordinal), "Errors were not encoded."); return Task.CompletedTask;
}

static Task CodeDecoding()
{
    var incident = new Incident("decode", DateTimeOffset.Now, IncidentKind.BugCheck, "Bugcheck", "test", "292", ["0xC0000005"]);
    var error = new EvidenceEvent(DateTimeOffset.Now, "Application", "Application Error", 1000, "2", "ExceptionCode=0xC0000005");
    Assert(CodeDecoder.GetBugCheckLabel(incident.BugCheckCode).Contains("WHEA_UNCORRECTABLE_ERROR", StringComparison.Ordinal), "Decimal bugcheck 292 was not decoded as 0x124.");
    var decoded = CodeDecoder.Interpret(incident, [error]);
    Assert(decoded.Any(x => x.Name == "WHEA_UNCORRECTABLE_ERROR"), "Bugcheck symbolic name missing.");
    Assert(decoded.Any(x => x.Name == "STATUS_ACCESS_VIOLATION"), "NTSTATUS symbolic name missing.");
    Assert(decoded.Any(x => x.Category == "Windows event"), "Event explanation missing.");
    var combined = new EvidenceEvent(DateTimeOffset.Now, "System", "Microsoft-Windows-WER-SystemErrorReporting", 1001, "2", "Bugcheck", Data: new Dictionary<string, string> { ["param1"] = "0x00020001 (0x11, 0x3a8502, 0x1005, 0xffffe70001005ee0)", ["param2"] = @"C:\Windows\MEMORY.DMP" });
    var parsed = IncidentDetector.ToIncident(combined);
    Assert(parsed?.BugCheckCode == "0x00020001", $"Combined WER bugcheck code parsed as '{parsed?.BugCheckCode}'.");
    Assert(parsed?.Parameters?.Count == 4 && parsed.Parameters[0] == "0x11", "Combined WER bugcheck parameters were not extracted.");
    Assert(parsed?.Title.Contains("HYPERVISOR_ERROR", StringComparison.Ordinal) == true, "Hypervisor bugcheck was not decoded in the incident title.");
    return Task.CompletedTask;
}

static async Task WerFiltering()
{
    var noise = new EvidenceEvent(DateTimeOffset.Now, "Application", "Windows Error Reporting", 1001, "4", "EventName=WindowsUpdateFailure", Data: new Dictionary<string, string> { ["EventName"] = "WindowsUpdateFailure" });
    Assert(IncidentDetector.ToIncident(noise) is null, "A generic WER 1001 status record was incorrectly treated as a crash.");
    var realCrash = noise with { Message = "EventName=APPCRASH; P1=sample.exe", Data = new Dictionary<string, string> { ["EventName"] = "APPCRASH", ["P1"] = "sample.exe" } };
    Assert(IncidentDetector.ToIncident(realCrash)?.Kind == IncidentKind.ApplicationCrash, "An explicit APPCRASH WER record was not detected.");
    var blueScreenReport = noise with { Message = "EventName=BlueScreen; P1=20001", Data = new Dictionary<string, string> { ["EventName"] = "BlueScreen", ["P1"] = "20001" } };
    Assert(IncidentDetector.ToIncident(blueScreenReport) is null, "A secondary Application-log BlueScreen report was incorrectly promoted to an incident.");

    var folder = Path.Combine(Path.GetTempPath(), "CecWerTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    try
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "System.xml"), string.Empty);
        var first = "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='Application Error'/><EventID>1000</EventID><Level>2</Level><TimeCreated SystemTime='2026-07-23T13:41:00Z'/><EventRecordID>1</EventRecordID><Channel>Application</Channel></System><EventData><Data Name='ApplicationName'>sample.exe</Data><Data Name='FaultingModule'>sample.dll</Data></EventData></Event>";
        var second = first.Replace("13:41:00", "13:41:20").Replace("<EventRecordID>1", "<EventRecordID>2");
        await File.WriteAllTextAsync(Path.Combine(folder, "Application.xml"), first + second);
        var detector = new IncidentDetector(new EventLogReader(), new StructuredLog(Path.Combine(folder, "test.log")));
        var incidents = await detector.DetectAsync(DateTimeOffset.MinValue, folder, CancellationToken.None);
        Assert(incidents.Count == 1, $"Expected one correlated application failure, got {incidents.Count}.");
    }
    finally { Directory.Delete(folder, true); }
}

static async Task DebuggerFixtureParsing()
{
    var analysis = await ParseFixture("invalid-stack-1aa.txt");
    Assert(analysis.Analyze.BugCheckCode?.Equals("0x1aa", StringComparison.OrdinalIgnoreCase) == true, "BUGCHECK_CODE was not parsed.");
    Assert(analysis.Analyze.BugCheckParameters.Count == 4, "All four parameters were not retained.");
    Assert(analysis.Analyze.UnknownFields.ContainsKey("UNKNOWN_FUTURE_FIELD"), "Unknown debugger fields were discarded.");
    Assert(analysis.Analyze.AnalysisElapsed == TimeSpan.FromMilliseconds(1264), "KEY_VALUES analysis duration was not parsed.");
    Assert(analysis.Dump.DumpHeaderTime == DateTimeOffset.Parse("2026-07-25T22:25:36+01:00"), "Dump-header crash time was not parsed.");
    Assert(analysis.Analyze.ExceptionAddress == "0xfffff80000102030" && analysis.Analyze.ExceptionSymbol == "nt!RtlpGetStackLimitsEx+0x30" && analysis.Analyze.ExceptionCode == "0xc0000005", "Reconstructed exception record was not parsed.");
    Assert(analysis.Analyze.RawSections.ContainsKey("r"), "Reconstructed register context was not preserved.");
    Assert(analysis.StackFrames.Count >= 2 && analysis.StackFrames[0].Module == "sampleflt", "Stack frames were not parsed from saved output.");
    Assert(analysis.Modules.Any(module => module.ModuleName == "sampleflt"), "Module table was not parsed.");
}

static Task InvalidStackDecoding()
{
    var definition = BugCheckKnowledge.Find(0x1AA);
    Assert(definition?.Name == "EXCEPTION_ON_INVALID_STACK", "0x1AA definition missing.");
    var decoded = BugCheckKnowledge.DecodeParameters(0x1AA, ["ffff8a0012345000", "7", "ffff8a0012346000", "ffff8a0012347000"]);
    Assert(decoded[1].SymbolicValue == "Kernel debugger stack", "0x1AA stack-limit type 7 was not decoded.");
    Assert(decoded[2].Semantic == ParameterSemantic.ContextRecord && decoded[3].Semantic == ParameterSemantic.ExceptionRecord, "Context/exception record semantics are incorrect.");
    return Task.CompletedTask;
}

static Task CommandPlanning()
{
    var first = DebuggerCommandPlanner.Plan(DumpType.KernelMemory, 0x124, ["0", "ffff800012340000", "0", "0"]);
    var second = DebuggerCommandPlanner.Plan(DumpType.KernelMemory, 0x124, ["0", "ffff800012340000", "0", "0"]);
    Assert(first.Select(item => item.Command).SequenceEqual(second.Select(item => item.Command)), "Command order is not deterministic.");
    Assert(first.Any(item => item.Command.StartsWith("!errrec 0xFFFF800012340000", StringComparison.OrdinalIgnoreCase)), "Canonical !errrec address was not planned.");
    var guarded = DebuggerCommandPlanner.Plan(DumpType.KernelMemory, 0x124, ["0", "1234", "0", "0"]);
    Assert(guarded.All(item => !item.Command.StartsWith("!errrec ", StringComparison.OrdinalIgnoreCase)), "Non-canonical address entered a debugger command.");
    Assert(first[0].Command == ".symfix+" && first[1].Command == ".reload /f", "Symbol setup is not the first deterministic debugger phase.");
    return Task.CompletedTask;
}

static Task SmallDumpCommands()
{
    var commands = DebuggerCommandPlanner.Plan(DumpType.SmallMemory, 0x9F, ["3", "ffff800011110000", "ffff800022220000", "ffff800033330000"]);
    Assert(commands.All(item => item.Command != "!stacks 2"), "A small dump was given the full-memory !stacks command.");
    Assert(commands.Any(item => item.Command.StartsWith("!irp ", StringComparison.OrdinalIgnoreCase)), "0x9F blocked-IRP command was omitted.");
    return Task.CompletedTask;
}

static async Task SymbolConfidenceCap()
{
    var analysis = await ParseFixture("poor-symbols.txt");
    analysis.Assessment = CulpritAssessmentEngine.Assess(analysis);
    var driver = analysis.Assessment.Candidates.First(candidate => candidate.Entity.Equals("mystery", StringComparison.OrdinalIgnoreCase));
    Assert(analysis.Symbols.Quality is SymbolQuality.Poor or SymbolQuality.Unavailable, "Fixture unexpectedly has usable symbols.");
    Assert(driver.Confidence == ConfidenceLevel.Low, $"Poor symbols did not cap confidence; got {driver.Confidence}.");
}

static async Task ThirdPartyCulprit()
{
    var analysis = await ParseFixture("third-party-fault.txt");
    analysis.Assessment = CulpritAssessmentEngine.Assess(analysis);
    var driver = analysis.Assessment.Candidates.First(candidate => candidate.Entity.Equals("vendorx", StringComparison.OrdinalIgnoreCase));
    Assert(driver.Confidence is ConfidenceLevel.High or ConfidenceLevel.Moderate, "A coherent third-party fault instruction did not produce a meaningful candidate.");
    Assert(driver.SupportingEvidence.Any(item => item.SourceReference.Contains("STACK", StringComparison.OrdinalIgnoreCase) || item.Description.Contains("frame", StringComparison.OrdinalIgnoreCase)), "Candidate lacks stack evidence.");
}

static async Task IncompleteOutput()
{
    var analysis = await ParseFixture("incomplete-output.txt");
    analysis.Dump.Completion = DebuggerCompletion.Partial;
    analysis.Quality = AnalysisQualityScorer.Score(analysis);
    Assert(analysis.Analyze.BugCheckCode == "0x50", "Useful bugcheck data was lost from incomplete output.");
    Assert(analysis.Analyze.Warnings.Count > 0, "Debugger read error was not preserved.");
    Assert(analysis.Quality.Level is AnalysisQualityLevel.Poor or AnalysisQualityLevel.Inconclusive, "Incomplete output was overstated.");
}

static Task TypedDecoders()
{
    Assert(TypedCodeDecoders.DecodeNtStatus(0xC0000005)?.Name == "STATUS_ACCESS_VIOLATION", "NTSTATUS decode failed.");
    var hresult = TypedCodeDecoders.DecodeHResult(0x80070005);
    Assert(hresult.Name == "HRESULT_FROM_WIN32(5)" && hresult.Explanation.Contains("denied", StringComparison.OrdinalIgnoreCase), "HRESULT decode failed.");
    Assert(TypedCodeDecoders.DecodeCmProblem(10).Name == "CM_PROB 10", "CM_PROB decode failed.");
    Assert(TypedCodeDecoders.DecodeNtStatus(0xDEADBEEF) is null, "Unknown arbitrary hex was guessed as an NTSTATUS.");
    var operation = TypedCodeDecoders.DecodeAccessViolationOperation(["1", "10"], out var target);
    Assert(operation.Contains("write", StringComparison.OrdinalIgnoreCase) && target == "0x10", "Access-violation operation was not decoded.");
    return Task.CompletedTask;
}

static Task PostBootTimeline()
{
    var incident = new Incident("timeline", DateTimeOffset.Parse("2026-07-24T10:00:00Z"), IncidentKind.BugCheck, "Bugcheck", "test", "0xD1");
    var events = new[]
    {
        new EvidenceEvent(incident.Timestamp.AddMinutes(-2), "System", "disk", 153, "2", "retry"),
        new EvidenceEvent(incident.Timestamp, "System", "Microsoft-Windows-WER-SystemErrorReporting", 1001, "2", "bugcheck"),
        new EvidenceEvent(incident.Timestamp.AddMinutes(1), "System", "EventLog", 6005, "4", "started"),
        new EvidenceEvent(incident.Timestamp.AddMinutes(2), "System", "Service Control Manager", 7000, "2", "service failed")
    };
    var timeline = EventTimelineEngine.Build(incident, events);
    Assert(timeline.Single(item => item.Source.Contains("/7000", StringComparison.Ordinal)).Phase == TimelinePhase.AfterReboot, "Post-reboot service noise was not separated.");
    Assert(timeline.Single(item => item.Source.Contains("/7000", StringComparison.Ordinal)).Explanation.Contains("not presented as a cause", StringComparison.OrdinalIgnoreCase), "Post-boot causality warning missing.");
    return Task.CompletedTask;
}

static async Task CrossIncidentComparison()
{
    var analysis = await ParseFixture("third-party-fault.txt");
    var current = CrossIncidentEngine.CreateFingerprint("current", analysis);
    var previous = current with { IncidentId = "previous", Hash = "different" };
    var findings = CrossIncidentEngine.Compare(current, [previous]);
    Assert(findings.Any(item => item.Title.Contains("Repeated bugcheck", StringComparison.Ordinal)), "Repeated fingerprint was not correlated.");
    Assert(findings.All(item => item.Origin == EvidenceOrigin.PossibleCorrelation), "A historical match was incorrectly labelled direct proof.");
}

static Task SmartHandling()
{
    var bytes = new byte[40];
    bytes[2] = 5; bytes[5] = 100; bytes[6] = 99; bytes[7] = 2;
    var smartJson = JsonSerializer.Serialize(new { InstanceName = "SampleDisk", Active = true, VendorSpecific = bytes.Select(value => (int)value).ToArray() });
    var devices = StorageHealthAnalyzer.Parse("""{"DeviceId":"0","FriendlyName":"SampleDisk","HealthStatus":"Healthy","OperationalStatus":"OK"}""", smartJson);
    Assert(devices.SelectMany(device => device.SmartAttributes).Any(attribute => attribute.Id == 5 && attribute.IsConcern), "Non-zero reallocated-sector data was not surfaced.");
    var analysis = new DumpAnalysisResult { Quality = new(AnalysisQualityLevel.Inconclusive, 0, "No dump", [], []) };
    var recommendations = RecommendationEngine.Build(analysis, new(), [], devices);
    Assert(recommendations.Any(item => item.Purpose == RecommendationPurpose.DataProtection), "SMART concern did not produce a backup-first recommendation.");
    Assert(analysis.Assessment.Candidates.All(item => item.Kind != CandidateKind.StorageCorruption), "SMART warning was incorrectly promoted to a crash cause.");
    return Task.CompletedTask;
}

static async Task CommandTimeout()
{
    var stopwatch = Stopwatch.StartNew();
    var result = await CommandRunner.RunAsync("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Write-Output before-timeout; Start-Sleep -Seconds 5"], TimeSpan.FromMilliseconds(200), CancellationToken.None);
    stopwatch.Stop();
    Assert(result.ExitCode == -1 && result.StandardError.Contains("timed out", StringComparison.OrdinalIgnoreCase), "Timeout did not return the documented result.");
    Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(4), "Timed-out command tree was not terminated promptly.");
}

static async Task CommandCancellation()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await CommandRunner.RunAsync("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 5"], TimeSpan.FromSeconds(10), cts.Token);
        throw new InvalidOperationException("Cancellation did not propagate.");
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(4), "Cancelled command tree was not terminated promptly.");
}

static async Task V2ReportSections()
{
    var analysis = await ParseFixture("invalid-stack-1aa.txt");
    analysis.Quality = AnalysisQualityScorer.Score(analysis);
    analysis.Assessment = CulpritAssessmentEngine.Assess(analysis);
    var report = new EvidenceReport
    {
        Incident = new("report-v2", DateTimeOffset.Now, IncidentKind.BugCheck, "Bugcheck 0x1AA", "Invalid stack", "0x1AA"),
        DumpAnalyses = [analysis],
        OverallAssessment = analysis.Assessment,
        AnalysisQuality = analysis.Quality,
        Recommendations = [new("Preserve the dump", "Supports comparison.", ["SHA-256"], RecommendationUrgency.Routine, RecommendationRisk.Low, true, "Retains evidence.", RecommendationPurpose.FurtherCollection)]
    };
    var html = ReportWriter.BuildHtml(report);
    Assert(html.Contains("A. Headline", StringComparison.Ordinal) && html.Contains("J. Confidence and limitations", StringComparison.Ordinal) && html.Contains("K. Recommendations", StringComparison.Ordinal), "Required V2 report sections are missing.");
    Assert(html.Contains("sampleflt", StringComparison.OrdinalIgnoreCase), "Parsed dump evidence is absent from the report.");
    var text = ReportWriter.BuildPlainText(report);
    Assert(text.Contains("CRASH EVIDENCE REPORT", StringComparison.Ordinal) && text.Contains("PRIMARY DUMP", StringComparison.Ordinal) && text.Contains("RECOMMENDATIONS", StringComparison.Ordinal), "Readable text report sections are missing.");
}

static Task ParserVariations()
{
    const string output = """
        === CEC COMMAND: !analyze -v ===
          bugcheck_code   :  DEaD
        BUGCHECK_P1: 1
        Future_Field : preserved
        PROCESS_NAME    : sample.exe
        === CEC ANALYSIS END ===
        """;
    var analysis = new DebuggerOutputParser().Parse(output, string.Empty, new());
    Assert(analysis.Analyze.BugCheckCode == "0xDEaD", "Mixed-case code or changed spacing was not tolerated.");
    Assert(analysis.Analyze.UnknownFields.ContainsKey("FUTURE_FIELD"), "Unknown field was not preserved.");
    Assert(BugCheckKnowledge.Find(0xDEAD) is null, "Test code unexpectedly exists in local definitions.");
    var decoded = BugCheckKnowledge.DecodeParameters(0xDEAD, ["1"]);
    Assert(decoded[0].Semantic == ParameterSemantic.Unknown && decoded[0].RawValue == "1", "Unknown bugcheck parameters were guessed.");
    return Task.CompletedTask;
}

static Task AccessOperations()
{
    Assert(TypedCodeDecoders.DecodeAccessViolationOperation(["0", "10000"], out _) == "Attempted read", "Read operation decode failed.");
    Assert(TypedCodeDecoders.DecodeAccessViolationOperation(["1", "10000"], out _) == "Attempted write", "Write operation decode failed.");
    Assert(TypedCodeDecoders.DecodeAccessViolationOperation(["8", "10000"], out _) == "Attempted execution", "Execute operation decode failed.");
    return Task.CompletedTask;
}

static async Task MicrosoftCoreHandling()
{
    var analysis = await ParseFixture("microsoft-core-thirdparty.txt");
    analysis.Assessment = CulpritAssessmentEngine.Assess(analysis);
    var core = analysis.Assessment.Candidates.First(candidate => candidate.Entity.Equals("nt", StringComparison.OrdinalIgnoreCase));
    var process = analysis.Assessment.Candidates.First(candidate => candidate.Kind == CandidateKind.ApplicationContext);
    Assert(core.Confidence is ConfidenceLevel.Low or ConfidenceLevel.InsufficientEvidence && core.CounterEvidence.Any(), "Microsoft core image was overstated as the origin.");
    Assert(process.Confidence == ConfidenceLevel.InsufficientEvidence && process.CounterEvidence.Any(item => item.Contains("context", StringComparison.OrdinalIgnoreCase)), "Active process was treated as causal.");
    Assert(analysis.Assessment.Candidates.Any(candidate => candidate.Entity.Equals("vendorfilter", StringComparison.OrdinalIgnoreCase)), "Third-party frame beneath Microsoft core was not retained as a lead.");
}

static async Task PowerFamilyAnalysis()
{
    var analysis = await ParseAndPrepareFamily("power-9f.txt");
    Assert(analysis.FamilyAnalysis.Family.Contains("Power", StringComparison.Ordinal), "Power-family analyzer was not selected.");
    Assert(analysis.FamilyAnalysis.CandidateEvidence.Any(item => item.Entity.Equals("vendorpower", StringComparison.OrdinalIgnoreCase)), "Blocked IRP owner was not extracted.");
}

static async Task GraphicsFamilyAnalysis()
{
    var analysis = await ParseAndPrepareFamily("graphics-116.txt");
    Assert(analysis.FamilyAnalysis.CandidateEvidence.Any(item => item.Entity.Equals("nvlddmkm", StringComparison.OrdinalIgnoreCase)), "Display miniport was not identified in the TDR stack.");
    Assert(analysis.DecodedParameters[2].SymbolicValue == "STATUS_INSUFFICIENT_RESOURCES", "TDR NTSTATUS parameter was not decoded.");
}

static async Task WheaFamilyAnalysis()
{
    var analysis = await ParseAndPrepareFamily("whea-124.txt");
    Assert(analysis.FamilyAnalysis.Facts.Any(item => item.Contains("Cache Hierarchy", StringComparison.OrdinalIgnoreCase)), "WHEA error type was not retained.");
    Assert(analysis.FamilyAnalysis.CandidateEvidence.Any(item => item.Entity.Contains("cache", StringComparison.OrdinalIgnoreCase)), "WHEA source class was not identified.");
    Assert(analysis.FamilyAnalysis.Limitations.All(item => !item.Contains("not readable", StringComparison.OrdinalIgnoreCase)), "!errrec section was incorrectly treated as missing.");
}

static Task NullStorageCounters()
{
    const string reliability = """
        {"DeviceId":"0","FriendlyName":"NVMe","MediaType":"SSD","HealthStatus":"Healthy","OperationalStatus":"OK","Temperature":null,"Wear":null,"PowerOnHours":null,"ReadErrorsTotal":null,"WriteErrorsTotal":null}
        """;
    var devices = StorageHealthAnalyzer.Parse(reliability, """{"InstanceName":"ATA","Active":true,"VendorSpecific":[1,0,null,0]}""");
    var device = devices.First(item => item.Device == "NVMe");
    Assert(device.TemperatureCelsius is null && device.WearPercent is null && device.PowerOnHours is null, "Null counters were not preserved as unavailable.");
    return Task.CompletedTask;
}

static Task DumpIncidentAssociation()
{
    var timestamp = DateTimeOffset.Parse("2026-07-25T21:39:32Z");
    var incident = new Incident("selected", timestamp, IncidentKind.BugCheck, "Bugcheck 0x1AA", "selected", "0x1AA");
    var wrong = new DumpAnalysisResult
    {
        Dump = new() { CopiedPath = "older-hypervisor.dmp", ModificationTime = timestamp, Completion = DebuggerCompletion.Completed },
        Analyze = new() { BugCheckCode = "0x20001", ProcessName = "svchost.exe", FailureBucketId = "0x20001_HYPERVISOR" },
        Quality = new(AnalysisQualityLevel.Excellent, 98, "Rich but unrelated dump.", [], [])
    };
    var matching = new DumpAnalysisResult
    {
        Dump = new() { CopiedPath = "matching-invalid-stack.dmp", ModificationTime = timestamp.AddMinutes(-1), Completion = DebuggerCompletion.Completed },
        Analyze = new() { BugCheckCode = "0x1AA", ProcessName = "expected.exe" },
        Quality = new(AnalysisQualityLevel.Limited, 45, "Matching but smaller dump.", [], [])
    };
    var primary = DumpIncidentMatcher.AssignPrimary(incident, [wrong, matching]);
    Assert(ReferenceEquals(primary, matching), "Higher analysis quality overrode the matching bugcheck.");
    Assert(wrong.Association.Status == DumpAssociationStatus.RejectedBugCheckMismatch && !wrong.Association.IsPrimaryForIncident, "Wrong-code dump was not explicitly rejected.");
    Assert(matching.Association.Status == DumpAssociationStatus.ExactBugCheckMatch && matching.Association.IsPrimaryForIncident, "Matching dump was not made primary.");
    var report = new EvidenceReport { Incident = incident, DumpAnalyses = [wrong, matching], AnalysisQuality = matching.Quality };
    var html = ReportWriter.BuildHtml(report); var rawSection = html.IndexOf("L. Raw evidence references", StringComparison.Ordinal);
    Assert(rawSection > 0 && !html[..rawSection].Contains("svchost.exe", StringComparison.OrdinalIgnoreCase) && !html[..rawSection].Contains("0x20001_HYPERVISOR", StringComparison.OrdinalIgnoreCase), "Rejected dump details leaked into primary report sections.");
    var text = ReportWriter.BuildPlainText(report); var allDumps = text.IndexOf("ALL COPIED DUMP ANALYSES", StringComparison.Ordinal);
    Assert(allDumps > 0 && !text[..allDumps].Contains("svchost.exe", StringComparison.OrdinalIgnoreCase), "Rejected process context leaked into the text report's primary dump.");
    Assert(DumpIncidentMatcher.AssignPrimary(incident, [wrong]) is null, "A collection containing only a wrong-code dump still produced a primary.");
    return Task.CompletedTask;
}

static async Task TrueCrashTimeDetection()
{
    var folder = Path.Combine(Path.GetTempPath(), "CecCrashTime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    try
    {
        const string ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        static string E(string provider, int id, string utc, string record, string data)
            => $"<Event xmlns='{ns}'><System><Provider Name='{provider}'/><EventID>{id}</EventID><Level>2</Level><TimeCreated SystemTime='{utc}'/><EventRecordID>{record}</EventRecordID><Channel>System</Channel></System><EventData>{data}</EventData></Event>";
        var xml =
            E("Microsoft-Windows-Kernel-General", 12, "2026-07-25T21:38:11Z", "100", "") +
            E("Microsoft-Windows-Kernel-Power", 41, "2026-07-25T21:38:14Z", "101", "<Data Name='BugcheckCode'>426</Data><Data Name='BugcheckParameter1'>1</Data>") +
            E("EventLog", 6008, "2026-07-25T21:38:38Z", "102", "<Data>10:25:36 PM</Data><Data>7/25/2026</Data>") +
            E("Microsoft-Windows-WER-SystemErrorReporting", 1001, "2026-07-25T21:39:32Z", "103", "<Data Name='param1'>0x000001aa (0x1, 0x7, 0x3, 0x4)</Data>");
        await File.WriteAllTextAsync(Path.Combine(folder, "System.xml"), xml);
        await File.WriteAllTextAsync(Path.Combine(folder, "Application.xml"), string.Empty);
        var detector = new IncidentDetector(new EventLogReader(), new StructuredLog(Path.Combine(folder, "test.log")));
        var incidents = await detector.DetectAsync(DateTimeOffset.MinValue, folder, CancellationToken.None);
        var incident = incidents.Single();
        Assert(incident.Timestamp == DateTimeOffset.Parse("2026-07-25T22:25:36+01:00"), $"Selected crash time was {incident.Timestamp:O}, not Event 6008's previous shutdown.");
        Assert(incident.RecordedAt == DateTimeOffset.Parse("2026-07-25T21:39:32Z"), "The WER record-written time was not retained separately.");
        Assert(incident.RebootTime == DateTimeOffset.Parse("2026-07-25T21:38:11Z"), "The next boot boundary was not correlated.");
        Assert(incident.TimestampBasis.Contains("6008", StringComparison.Ordinal), "Timestamp basis does not identify Event 6008.");
    }
    finally { Directory.Delete(folder, true); }
}

static Task CrashTimeSelection()
{
    var crash = DateTimeOffset.Parse("2026-07-25T22:25:36+01:00");
    var boot = DateTimeOffset.Parse("2026-07-25T22:38:11+01:00");
    var incident = new Incident("time", crash, IncidentKind.BugCheck, "0x1AA", "test", "0x1AA", RebootTime: boot);
    var events = new[]
    {
        new EvidenceEvent(boot.AddSeconds(3), "System", "Microsoft-Windows-Kernel-Power", 41, "1", "BugcheckCode=426"),
        new EvidenceEvent(boot.AddSeconds(27), "System", "EventLog", 6008, "2", "previous shutdown",
            Data: new Dictionary<string, string> { ["Data0"] = "10:25:36 PM", ["Data1"] = "7/25/2026" }),
        new EvidenceEvent(boot.AddSeconds(81), "System", "Microsoft-Windows-WER-SystemErrorReporting", 1001, "2", "bugcheck")
    };
    var dump = new DumpAnalysisResult { Dump = new() { ModificationTime = boot.AddSeconds(81), CreationTime = boot.AddSeconds(80) } };
    var selected = CrashTimestampAnalyzer.Build(incident, events, dump);
    Assert(selected.SelectedCrashTime == crash, "A post-boot dump/WER timestamp displaced the Event 6008 shutdown time.");
    Assert(selected.Event6008RecordedTime == boot.AddSeconds(27) && selected.WerSystemErrorEventTime == boot.AddSeconds(81), "Post-boot evidence timestamps were not retained.");
    Assert(selected.Confidence == CrashTimeConfidence.WindowsReported, "Event 6008 shutdown time did not receive WindowsReported confidence.");
    return Task.CompletedTask;
}

static Task DualAnchorTimeline()
{
    var crash = DateTimeOffset.Parse("2026-07-25T22:25:36+01:00");
    var boot = DateTimeOffset.Parse("2026-07-25T22:38:11+01:00");
    var incident = new Incident("anchors", crash, IncidentKind.BugCheck, "0x1AA", "test", "0x1AA", RebootTime: boot);
    var evidence = new CrashTimestampEvidence { SelectedCrashTime = crash, NextBootTime = boot, SelectedCrashTimeSource = "Event 6008", Confidence = CrashTimeConfidence.WindowsReported };
    var events = new[]
    {
        new EvidenceEvent(boot, "System", "Microsoft-Windows-Kernel-General", 12, "4", "boot"),
        new EvidenceEvent(boot.AddSeconds(81), "System", "Microsoft-Windows-WER-SystemErrorReporting", 1001, "2", "bugcheck")
    };
    var timeline = EventTimelineEngine.Build(incident, events, evidence);
    Assert(timeline.Any(item => item.Title == "Crash time anchor" && item.RelativeLabel == "Crash T±0"), "Crash anchor is missing.");
    Assert(timeline.Single(item => item.Source.Contains("/12", StringComparison.Ordinal)).RelativeLabel == "Boot T±0", "Boot event is not the boot anchor.");
    var wer = timeline.Single(item => item.Source.Contains("/1001", StringComparison.Ordinal));
    Assert(wer.RelativeLabel == "Boot T+81s" && wer.Phase == TimelinePhase.DumpCreation, "Post-boot WER was shown against the crash anchor or as the crash.");
    return Task.CompletedTask;
}

static Task InvalidStackConclusion()
{
    var incident = new Incident("1aa", DateTimeOffset.Now, IncidentKind.BugCheck, "0x1AA", "test", "0x1AA");
    var dump = new DumpAnalysisResult
    {
        Analyze = new() { BugCheckCode = "0x1AA", ExceptionAddress = "nt!RtlpGetStackLimitsEx", ProcessName = "SearchProtocolHost.exe" }
    };
    var assessment = new CulpritAssessment { HeadlineComponent = "Undetermined", Confidence = ConfidenceLevel.InsufficientEvidence };
    var conclusion = IncidentConclusionEngine.Build(incident, dump, assessment);
    Assert(conclusion.FailureType == "Invalid kernel stack", "0x1AA failure type was not expressed in plain English.");
    Assert(conclusion.DetectionLocation == "nt!RtlpGetStackLimitsEx", "Detection location was not kept separate.");
    Assert(conclusion.ActiveProcessContext == "SearchProtocolHost.exe", "Active process context was lost.");
    Assert(conclusion.UnderlyingCause == "Undetermined" && conclusion.CulpritConfidence == ConfidenceLevel.InsufficientEvidence, "Failure type or process context was promoted to a cause.");
    return Task.CompletedTask;
}

static async Task<DumpAnalysisResult> ParseAndPrepareFamily(string name)
{
    var analysis = await ParseFixture(name);
    if (NumericParser.TryParse(analysis.Analyze.BugCheckCode, out var code))
        analysis.DecodedParameters.AddRange(BugCheckKnowledge.DecodeParameters(code, analysis.Analyze.BugCheckParameters));
    analysis.FamilyAnalysis = BugCheckFamilyAnalyzer.Analyze(analysis);
    return analysis;
}

static async Task<DumpAnalysisResult> ParseFixture(string name)
{
    var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    var output = await File.ReadAllTextAsync(path);
    var dump = new DumpEvidence { CopiedPath = name + ".dmp", DumpType = DumpType.SmallMemory, Completion = DebuggerCompletion.Completed };
    return new DebuggerOutputParser().Parse(output, string.Empty, dump);
}

static string EventXml(string provider, int id, string channel, string time, string record) => $"<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='{provider}'/><EventID>{id}</EventID><Level>2</Level><TimeCreated SystemTime='{time}'/><EventRecordID>{record}</EventRecordID><Channel>{channel}</Channel></System><EventData><Data Name='Reason'>test</Data></EventData></Event>";
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
