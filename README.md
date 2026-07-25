# Crash Evidence Collector (BSODGuru)

Crash Evidence Collector is a read-only Windows 11 desktop application for collecting and analysing evidence around a bugcheck, unexpected restart, freeze, hardware error, application crash, or power loss. It is built with C# and .NET 10 WinForms.

It never changes drivers, services, BIOS settings, registry settings, page-file settings, dump settings, or Windows configuration. It does not enable Driver Verifier or run stress tests.

## Solution structure

- `CrashEvidenceCollector.App` — the normal unelevated WinForms application. Its splitter workspace keeps the filtered timeline, selected incident, collection actions, progress, evidence status, HTML report, and raw debugger output together.
- `CrashEvidenceCollector.Core` — incident detection, event and system collection, streamed file copying, CDB execution, debugger parsing, code knowledge, typed decoders, family analysis, confidence scoring, correlation, reporting, redaction, packaging, logging, and validated IPC.
- `CrashEvidenceCollector.Helper` — the only `requireAdministrator` executable. It has one allow-listed operation: stream-copy protected Windows crash dumps to a validated evidence destination.
- `CrashEvidenceCollector.WorkflowHarness` — a clearly labelled copied-log workflow and optional developer smoke tests.
- `CrashEvidenceCollector.Tests` — a dependency-free automated test runner using saved CDB-style output fixtures.
- `TestData` — copied/sample XML, inventory, WER, dump placeholder, and saved debugger-output fixtures used only in explicit TEST-DATA MODE.

## Requirements

- Windows 11
- .NET SDK 10.0 or Visual Studio with the .NET 10 Windows desktop workload
- Microsoft Debugging Tools for Windows for dump analysis (`cdb.exe`). Collection and reporting continue with a warning if it is unavailable.
- Internet access for first-time Microsoft public-symbol downloads. Symbols are cached under `%LOCALAPPDATA%\CrashEvidenceCollector\Symbols`.

No third-party NuGet packages are required.

## Build and run

```powershell
dotnet build BSODGuru.slnx -c Release
dotnet run --project .\CrashEvidenceCollector.App\CrashEvidenceCollector.App.csproj -c Release
```

Release executables:

- `CrashEvidenceCollector.App\bin\Release\net10.0-windows\CrashEvidenceCollector.App.exe`
- `CrashEvidenceCollector.App\bin\Release\net10.0-windows\CrashEvidenceCollector.Helper.exe`
- `CrashEvidenceCollector.WorkflowHarness\bin\Release\net10.0-windows\CrashEvidenceCollector.WorkflowHarness.exe`
- `CrashEvidenceCollector.Tests\bin\Release\net10.0-windows\CrashEvidenceCollector.Tests.exe`

## Using the application

1. Start `CrashEvidenceCollector.App.exe`. The normal interface runs unelevated.
2. The left splitter shows the last hour of system crashes and shutdowns by default. Use the range and type filters to move from the immediate incident to older history.
3. Select one incident. Its code and plain-English meaning appear in the right pane.
4. Choose **Collect Evidence**. Leave full `MEMORY.DMP` excluded unless it is genuinely required and you accept the size/privacy risk.
5. If Windows denies the dump read, approve the separate helper’s UAC prompt. Declining it does not abort the other evidence categories.
6. Watch byte/percentage progress during dump copying and hashing. During CDB analysis the status shows elapsed time, captured output/error line counts, captured size, and the latest debugger line.
7. When complete, use the **Readable report**, **Raw debugger output**, and **Evidence status** tabs without leaving the workspace.
8. The report toolbar can copy the full text report, copy JSON, save the text report elsewhere, or open the Windows print dialog. Choose **Microsoft Print to PDF** there to save a PDF.
9. Use **Open output folder** or **Copy summary**.

Settings control the pre-crash window (10 minutes by default), post-startup window (5 minutes by default), CDB analysis, CDB timeout, report redaction, full-dump inclusion, output directory, and TEST-DATA MODE.

## What is collected

- Focused System, Application, WER, Kernel-PnP, Kernel-Power, EventLog, WHEA, disk, NTFS, storage, service, driver, and application-crash events.
- Recent Windows minidumps, supported WER/live-kernel dumps, watchdog/graphics reports where present, and optional `MEMORY.DMP`.
- WER/Reliability Monitor report records.
- Windows edition/build/update history; BIOS, board, CPU, RAM and speeds; GPU and driver; physical storage and supported health/reliability data.
- ATA SMART vendor bytes where the supported Windows WMI provider exposes them, with cautious decoding of important IDs.
- Hyper-V, VBS/Device Guard, signed and loaded drivers, PnP inventory, minifilters, page-file and crash-dump configuration, boot time, and uptime.

All large dumps are copied with asynchronous streaming. Each copied dump records its source/evidence path, original timestamps, SHA-256, type, architecture, debugger/version, symbol path/quality, analysis timing, timeout, exit status, and raw stdout/stderr references.

Every analysed dump is explicitly correlated with the selected incident. A matching bugcheck code and close source timestamp take precedence over analysis richness. An explicit bugcheck mismatch disqualifies that dump from the incident headline, culprit assessment, recommendations, process context, stack, and failure bucket. The unrelated analysis remains preserved and clearly labelled in the raw-evidence section.

