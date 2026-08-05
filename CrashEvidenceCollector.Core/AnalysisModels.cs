namespace CrashEvidenceCollector.Core;

public enum DumpType { Unknown, SmallMemory, KernelMemory, AutomaticMemory, ActiveMemory, CompleteMemory, LiveKernelReport, WerMinidump, WatchdogLiveDump, GraphicsLiveDump }
public enum DebuggerCompletion { NotAttempted, Completed, Partial, TimedOut, Cancelled, Failed, Unsupported }
public enum SymbolQuality { Good, Partial, Poor, Unavailable }
public enum AnalysisQualityLevel { Excellent, Good, Limited, Poor, Inconclusive }
public enum EvidenceOrigin { DirectObservation, DebuggerConclusion, AnalyzerInference, PossibleCorrelation, Unknown }
public enum ConfidenceLevel { ConfirmedByDebuggerEvidence, High, Moderate, Low, InsufficientEvidence }
public enum CandidateKind { Driver, Hardware, Firmware, WindowsSubsystem, StorageCorruption, MemoryCorruption, CpuInstability, ApplicationContext, Unknown }
public enum ModuleCategory { MicrosoftKernelCore, MicrosoftInboxDriver, HardwareFirmwareAbstraction, GraphicsDriver, StorageDriver, FileSystemDriver, FileSystemMinifilter, AntivirusSecurity, VirtualizationHypervisor, NetworkDriver, AudioDriver, RgbMonitoringOverclocking, SandboxingFiltering, ThirdPartyMiscellaneous, UnknownUnverifiable }
public enum StackRelationship { ExecutingAtFault, DirectCaller, ParticipatingLowerInStack, LoadedNotOnStack, Unknown }
public enum TimelinePhase { BeforeCrash, Crash, DumpCreation, Reboot, AfterReboot, Unknown }
public enum RecommendationUrgency { Immediate, Soon, Routine, Optional }
public enum RecommendationRisk { Low, Medium, High }
public enum RecommendationPurpose { Corrective, IsolationTest, DataProtection, FurtherCollection }
public enum ParameterSemantic { Unknown, Address, VirtualAddress, InstructionPointer, Irql, AccessType, ExceptionCode, TrapFrame, ContextRecord, ExceptionRecord, Irp, DeviceObject, DriverObject, Thread, WheaErrorRecord, NtStatus, TimeoutSeconds, Flags, StackLimitType, Subtype }
public enum DumpAssociationStatus { Unassessed, ExactBugCheckMatch, TimestampOnlyMatch, RejectedBugCheckMismatch, RejectedTooDistant, InsufficientEvidence }
public enum CrashTimeConfidence { ExactDumpHeader, WindowsReported, EstimatedRange, Low, Unavailable }

public sealed record BugCheckParameterDefinition(int Index, string Name, ParameterSemantic Semantic, string Explanation, IReadOnlyDictionary<ulong, string>? Values = null);
public sealed record BugCheckDefinition(ulong Code, string Name, string ConciseExplanation, string DetailedExplanation, IReadOnlyList<BugCheckParameterDefinition> Parameters, IReadOnlyList<string> RelevantCommands, IReadOnlyList<string> EvidencePatterns, IReadOnlyList<string> ResponsibleRecommendations, string MisinterpretationWarning, string Source);
public sealed record DecodedParameter(int Index, string RawValue, string Name, string Meaning, string ConvertedValue, ParameterSemantic Semantic, bool AddressIsCanonical, string? SymbolicValue = null);

