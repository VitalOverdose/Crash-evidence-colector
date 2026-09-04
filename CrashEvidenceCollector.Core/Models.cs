using System.Text.Json.Serialization;

namespace CrashEvidenceCollector.Core;

public enum IncidentKind { BugCheck, UnexpectedShutdown, PowerLossOrFreeze, ApplicationCrash, HardwareError }
public enum EvidenceState { Pending, Running, Success, Warning, Failure, Skipped }
public enum IncidentTimeConfidence { EventTimestamp, Event6008ReportedShutdown, EstimatedFromRebootBoundary, PostRebootReportTime }

public sealed record Incident(
    string Id,
    DateTimeOffset Timestamp,
    IncidentKind Kind,
    string Title,
    string Summary,
    string? BugCheckCode = null,
    IReadOnlyList<string>? Parameters = null,
    DateTimeOffset? BootTime = null,
    string Source = "System event log",
    DateTimeOffset? RecordedAt = null,
    string TimestampBasis = "Source event timestamp",
    IncidentTimeConfidence TimeConfidence = IncidentTimeConfidence.EventTimestamp,
    DateTimeOffset? RebootTime = null);

public sealed record EvidenceEvent(
    DateTimeOffset Timestamp,
    string LogName,
    string Provider,
    int EventId,
    string Level,
    string Message,
    string? RecordId = null,
    IReadOnlyDictionary<string, string>? Data = null);

public sealed record EvidenceItem(
    string Category,
    EvidenceState State,
    string Summary,
    IReadOnlyList<string>? Files = null,
    string? Error = null,
    long? Bytes = null);

public sealed record Observation(string Severity, string Title, string Detail);

public sealed record InstalledProgram(
    string Name,
    string? Version,
    string? Publisher,
    DateTimeOffset? InstallDate,
    string? InstallLocation,
    string Source);

public sealed record ProgramWebInfo(
    string ProgramName,
    string QueryUsed,
    string? MatchedName,
    string? MatchedId,
    string? LatestVersion,
    string? Publisher,
    string? Homepage,
    string? Description,
    string Source,
    string? Note = null);

public sealed record CodeInterpretation(string Category, string RawValue, string Name, string Explanation, string? ConvertedValue = null);

public sealed class MachineSnapshot
{
    public string ComputerName { get; set; } = Environment.MachineName;
    public string Windows { get; set; } = "Unavailable";
    public string Architecture { get; set; } = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
    public string Bios { get; set; } = "Unavailable";
    public string Motherboard { get; set; } = "Unavailable";
    public string Cpu { get; set; } = "Unavailable";
    public string Memory { get; set; } = "Unavailable";
    public string Gpu { get; set; } = "Unavailable";
    public string Storage { get; set; } = "Unavailable";
    public string Virtualization { get; set; } = "Unavailable";
    public string CrashDumpConfiguration { get; set; } = "Unavailable";
    public string PageFiles { get; set; } = "Unavailable";
    public DateTimeOffset? BootTime { get; set; }
    public TimeSpan? Uptime { get; set; }
    public Dictionary<string, string> RawSections { get; set; } = [];
}

public sealed class EvidenceReport
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.Now;
    public Incident Incident { get; set; } = null!;
    public CollectionOptions Options { get; set; } = new();
    public MachineSnapshot Machine { get; set; } = new();
    public List<EvidenceEvent> Events { get; set; } = [];
    public List<EvidenceItem> Evidence { get; set; } = [];
    public List<Observation> Observations { get; set; } = [];
    public List<CodeInterpretation> CodeInterpretations { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<Incident> RelatedIncidents { get; set; } = [];
    public List<DumpAnalysisResult> DumpAnalyses { get; set; } = [];
    public CulpritAssessment OverallAssessment { get; set; } = new();
    public AnalysisQuality AnalysisQuality { get; set; } = new(AnalysisQualityLevel.Inconclusive, 0, "No dump analysis was available.", [], []);
    public List<Recommendation> Recommendations { get; set; } = [];
    public List<CorrelatedTimelineEntry> CorrelatedTimeline { get; set; } = [];
    public List<ComparisonFinding> ComparisonFindings { get; set; } = [];
    public List<StorageDeviceHealth> StorageDevices { get; set; } = [];
    public List<InstalledProgram> InstalledPrograms { get; set; } = [];
    public List<ProgramWebInfo> InstalledProgramWebInfo { get; set; } = [];
    public ApplicationFailureDetails? ApplicationFailure { get; set; }
    public CrashTimestampEvidence CrashTimestamp { get; set; } = new();
    public IncidentConclusion Conclusion { get; set; } = new("Undetermined failure", "Unavailable", "Unavailable", "Undetermined", ConfidenceLevel.InsufficientEvidence);
}

public sealed class CollectionOptions
{
    public int MinutesBefore { get; set; } = 10;
    public int MinutesAfterStartup { get; set; } = 5;
    public bool IncludeFullMemoryDump { get; set; }
    public bool RedactAccountName { get; set; } = true;
    public bool AnalyzeCrashDumps { get; set; } = true;
    public bool LookUpInstalledProgramsOnline { get; set; } = true;
    public bool StartMonitoringOnLaunch { get; set; } = true;
    public bool AlwaysRunElevated { get; set; }
    public int DebuggerTimeoutSeconds { get; set; } = 180;
    public string OutputRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Crash Evidence Collector");
    public string? TestDataDirectory { get; set; }

    [JsonIgnore]
    public bool IsTestDataMode => !string.IsNullOrWhiteSpace(TestDataDirectory);

    public void Validate()
    {
        if (MinutesBefore is < 1 or > 1440 || MinutesAfterStartup is < 1 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(MinutesBefore), "Evidence windows must be between 1 and 1,440 minutes.");
        if (DebuggerTimeoutSeconds is < 15 or > 1800) throw new ArgumentOutOfRangeException(nameof(DebuggerTimeoutSeconds), "Debugger timeout must be between 15 and 1,800 seconds.");
        if (string.IsNullOrWhiteSpace(OutputRoot)) throw new ArgumentException("An output folder is required.", nameof(OutputRoot));
        if (OutputRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0) throw new ArgumentException("The output path is invalid.", nameof(OutputRoot));
    }
}

public sealed record CollectionProgress(int Percent, string Category, string Message, EvidenceState State = EvidenceState.Running);
public sealed record CollectionResult(EvidenceReport Report, string OutputDirectory, string JsonPath, string HtmlPath, string TextPath, string ZipPath);

public sealed record HelperRequest(string ProtocolVersion, string Nonce, string Operation, string Destination, bool IncludeFullDump, DateTimeOffset? IncidentTimestamp = null);
public sealed record HelperResponse(bool Success, string Nonce, IReadOnlyList<string> CopiedFiles, IReadOnlyList<string> Errors);

public static class AppPaths
{
    public static string StateDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashEvidenceCollector");
    public static string StateFile => Path.Combine(StateDirectory, "state.json");
    public static string SettingsFile => Path.Combine(StateDirectory, "settings.json");
    public static string LogFile => Path.Combine(StateDirectory, "application.jsonl");
}