Crash time is derived independently from dump association. The collector keeps the dump-header time, original and copied file timestamps, WER/SystemErrorReporting time, Kernel-Power time, Event 6008 record-written time, Event 6008's embedded previous-shutdown time, and adjacent boot boundaries as separate fields. It prefers a valid pre-boot dump-header time, then Windows' Event 6008 previous-shutdown value. Post-boot WER registration and dump final-modified times are never silently substituted for the crash time.

## Dump analysis

CDB receives a deterministic command script. Dump text cannot add commands. The baseline establishes Microsoft symbols and collects target, `!analyze`, bugcheck, exception/context, registers, stack, thread/process, modules, black boxes, and minifilter output. Validated bugcheck parameters add only applicable address-specific commands such as `.cxr`, `.exr`, `!errrec`, `!irp`, or `!devstack`. Small dumps skip commands known to require fuller memory.

The parser is line-oriented, preserves unknown fields and raw command sections, and extracts bugcheck fields, parameters, exception/context records, failure buckets, stack frames, modules, warnings, key/value data, symbol problems, and elapsed time. Optional family analyzers cover power/blocked-IRP, WHEA, TDR, invalid-stack, storage/store, and hypervisor/secure-kernel evidence.

The report deliberately separates:

- directly observed facts;
- debugger conclusions;
- analyzer inference;
- possible correlation;
- unknown or unavailable evidence.

Culprit confidence is separate from analysis quality. Microsoft framework modules and the active process are treated as context unless stronger evidence exists. Poor symbols cap driver attribution at Low confidence.

For `EXCEPTION_ON_INVALID_STACK` (0x1AA), the debugger restores the supplied context and exception records before repeating register and stack commands. The report separately labels the failure type (invalid kernel stack), the exception/detection location, active process context, and underlying cause. “Invalid kernel stack” is a failure category, not a hardware or software component, and the cause remains Undetermined when the reconstructed evidence does not support attribution.

## Reports and archives

Every timestamped collection produces:

- `report.html` — human-readable A–L analysis;
- `report.json` — strongly typed machine-readable evidence;
- `report.txt` — copy-friendly plain-text analysis;
- `Raw\` — event XML/JSON, inventories, WER files, dump manifest/copies, command script, CDB stdout, and CDB stderr;
- a sibling timestamped ZIP containing the complete collection directory.

Reports redact the current account/profile path by default. Raw event records and dumps are not rewritten because doing so would destroy evidentiary fidelity. Dumps may contain passwords, document fragments, keys, browsing data, and other private memory; inspect the archive before sharing it.

The readable timeline is deliberately filtered to significant crash, boot, dump, WHEA, storage, PnP, and error records. It uses separate `Crash T±…` and `Boot T±…` labels so post-reboot records cannot appear as negative crash offsets. The complete event window remains available in the HTML technical appendix, JSON report, and raw event JSON/XML.

## Test-data mode and verification

Run the complete copied-input workflow without reading production logs:

```powershell
dotnet run --project .\CrashEvidenceCollector.WorkflowHarness\CrashEvidenceCollector.WorkflowHarness.csproj -c Release -- .\TestData .\TestRuns
dotnet run --project .\CrashEvidenceCollector.Tests\CrashEvidenceCollector.Tests.csproj -c Release
```

The UI exposes the same mode in Settings and displays an amber `TEST-DATA MODE` banner. Saved debugger text is parsed verbatim and is labelled as a fixture; no sample conclusion is inserted into normal operation.

Developer-only checks:

```powershell
# Opens the helper's UAC prompt and copies protected minidumps only.
dotnet run --project .\CrashEvidenceCollector.WorkflowHarness --configuration Release -- --helper-smoke-test <helper.exe> <destination>

# Analyses an already accessible dump after making a streamed evidence copy.
dotnet run --project .\CrashEvidenceCollector.WorkflowHarness --configuration Release -- --dump-smoke-test <dump.dmp> <destination>
```

## Safety and elevation

The UI manifest is `asInvoker`. Protected dump access uses a current-user-only named pipe, a random 256-bit nonce, fixed-time nonce comparison, a 64 KiB message limit, protocol/operation allow lists, absolute destination validation, and known Windows source paths. The helper cannot accept an arbitrary source path or execute a command.

CDB analyses only the copied evidence file and runs unelevated. Timeout or cancellation kills its complete child process tree while preserving partial stdout/stderr.

## Known limitations

- A power cut may prevent Windows from writing any dump or final event. Missing evidence cannot be reconstructed.
- Protected dump validation requires approving the helper’s UAC prompt. An unelevated process is expected to receive `Access denied`.
- CDB output is text and changes between debugger/Windows versions. Unknown fields and raw sections are retained, but a future format may reduce parsed detail.
- A small dump may omit memory pages, IRPs, device stacks, WHEA records, or context required for a reliable culprit. The report lowers quality/confidence and recommends better collection rather than guessing.
- Public symbols do not provide private symbols for third-party drivers. Their frames can remain unresolved even with a healthy Microsoft symbol setup.
- Standard Windows APIs do not expose every vendor NVMe/ATA health field on every controller. ATA raw values and thresholds are vendor-specific; non-zero counters are warnings, not automatic bugcheck attribution.
- Driver inventory does not guarantee a reliable on-disk path, service mapping, signer chain, or file hash for every loaded module.
- The family analyzers consume only evidence present in deterministic CDB output; they do not reverse-engineer undocumented kernel structures.
- Historical comparisons use retained prior `report.json` fingerprints. Deleted/moved reports and events aged out of Windows logs cannot participate.
- The locally built helper is not Authenticode-signed, so UAC may show an unknown publisher until a production signing certificate is used.