public sealed class DumpEvidence
{
    public string SourcePath { get; set; } = string.Empty;
    public string CopiedPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTimeOffset CreationTime { get; set; }
    public DateTimeOffset ModificationTime { get; set; }
    public DateTimeOffset CopiedFileCreationTime { get; set; }
    public DateTimeOffset CopiedFileModificationTime { get; set; }
    public DateTimeOffset? DumpHeaderTime { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DumpType DumpType { get; set; }
    public string Architecture { get; set; } = "Unknown";
    public string DebuggerPath { get; set; } = string.Empty;
    public string DebuggerVersion { get; set; } = "Unavailable";
    public DateTimeOffset? AnalysisStarted { get; set; }
    public DateTimeOffset? AnalysisEnded { get; set; }
    public TimeSpan CommandTimeout { get; set; }
    public int? ExitCode { get; set; }
    public DebuggerCompletion Completion { get; set; } = DebuggerCompletion.NotAttempted;
    public string SanitizedCommandLine { get; set; } = string.Empty;
    public string? RawOutputPath { get; set; }
    public string? RawErrorPath { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class SymbolDiagnostics
{
    public SymbolQuality Quality { get; set; } = SymbolQuality.Unavailable;
    public string SymbolPath { get; set; } = string.Empty;
    public bool NtSymbolsLoaded { get; set; }
    public List<string> MissingSymbols { get; set; } = [];
    public List<string> MismatchedPdbs { get; set; } = [];
    public List<string> DeferredModules { get; set; } = [];
    public List<string> ExportOnlyModules { get; set; } = [];
    public List<string> TimestampWarnings { get; set; } = [];
    public List<string> ExtensionFailures { get; set; } = [];
    public List<string> OtherWarnings { get; set; } = [];
    public string Explanation { get; set; } = string.Empty;
}

public sealed class AnalyzeData
{
    public string? BugCheckCode { get; set; }
    // True only when CDB did not expose a code and the selected Windows event
    // supplied it as context. Matchers and reports must not call this a
    // debugger-confirmed dump code.
    public bool BugCheckCodeFromSelectedIncidentFallback { get; set; }
    public List<string> BugCheckParameters { get; set; } = [];
    public string? BugCheckString { get; set; }
    public string? ProcessName { get; set; }
    public string? Thread { get; set; }
    public string? TrapFrame { get; set; }
    public string? ContextRecord { get; set; }
    public string? ExceptionRecord { get; set; }
    public string? ExceptionCode { get; set; }
    public string? ExceptionAddress { get; set; }
    public string? ExceptionSymbol { get; set; }
    public List<string> ExceptionParameters { get; set; } = [];
    public string? ReadAddress { get; set; }
    public string? WriteAddress { get; set; }
    public string? ExecuteAddress { get; set; }
    public string? FaultingIp { get; set; }
    public string? SymbolName { get; set; }
    public string? ModuleName { get; set; }
    public string? ImageName { get; set; }
    public string? ImageVersion { get; set; }
    public string? StackCommand { get; set; }
    public string? FailureBucketId { get; set; }
    public string? HashString { get; set; }
    public string? FailureIdHash { get; set; }
    public string? DefaultBucketId { get; set; }
    public int? CustomerCrashCount { get; set; }
    public TimeSpan? AnalysisElapsed { get; set; }
    public Dictionary<string, string> BlackBoxes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> KeyValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> UnknownFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> RawSections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; set; } = [];
    public List<string> DumpAttributes { get; set; } = [];
}

public sealed record StackFrame(int Index, string? ChildStackPointer, string? ReturnAddress, string? Module, string? Symbol, string? Displacement, string? SourceFile, int? SourceLine, string RawLine, bool Resolved, ModuleCategory ModuleCategory, bool IsTransitionFrame, string? TransitionKind, StackRelationship Relationship = StackRelationship.Unknown);

public sealed class DriverIdentity
{
    public string ModuleName { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? BaseAddress { get; set; }
    public string? EndAddress { get; set; }
    public int? LoadOrder { get; set; }
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string? FileDescription { get; set; }
    public string? FileVersion { get; set; }
    public string? Signer { get; set; }
    public bool? IsMicrosoftSigned { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public string? Sha256 { get; set; }
    public string? ServiceName { get; set; }
    public string? StartType { get; set; }
    public string? PnpDevice { get; set; }
    public string? MinifilterAltitude { get; set; }
    public int? MinifilterInstances { get; set; }
    public bool WasUnloaded { get; set; }
    public string MetadataSource { get; set; } = "Dump";
    public ModuleCategory Category { get; set; } = ModuleCategory.UnknownUnverifiable;
    public StackRelationship StackRelationship { get; set; } = StackRelationship.LoadedNotOnStack;
    public List<string> Evidence { get; set; } = [];
}

public sealed record CandidateEvidence(string Description, int Weight, EvidenceOrigin Origin, string SourceReference);
public sealed class CandidateAssessment
{
    public string Entity { get; set; } = "Unknown";
    public CandidateKind Kind { get; set; } = CandidateKind.Unknown;
    public int Score { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.InsufficientEvidence;
    public List<CandidateEvidence> SupportingEvidence { get; set; } = [];
    public List<string> CounterEvidence { get; set; } = [];
    public string Explanation { get; set; } = string.Empty;
}

public sealed class CulpritAssessment
{
    public string HeadlineComponent { get; set; } = "Undetermined";
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.InsufficientEvidence;
    public EvidenceOrigin Origin { get; set; } = EvidenceOrigin.AnalyzerInference;
    public string Explanation { get; set; } = "The available evidence does not identify a responsible component.";
    public List<CandidateAssessment> Candidates { get; set; } = [];
}

public sealed record AnalysisQuality(AnalysisQualityLevel Level, int Score, string Explanation, IReadOnlyList<string> Strengths, IReadOnlyList<string> Limitations);
public sealed record Recommendation(string Action, string Reason, IReadOnlyList<string> SupportingEvidence, RecommendationUrgency Urgency, RecommendationRisk Risk, bool Reversible, string ExpectedDiagnosticValue, RecommendationPurpose Purpose);
public sealed record CorrelatedTimelineEntry(
    DateTimeOffset Timestamp,
    TimeSpan OffsetFromIncident,
    TimelinePhase Phase,
    string Source,
    string Title,
    string Explanation,
    EvidenceOrigin Origin,
    string RelativeLabel = "");
public sealed record IncidentFingerprint(string IncidentId, string BugCheckCode, string? Subtype, string? FailureBucket, string? FaultingInstruction, string? Module, string? Symbol, string? Process, string? ExceptionCode, IReadOnlyList<string> TopFrames, IReadOnlyList<string> ThirdPartyFrames, string Hash);
public sealed record ComparisonFinding(string Title, string Explanation, EvidenceOrigin Origin, ConfidenceLevel Confidence, IReadOnlyList<string> IncidentIds);
public sealed record FamilyEvidence(string Entity, CandidateKind Kind, string Description, int Weight, EvidenceOrigin Origin, string SourceReference);

public sealed class BugCheckFamilyAnalysis
{
    public string Family { get; set; } = "Generic bugcheck";
    public List<string> Facts { get; set; } = [];
    public List<FamilyEvidence> CandidateEvidence { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
    public List<string> RawReferences { get; set; } = [];
}

public sealed record DumpIncidentAssociation(
    DumpAssociationStatus Status,
    bool IsPrimaryForIncident,
    int Score,
    TimeSpan TimestampDelta,
    string Explanation);

public sealed class CrashTimestampEvidence
{
    public DateTimeOffset? DumpHeaderTime { get; set; }
    public DateTimeOffset? DumpSourceCreationTime { get; set; }
    public DateTimeOffset? DumpSourceModifiedTime { get; set; }
    public DateTimeOffset? CopiedFileCreationTime { get; set; }
    public DateTimeOffset? CopiedFileModifiedTime { get; set; }
    public DateTimeOffset? WerSystemErrorEventTime { get; set; }
    public DateTimeOffset? KernelPowerEventTime { get; set; }
    public DateTimeOffset? Event6008RecordedTime { get; set; }
    public DateTimeOffset? Event6008PreviousShutdownTime { get; set; }
    public DateTimeOffset? PreviousBootTime { get; set; }
    public DateTimeOffset? NextBootTime { get; set; }
    public DateTimeOffset? SelectedCrashTime { get; set; }
    public DateTimeOffset? EstimatedRangeStart { get; set; }
    public DateTimeOffset? EstimatedRangeEnd { get; set; }
    public string SelectedCrashTimeSource { get; set; } = "Unavailable";
    public CrashTimeConfidence Confidence { get; set; } = CrashTimeConfidence.Unavailable;
    public string Explanation { get; set; } = "No trustworthy crash-time evidence was available.";
}

public sealed record IncidentConclusion(string FailureType, string DetectionLocation, string ActiveProcessContext, string UnderlyingCause, ConfidenceLevel CulpritConfidence);
public sealed record SmartAttribute(int Id, string Name, int? Current, int? Worst, ulong RawValue, bool IsConcern, string Interpretation);

public sealed class StorageDeviceHealth
{
    public string Device { get; set; } = "Unknown storage device";
    public string? DeviceId { get; set; }
    public string? MediaType { get; set; }
    public string? HealthStatus { get; set; }
    public string? OperationalStatus { get; set; }
    public int? TemperatureCelsius { get; set; }
    public int? WearPercent { get; set; }
    public ulong? PowerOnHours { get; set; }
    public ulong? ReadErrorsTotal { get; set; }
    public ulong? WriteErrorsTotal { get; set; }
    public string Source { get; set; } = "Windows storage reliability API";
    public bool PredictFailure { get; set; }
    public List<SmartAttribute> SmartAttributes { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class DumpAnalysisResult
{
    public DumpEvidence Dump { get; set; } = new();
    public AnalyzeData Analyze { get; set; } = new();
    public SymbolDiagnostics Symbols { get; set; } = new();
    public List<DecodedParameter> DecodedParameters { get; set; } = [];
    public List<StackFrame> StackFrames { get; set; } = [];
    public List<DriverIdentity> Modules { get; set; } = [];
    public CulpritAssessment Assessment { get; set; } = new();
    public AnalysisQuality Quality { get; set; } = new(AnalysisQualityLevel.Inconclusive, 0, "Analysis was not completed.", [], []);
    public IncidentFingerprint? Fingerprint { get; set; }
    public List<Recommendation> Recommendations { get; set; } = [];
    public List<string> UnsupportedCommands { get; set; } = [];
    public List<string> RawCommandSections { get; set; } = [];
    public BugCheckFamilyAnalysis FamilyAnalysis { get; set; } = new();
    public DumpIncidentAssociation Association { get; set; } = new(DumpAssociationStatus.Unassessed, false, 0, TimeSpan.Zero, "Dump-to-incident association has not been evaluated.");
}
