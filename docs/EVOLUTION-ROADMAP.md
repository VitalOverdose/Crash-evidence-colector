# Crash Evidence Collector — Evolution Roadmap: Top 100 Improvements

Produced from a complete review of every subsystem at the current baseline (all Core engines, the
debugger pipeline, the IPC helper, the report writers, the test suite, and the fixtures), plus the
installed-programs inventory added alongside this document. Items are **ranked by engineering
value**: forensic impact × how often the evidence applies ÷ implementation cost, adjusted for risk
to the project's core guarantees (read-only, evidence-first, never overstate certainty).

Each item lists: **Value** (technical value) · **Difficulty** · **Risk** · **User impact** ·
**Forensic impact** · **When** (Now = next 1–3 milestones; Next = after Tier 1 lands; Later =
needs Tier 1/2 foundations or external data).

Non-negotiable constraints every item respects:

- The tool never changes drivers, services, firmware, registry, dump settings, or Windows
  configuration. Items that would require any change (verifier, ETW autologgers, live dumps) are
  explicitly opt-in, clearly labelled, and ranked lower for that reason.
- Every new claim in the report must carry its evidence origin (direct observation / debugger
  conclusion / analyzer inference / possible correlation / unknown), exactly as the existing
  A–L report does.
- Knowledge bases guide investigation; they never automatically assign blame.

---

## Tier 1 — Now (1–25): highest forensic return on effort

### 1. Native minidump header parser (no-debugger fallback)
Value: Very high · Difficulty: Medium · Risk: Low · User impact: Very high · Forensic impact: Very high · When: Now
Windows dump files start with a documented `PAGEDUMP`/`PAGEDU64` header containing the bugcheck
code, the four parameters, machine type, and the crash time. Parsing ~4 KB of the copied evidence
file directly would give `DumpIncidentMatcher` and `CrashTimestampAnalyzer` their two most
important inputs **without CDB installed at all** — today both are empty when Debugging Tools are
absent. This single item upgrades the "no debugger" experience from "events only" to "correct
association and crash time".

### 2. Adopt `!analyze -json` with `-v` fallback
Value: Very high · Difficulty: Low–Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Now
Recent debugger versions emit structured JSON from `!analyze`. Try it first in the command script;
keep the existing line-oriented parser as the fallback and as the raw-evidence record. This
removes the largest fragility the README already admits ("CDB output is text and changes between
versions") for the single most important command.

### 3. Expand the bugcheck knowledge base from 18 to 60+ codes
Value: Very high · Difficulty: Medium (data work) · Risk: Low · User impact: Very high · Forensic impact: High · When: Now
`BugCheckKnowledge` covers 18 codes today. The next tranche by real-world frequency: 0x1, 0x19
(BAD_POOL_HEADER), 0x24 (NTFS), 0x34, 0x3D, 0x44, 0x4E (PFN), 0x77/0x7A (kernel data inpage —
storage/paging semantics), 0xBE, 0xC2 (BAD_POOL_CALLER), 0xC4/0xC1 (verifier), 0xC5, 0xCE, 0xD5,
0xDE, 0xE1, 0xEA, 0xFC, 0x109 (PatchGuard — with its documented "do not chase the detector"
caveat), 0x113/0x117/0x119/0x141/0x144 (graphics/USB), 0x14x/0x15x families, 0x192, 0x1C7, 0x1CA,
0x1D5, 0x333. The definition format (typed parameter semantics, address-guarded commands, warning
text) is already exactly right — this is pure data authoring with immediate report quality gains.

### 4. Match loaded drivers against the Microsoft vulnerable-driver blocklist
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: High · When: Now
Microsoft publishes the recommended driver block rules (the HVCI blocklist) as XML. Ship a
snapshot, compare it against `loaded-drivers.txt`/`lm` output, and report matches as a security
observation with the version range and source citation — flagged as "known-vulnerable driver
present", never as "cause of this crash" unless it is also on the stack.

### 5. Decode minifilter altitudes into filter classes
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: High · When: Now
`fltmc` output is already collected but never interpreted. Microsoft's published altitude ranges
identify each filter's class (360000–389999 anti-virus, 140000–149999 encryption, 280000–289999
backup/replication, etc.). Rendering "third-party anti-virus filter at altitude 328010 between
NTFS and the failing I/O" turns an opaque list into subsystem recognition — and filter drivers are
one of the most common real-world crash contributors.

### 6. Correlate installed programs ↔ loaded drivers ↔ stack modules
Value: Very high · Difficulty: Medium · Risk: Low · User impact: Very high · Forensic impact: Very high · When: Now
The installed-programs inventory added today enables the strongest new correlation chain in the
product: "program X was installed 3 days before the crash; X ships kernel driver Y (matching
publisher/install path/INF provider); Y appears at stack frame 2." Each link is separately
evidenced and separately confident. This is what commercial tools print as a blunt guess; done
with explicit link-by-link evidence it becomes a genuinely superior conclusion.

### 7. Curated third-party driver knowledge base
Value: Very high · Difficulty: Medium (ongoing data) · Risk: Medium (must never auto-blame) · User impact: Very high · Forensic impact: High · When: Now (seed ~200 entries, grow forever)
A JSON knowledge pack mapping driver file names to vendor, product, category (anti-virus filter,
RGB/SMBus utility, overclocking tool, VPN/NDIS filter, anti-cheat, virtualization, storage
utility, motherboard suite), plus known-issue notes with affected version ranges and a citation.
Output is investigative guidance ("this driver family has documented issues with X; compare your
version") attached only to drivers already implicated by stack or family evidence. The
categories the prompt lists — RGB software, cheat engines, Killer NIC, Armoury Crate, NVMe vendor
tools — are exactly the rows of this table.

### 8. Run `lmvm` for every non-Microsoft on-stack module
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: High · When: Now
The command planner is deterministic, but module names from `lm t n` are known before analysis
ends — add a second, still-deterministic CDB pass (or extend the script with `lmDvm`-style output
for all modules) so every third-party stack participant gets image path, link timestamp, file
version, and company name from the dump itself. Today `ModuleClassifier` works with far less than
the debugger can prove.

### 9. `!chkimg` code-integrity check on the kernel and the faulting module
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: Very high · When: Now
`!chkimg -d nt` (and the faulting module) compares in-memory code against symbol-server images and
reports corrupt bytes. Single-bit deviations are the strongest software-observable evidence of
RAM/cache faults; multi-byte patches indicate hooking. Either way it converts "could be memory
corruption" from speculation into direct observation. Skip on small dumps (pages absent), as the
planner already does for other memory-dependent commands.

### 10. Disassemble around the faulting instruction (`u`, `ub`)
Value: High · Difficulty: Low–Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Now
Add `u {IP} L10` / `ub {IP} L8` for the validated instruction pointer and classify the faulting
instruction: null-plus-offset dereference (structure field through null pointer), wild indirect
call (corrupted vtable/function pointer), REP string op (buffer overrun), or nonsensical opcode
stream (executing corrupted/freed memory). Each pattern supports a different root-cause family and
is currently invisible in the report.

### 11. Address forensics on referenced addresses
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: High · When: Now
`TypedCodeDecoders.ClassifyAddress` exists but can go much further for 0xA/0x50/0xD1 parameter 1:
small offsets from null (0x0–0x1000) imply a specific field offset through a null pointer; values
like 0x????????`deadbeef or pool-fill patterns imply use-after-free; non-canonical bit patterns
one bit away from a canonical address suggest a flipped bit (cross-reference item 9); user-mode
addresses referenced at kernel IRQL imply captured-pointer misuse. Add `!pte {P1}` where the dump
supports it to distinguish "never mapped" from "paged out" from "protection mismatch".

### 12. Pool-tag intelligence
Value: High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Now
Ship a `pooltag.txt`-derived knowledge table, extract pool tags from `!analyze` and `!pool`
output for pool-corruption bugchecks (0x19, 0xC2, 0xC5), and add `!pool {P}` for validated pool
addresses. "Pool block tagged `Ntf0` (NTFS) with a corrupted following-block header" is a
dramatically better statement than the current raw parameter dump, and pool tags feed
cross-incident matching (item 41).

### 13. Dump-absence analyzer ("why is there no dump?")
Value: High · Difficulty: Low–Medium · Risk: Low · User impact: Very high · Forensic impact: Medium–High · When: Now
When an incident has no associated dump, today's report simply says so. All the evidence to
explain *why* is already collected: `CrashControl` configuration (dump type disabled?), page-file
configuration (too small / on the wrong volume / none), volmgr events 161/162 (dump write
failed), storage errors during the crash window, and power-loss incidents (no time to write).
A short deterministic decision tree turns the most frustrating support answer ("no dump") into an
actionable explanation with a specific fix recommendation.

### 14. Corroborate with Kernel-Power 41's embedded bugcheck data
Value: Medium–High · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium–High · When: Now
Event 41's data section carries `BugcheckCode`/`BugcheckParameter1–4` (decimal) when the previous
stop was a bugcheck, and zeros for true power loss. `IncidentDetector` currently treats 41 as a
power-loss/freeze signal only. Reading these fields distinguishes "bugcheck whose WER record is
missing" from "genuine power cut", corroborates dump association with a second independent code
source, and strengthens `PowerLossOrFreeze` classification.

### 15. Storage precursor channels: Storport 500/505, disk 153/7/51, Ntfs/Operational
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: High · When: Now
The collector reads System/Application/Kernel-PnP logs. Storage-rooted crashes (0x7A, 0x77, 0xF4,
0x154, 0xEF) usually telegraph themselves in `Microsoft-Windows-Storport/Operational` (505 latency
records), `Microsoft-Windows-Ntfs/Operational`, and disk events 153 (retried I/O), 7 (bad block),
51 (paging error) well before the stop. Add these channels to the read set and teach
`CodeDecoder`/the timeline their meanings.

### 16. WHEA corrected-error precursor scan
Value: High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: High · When: Now
Corrected machine-check and PCIe AER events (WHEA-Logger informational events) are the classic
precursor to a fatal 0x124. Scan a much longer window (30–90 days) than the incident window for
corrected-error frequency and trend per error source, and present it as hardware-health context:
"41 corrected memory errors on the same bank in 3 weeks" is decisive investigative guidance that
no event in the ±10-minute window can provide.

### 17. Windows Memory Diagnostic results
Value: Medium–High · Difficulty: Low · Risk: Low · User impact: High · Forensic impact: Medium–High · When: Now
`Microsoft-Windows-MemoryDiagnostics-Results/Debug` and the System-log events record whether the
user ever ran mdsched and what it found. Reports that recommend memory testing should first state
whether a test already ran and what it concluded — and the recommendation engine should stop
recommending a test that recently passed/failed.

### 18. Boot-start third-party driver inventory
Value: Medium–High · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium–High · When: Now
Read `HKLM\SYSTEM\CurrentControlSet\Services` start types (read-only, like the existing
CrashControl read) and list non-Microsoft boot/system-start drivers separately. These load before
any user session, are the usual suspects for early-boot crashes, and their start type is evidence
the current driver lists don't capture.

### 19. Recover missing install dates from uninstall-key timestamps
Value: Medium–High · Difficulty: Low–Medium (one P/Invoke) · Risk: Low · User impact: Medium–High · Forensic impact: Medium–High · When: Now
Live validation of today's installed-programs feature found 101 of 181 real entries had no
`InstallDate` value. `RegQueryInfoKey` exposes each uninstall key's last-write time — a
day-accurate proxy for install/update time that fills most of that gap. Label it distinctly
("registry key last written", not "install date") to preserve provenance honesty, and feed it into
the recent-install correlation.

### 20. Power/sleep configuration evidence
Value: Medium · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Now
Fast Startup state (`HiberbootEnabled`), hibernation availability, active power plan, and
supported sleep states (`powercfg /a` output) are read-only, cheap, and directly material for
0x9F, resume-from-sleep crashes, and "PC turned off overnight" reports. Fast Startup in particular
changes what "shutdown" means and belongs next to the boot/crash-time evidence.

### 21. CPU microcode revision + platform security state
Value: Medium · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium–High · When: Now
`HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` exposes the loaded microcode "Update
Revision"; Win32_DeviceGuard (already collected) plus Secure Boot state and Kernel DMA protection
complete the platform picture. Microcode revision is required context for 0x101/0x124/0x20001
recommendations that currently say "check microcode" without recording what is installed.

### 22. Per-command progress and stall detection in CDB execution
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: High · Forensic impact: Medium · When: Now
The script already brackets every command with `=== CEC COMMAND: … ===` echoes, so the runner can
know *which* command is executing, time each one, and distinguish "symbol download in progress"
(output advancing) from "command wedged" (no output for N minutes). Today one global timeout kills
the whole analysis and loses everything after the stall; a per-command watchdog can skip past a
single hung extension and preserve the rest.

### 23. Symbol health analyzer
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: High · Forensic impact: Medium–High · When: Now
`SymbolQuality` currently grades outcomes; it should also diagnose causes: no network vs proxy
block vs corporate TLS interception vs mismatched PDB vs timestamp mismatch, using the error text
CDB already emits plus a cheap pre-flight HTTPS reachability probe of msdl.microsoft.com. The
recommendation "rerun after symbols are available" becomes "your network blocks the symbol server
— use setting X or an offline symbol share (item 40)".

### 24. Structured `!errrec` / WHEA record decoding
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Now
The 0x124 family analyzer preserves classification text today. Parse the `!errrec` sections into
typed fields — error source type, MCA bank, MCi_STATUS bit decode (MCACOD/MSCOD, corrected vs UC,
address validity), PCIe segment/bus/device/function with vendor ID lookup — so the report can say
"uncorrectable data-cache error on core 3" or "PCIe endpoint 8086:xxxx completion timeout" with
each field traced to the record.

### 25. `.trap {P2}` restoration for 0x7F and 0x139
Value: Medium · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: High · When: Now
The 0x1AA path already proves the pattern: restore the supplied context, then re-run register and
stack commands and clearly label pre- vs post-restoration output. 0x7F (trap frame in P2 on many
subtypes) and 0x139 (trap frame P2, exception record P3 — already partially planned) deserve the
same treatment, because the post-restoration stack is the actual failing stack.

---

## Tier 2 — Next (26–55): analysis intelligence and the evidence engine

### 26. Stack execution-pattern recognizer ("what was Windows doing")
Value: Very high · Difficulty: High · Risk: Medium (patterns must stay descriptive) · User impact: Very high · Forensic impact: Very high · When: Next
A data-driven table mapping symbol sequences to activity narratives: `nt!IopfCompleteRequest` →
IRP completion; `nt!MmAccessFault`+`nt!KiPageFault` → page-fault service; `nt!PopIrpWorker` →
power transition; `fltmgr!FltpPerformPreCallbacks` → filter pre-operation; `nt!CmpCallCallBacks`
→ registry callback; `nt!PspExitThread` → thread teardown; `nt!KeBugCheckEx` preceded by
`nt!RtlpHpVsContextFree` → heap free path, etc. The report gains a sentence every engineer wants
first: "Windows was completing a storage IRP through two third-party filters when the fault
occurred." Ship the recognizer with the pattern table as reviewable data, each pattern citing the
frames that matched — never as an opaque conclusion.

### 27. Filter-sandwich detection
Value: High · Difficulty: Low–Medium · Risk: Low · User impact: High · Forensic impact: High · When: Next
A special case of item 26 worth shipping first: detect third-party modules positioned between
`nt`/`fltmgr` frames and the filesystem/storage stack, and combine with altitude classes (item 5).
"Your I/O passed through anti-virus filter X and backup filter Y at the time of failure" is the
highest-frequency actionable pattern in real-world crash triage.

### 28. Stack-coherence scoring
Value: High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
Parse frame addresses (child-SP/ret-addr columns already present in `kv` output) and verify stack
monotonicity, detect unwind breaks, count `0x`-only frames, and recognize truncated unwinds. Feed
the coherence score into `AnalysisQualityScorer` and — critically — into culprit confidence: a
module named on an incoherent stack should be capped the way poor symbols already cap attribution.

### 29. Log-odds Bayesian evidence engine
Value: High · Difficulty: Medium–High · Risk: Medium (must not fake precision) · User impact: Medium · Forensic impact: High · When: Next
`CulpritAssessmentEngine` today sums ad-hoc weights and clamps 0–100. Reframe it: each candidate
class gets a prior (third-party driver on stack ≫ Microsoft core ≈ user process), each evidence
item a likelihood ratio (verifier naming: very strong; faulting IP inside module: strong; loaded
but not on stack: ~1; corrupt pool header near module's tag: strong; stack incoherent: discounts
everything stack-derived), combined in log-odds with explicit caps for symbol quality and dump
completeness. Publish the weight table in the docs and print each candidate's ledger in expert
mode (item 49). The output vocabulary stays the existing confidence levels — the win is internal
consistency and auditability, not decimal probabilities in the report.

### 30. Explicit contradiction checks (negative evidence)
Value: High · Difficulty: Medium · Risk: Low · User impact: High · Forensic impact: High · When: Next
The report separates "what supports X"; it should equally compute "what contradicts X": headline
driver absent from the stack; bugcheck family inconsistent with the candidate's subsystem (a
display driver blamed on an NVMe-path 0x7A); crash recurring after the candidate was updated
(cross-incident); WHEA record naming a different component. Every contradiction lowers the
log-odds (item 29) and is printed in section E — this is the "What evidence contradicts that?"
question the project goals demand, made systematic.

### 31. "What would change this conclusion" generator
Value: Medium–High · Difficulty: Low–Medium · Risk: Low · User impact: High · Forensic impact: Medium–High · When: Next
For each headline and alternative, emit the concrete evidence that would confirm or refute it:
"A kernel dump (not minidump) would show whether the IRP list names the same driver"; "one more
incident with the same pool tag would raise this to High"; "a passing extended memory test would
weaken the bit-flip hypothesis". Turns every report into a next-steps plan for the engineer.

### 32. `!vm` memory-state baseline and pool-exhaustion detection
Value: Medium–High · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
Add `!vm 1` to the baseline for kernel dumps: commit charge, nonpaged/paged pool usage vs maximum,
and free PTEs. Pool exhaustion is a distinct, provable failure mode (0x41D8, 0x3F, allocation
failures behind many 0xA crashes) that the current pipeline cannot see, and it points at leakers
rather than the crashing driver — a classic misattribution the tool could uniquely avoid.

### 33. Hang-family commands: `!locks`, `!ready`, `!running -it`
Value: Medium–High · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
For 0x9F, 0x101, 0x133, and 0xEF on kernel dumps, resource-lock ownership and per-processor
running/ready threads identify *who was blocking whom* — the difference between "power IRP timed
out" and "power IRP timed out because thread T holding ERESOURCE R was stuck in driver D".
`!locks` is already planned for 0x9F only; generalize per family with small-dump guards.

### 34. IRP-chain narrative via `!irpfind` (kernel dumps)
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
For power/storage families on kernel+ dumps, enumerate pending IRPs, group by device object and
driver, and render the blocked chain (device stack from `!devstack` is already planned per
bugcheck). The 0x9F analyzer names the blocked IRP owner today; this generalizes to "and 213 other
IRPs are queued behind the same device", which is proof of a stall's blast radius.

### 35. Driver PE metadata and Authenticode chain for implicated drivers
Value: High · Difficulty: Medium · Risk: Low · User impact: High · Forensic impact: High · When: Next
For every on-stack third-party module with an on-disk file: PE link timestamp, file/product
version, signer chain via WinVerifyTrust (read-only), catalog vs embedded signature, and SHA-256.
Detects version skew (file on disk ≠ module in dump — update pending reboot!), expired/revoked
signers, and gives the report the driver-age analysis (item 36) raw material.

### 36. Driver age and staleness analysis
Value: Medium–High · Difficulty: Low (after 35) · Risk: Low · User impact: High · Forensic impact: Medium–High · When: Next
Rank implicated drivers by link-timestamp age vs the running Windows build's release date.
"This storage filter was compiled in 2019, four Windows feature updates before this build" is
legitimate, evidence-grounded investigative guidance — presented as compatibility context, not as
proof of guilt.

### 37. INF/DriverStore mapping (`.sys` → driver package → install date)
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
`driver-metadata.json` already captures INF names. Complete the chain: DriverStore package →
oem*.inf → install date → providing package (WU, OEM utility, or the installed program from item
6). This is how "the crash started after the July driver rollup" becomes provable instead of
anecdotal.

### 38. Windows Update driver-update history via the WUA API
Value: Medium–High · Difficulty: Low–Medium · Risk: Low · User impact: High · Forensic impact: High · When: Next
`Get-HotFix` (the current `windows-updates.txt`) misses feature updates and, crucially, **driver
updates delivered through Windows Update** — the single most common uncorrelated system change
before a new crash pattern. Query the WUA history COM API (read-only) for the full install
history including driver packages, and feed it into the recent-changes observation and item 37.

### 39. Pending-reboot / half-applied servicing detection
Value: Medium · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium–High · When: Next
CBS `RebootPending`, `PendingFileRenameOperations`, and WU reboot-required flags (all read-only
registry) identify the "driver updated on disk but old version still loaded" state — a real crash
cause and a certain source of misleading version evidence if not flagged (complements item 35's
skew detection).

### 40. Driver Verifier state detection + guided (manual) verifier plan
Value: Medium–High · Difficulty: Low · Risk: Low (detection) · User impact: High · Forensic impact: High · When: Next
Read `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\VerifyDrivers` (read-only) and `!verifier`
in-dump to state whether verifier was active — today a verifier-flagged dump and a normal dump
read the same in the report header. When evidence is inconclusive and a repeat crash is likely,
emit a copy-pasteable, fully explained *manual* verifier plan (flags, target drivers from the
candidate list, how to undo, the crash-loop risk) as a recommendation. The tool still never
enables anything itself.

### 41. Cross-incident matching on pool tag, address family, CPU core, IRQL
Value: High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
`IncidentFingerprint` compares code/module/process/frames today. Add: pool tag (item 12), faulting
address masked to region (same-structure-offset detection across ASLR), processor index for
0x101/0x124 (same physical core repeatedly = hardware lead), and IRQL at fault. Two crashes with
the same pool tag and offset pattern are near-certainly the same bug even when the bugcheck code
differs — exactly what "bugcheck evolution" recognition needs.

### 42. Stack-prefix similarity clustering
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: High · When: Next
N-gram/Jaccard similarity over normalized top-8 frames (module!function, displacements stripped)
groups incidents into failure clusters even across code changes and different bugcheck codes.
Renders as "this crash matches cluster #2 (4 prior incidents, first seen 12 May)" with the shared
frames listed. Foundation for crash similarity search (item 83).

### 43. Bugcheck evolution sequences (precursor chains)
Value: Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Medium–High · When: Next
Recognize documented escalation patterns across the retained history: corrected WHEA → fatal
0x124; storage latency events → 0x154/0x7A → 0xEF; TDR events → 0x116. Presented as a dated chain
with each link's evidence, it answers "how long has this been building?" — a question no
single-incident report can address.

### 44. System stability trend (descriptive, not predictive)
Value: Medium · Difficulty: Low–Medium · Risk: Medium (wording) · User impact: High · Forensic impact: Medium · When: Next
Reliability-Monitor records (`Win32_ReliabilityRecords`, read-only WMI) plus the incident history
give crashes/unexpected-shutdowns per week, trend direction, and first-seen date of the current
pattern. Strictly descriptive statistics with the methodology printed — no "failure probability"
claims (see item 92 for why).

### 45. Full LiveKernelReports family coverage
Value: Medium · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium–High · When: Next
Beyond WATCHDOG and graphics, enumerate the whole `LiveKernelReports` tree: USBHUB3/USB4,
PoW32kWatchdog, WHEA live dumps, NDIS, and vendor subfolders. These are Windows' own "something
was wrong but I survived" records; their timestamps often bracket the fatal incident and they are
analyzable with the existing CDB pipeline.

### 46. WER ReportArchive kernel-report mining
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Next
Parse `ReportArchive`/`ReportQueue` metadata for kernel and LiveKernel reports (the `Report.wer`
parser exists): submission status, bucket IDs, and OCA response IDs. A Microsoft-assigned bucket
ID connects the local incident to Microsoft's own crash-clustering — free corroboration of the
local failure-bucket evidence.

### 47. Case notes and hypothesis tracking
Value: Medium · Difficulty: Medium · Risk: Low · User impact: High (professionals) · Forensic impact: Medium · When: Next
Enterprise engineers investigate across sessions. Per-incident investigator notes, a hypothesis
list with status (open/supported/refuted plus which evidence moved it), and inclusion of the notes
in the report/ZIP turns the tool from a report generator into an investigation platform.

### 48. Report diffing between incidents
Value: Medium–High · Difficulty: Medium · Risk: Low · User impact: High · Forensic impact: High · When: Next
Structured diff of two `report.json` files: drivers added/updated/removed, programs installed
(item 6 data), firmware/OS build changes, storage-health deltas, same-cluster membership. "What
changed between the last good week and the first crash" is the question every support engineer
asks first, and all inputs are already persisted.

### 49. Per-claim citations with deep links into raw output (expert mode)
Value: High · Difficulty: Medium · Risk: Low · User impact: High · Forensic impact: High · When: Next
The `=== CEC COMMAND: … ===` markers make every parsed fact traceable to a byte range in the raw
stdout. In expert mode, every headline claim carries a link that opens the exact raw section
(HTML anchors into an embedded, escaped copy). This is the feature that makes senior engineers
trust the tool: nothing is asserted that cannot be clicked through to the debugger's own words.

### 50. Expert/basic report modes
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: High · Forensic impact: Low–Medium · When: Next
One report, two renderings: basic keeps today's A–L narrative; expert additionally shows the
evidence ledgers (item 29), coherence scores, all candidates with weights, per-command timings,
and the citation links (item 49). A toggle in the existing report toolbar, not a fork of the
writer.

### 51. NTSTATUS/HRESULT/Win32 decode expansion
Value: Medium · Difficulty: Low (data) · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Next
`TypedCodeDecoders` covers the common codes; extend to the few hundred that actually appear in
kernel crash evidence (I/O, security, memory, FS families), each with the same plain-language +
raw-value dual rendering the report already uses.

### 52. Thermal evidence
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Next
Kernel-Power thermal events, ACPI thermal-zone trips, and `MSAcpi_ThermalZoneTemperature`
(best-effort, many boards don't expose it) plus storage temperatures already collected. Thermal
correlation is a frequent hidden variable for 0x124/0x101 on gaming systems; report what is
readable, state clearly when the platform exposes nothing.

### 53. GPU/TDR deep-dive package
Value: Medium · Difficulty: Medium · Risk: Low · User impact: Medium–High · Forensic impact: Medium–High · When: Next
For 0x116/0x117/0x119 and TDR live dumps: Display-channel event 4101 history (per-GPU TDR
frequency over weeks), `TdrDelay`/`TdrLevel` registry reads (read-only — users often set these and
forget), dual-GPU/hybrid detection from the PnP inventory, and the display-driver version
timeline from item 37.

### 54. USB/PnP removal-storm correlation
Value: Low–Medium · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Next
Kernel-PnP events are collected; add recognition of device-removal/re-enumeration bursts and
surprise removals of storage/network devices in the pre-crash window — the classic hidden cause
behind 0x9F and 0xCA, and cheap to detect with data already on hand.

### 55. Network-stack family analyzer (NDIS/TCPIP/Winsock recognition)
Value: Low–Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Next
Recognize NDIS filter chains (VPNs, packet inspection) on stacks, decode `!ndiskd`-adjacent
evidence where the extension is available, and classify lightweight filter modules from the
existing driver inventory. VPN/NIC filter drivers are a top-five third-party crash family and
currently get generic treatment.

---

## Tier 3 — Later, high value (56–80): visualization, enterprise, robustness

### 56. Stack tree rendering with category coloring
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: High · Forensic impact: Low–Medium · When: Later
Self-contained SVG/CSS rendering of the parsed stack with the existing `ModuleCategory` coloring
(Microsoft core / inbox / third-party / unresolved), transition markers (user→kernel, trap,
context restore), and the pattern-recognizer narratives (item 26) as annotations.

### 57. Candidate confidence visualization
Value: Medium · Difficulty: Low · Risk: Low · User impact: Medium–High · Forensic impact: Low–Medium · When: Later
Horizontal evidence bars per candidate showing supporting vs contradicting weight (items 29/30),
rendered inline in section D/E. Makes "why is this only Moderate?" self-answering.

### 58. Timeline heat map
Value: Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
Events-per-hour density strip across the full retained history with crash markers and category
lanes (storage, WHEA, PnP, power) — the visual form of items 15/16/43's precursor story. Pure
inline SVG; no external assets, matching the report's self-contained constraint.

### 59. Executive one-pager
Value: Medium · Difficulty: Low · Risk: Low · User impact: High (managers/ticket systems) · Forensic impact: Low · When: Later
A print-oriented first page: what/when/why-confidence/next actions in five sentences, generated
from fields the report already computes. Helps the enterprise adoption path more than any single
analytic feature.

### 60. Redaction profiles for external sharing
Value: Medium–High · Difficulty: Medium · Risk: Medium (must not over-promise) · User impact: High · Forensic impact: Low · When: Later
Beyond the current account/profile redaction: hostname, IPs/MACs, volume serials/GUIDs, SIDs,
license keys in program lists — as selectable profiles ("share with vendor", "share publicly")
that rewrite the *report* while leaving raw evidence untouched locally, with an explicit manifest
of what was redacted and the standing warning that dumps themselves remain unscrubbed.

### 61. Fleet import and cross-machine comparison
Value: Medium–High · Difficulty: High · Risk: Low · User impact: High (enterprise) · Forensic impact: High · When: Later
Import many collection ZIPs; aggregate clusters (item 42) across machines: "this driver version
crashes on 14 of 200 machines, all with firmware F". This is where the tool becomes an enterprise
product; everything needed is already in `report.json`, so it is an aggregation layer, not a
schema change.

### 62. Formal schema versioning and NDJSON export
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: Medium (integrators) · Forensic impact: Low · When: Later
`SchemaVersion` exists but has no migration story. Document the schema, add explicit version bumps
with converters (CrossIncidentEngine already deserializes old reports — protect it), and add
flat NDJSON event/finding export for SIEM/BI ingestion (see item 78).

### 63. Parallel dump analysis
Value: Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Low · When: Later
`AnalyzeDirectoryAsync` is sequential; multiple dumps (minidump set + live kernel reports) can run
concurrent CDB instances against the shared symbol cache with bounded parallelism. Wall-clock
matters when a machine has 15 minidumps and item 45 adds live reports.

### 64. Background symbol prewarm
Value: Low–Medium · Difficulty: Low–Medium · Risk: Low · User impact: Medium · Forensic impact: Low · When: Later
Optionally warm the local symbol cache for the running kernel's modules while the user reads the
incident list, so first analysis isn't dominated by downloads. Read-only network fetches to the
already-used Microsoft endpoint; off by default on metered connections.

### 65. Real-dump fixture farm (NotMyFault corpus)
Value: High (engineering) · Difficulty: Medium · Risk: Low · User impact: Indirect · Forensic impact: High (correctness) · When: Later (start small Now)
The 94-byte placeholder cannot exercise the parser. Generate a corpus of genuine dumps per family
(Sysinternals NotMyFault in a disposable VM: 0xA, 0xD1, 0x50, 0xC2, 0x7F stack overflow, hang)
across dump types and debugger versions, and snapshot their CDB outputs as fixtures. Every parser
and analyzer change gets regression coverage against real material; item 2's JSON mode gets
validated per debugger version.

### 66. Parser fuzzing and hostile-output hardening
Value: Medium · Difficulty: Medium · Risk: Low · User impact: Indirect · Forensic impact: Medium (trust) · When: Later
Dump-derived text is untrusted input. Fuzz `DebuggerOutputParser` with mutated fixtures (truncated
UTF-8, gigantic lines, section-marker collisions with `=== CEC COMMAND:` appearing inside dump
strings, ANSI escapes) and assert graceful preservation. The report writer already HTML-encodes;
the parser deserves the same adversarial standard.

### 67. Debugger-version compatibility matrix
Value: Medium · Difficulty: Low (with 65) · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
Pin fixtures per WinDbg/CDB release, record which parsed fields degrade per version, and surface
"your debugger is version X; field Y is unavailable before Z" in the report's limitations instead
of silently reporting less.

### 68. ARM64 validation pass
Value: Medium · Difficulty: Medium · Risk: Low · User impact: Medium (growing) · Forensic impact: Medium · When: Later
`DetectArchitecture` recognizes ARM64 text, but the command planner, address canonicality rules,
trap/context handling, and `FindCdb` path discovery (ARM64 kit layout) are x64-shaped. Windows-on-
ARM crash analysis is where commercial tools are weakest — an achievable differentiation.

### 69. Registry & Configuration Manager subsystem recognition
Value: Low–Medium · Difficulty: Low–Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
Recognize `CmpCallCallBacks`/registry-callback stacks (AV and policy products register these),
0x51 REGISTRY_ERROR knowledge, and hive-corruption events — completing the subsystem coverage the
prompt lists that Tier 1/2 items don't already touch (Object Manager and Executive patterns land
with item 26's table; Win32k/CSRSS/session with item 26 + 0x1CA/0x119 knowledge in item 3).

### 70. Hypervisor/VBS environment analyzer expansion
Value: Low–Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
The hypervisor family analyzer exists; add Hyper-V worker/VMBus event channels, nested-virt
detection (WSL2/VBS + third-party hypervisors like VirtualBox/VMware co-resident — a documented
conflict source), and HVCI enforcement state correlation for 0x18B/0x20001.

### 71. Pool-usage trend across incidents (leak narrative)
Value: Low–Medium · Difficulty: Low (after 32/41) · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
With `!vm` snapshots retained per incident, rising nonpaged-pool usage across weeks with the same
top pool tag is a provable leak narrative — "pool tag Xyz grew 40 MB→900 MB across 5 incidents" —
that no single dump can show.

### 72. Storage device end-to-end mapping
Value: Low–Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
Join the layers that are currently separate lists: physical disk → partition → volume → what lives
on it (pagefile? dump target? system volume?) so storage warnings can say "the disk with the
failing SMART counters holds your page file and dump target" — which changes both risk and the
dump-absence analysis (item 13).

### 73. Content-security hardening of the HTML report
Value: Low · Difficulty: Low · Risk: Low · User impact: Low (trust) · Forensic impact: Low · When: Later
Add a restrictive `<meta http-equiv="Content-Security-Policy">`, explicit charset/no-referrer
tags, and keep the zero-external-asset rule as a tested invariant. Cheap insurance for a file
that gets emailed around and opened everywhere.

### 74. Accessibility and print stylesheet
Value: Low · Difficulty: Low · Risk: Low · User impact: Medium · Forensic impact: None · When: Later
Text labels alongside all color-coded origin badges (partially present), semantic landmarks,
`prefers-color-scheme` support, and a print stylesheet so the existing print-to-PDF flow paginates
section-per-page cleanly.

### 75. Localization framework
Value: Low · Difficulty: Medium · Risk: Low · User impact: Medium (non-English fleets) · Forensic impact: None · When: Later
The educational text is a differentiator; extract report strings into resource tables. Note the
collectors must also stop assuming English event rendering (a current hidden assumption — several
heuristics match provider *names*, which are stable, but some match message text like "error"/
"failed" in `SignificantTimeline`, which is locale-fragile; fix that matching as part of this).

### 76. Headless CLI mode with machine exit codes
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: High (enterprise/automation) · Forensic impact: Low · When: Later
The WorkflowHarness is 90% of this: a supported `collect --incident latest --output X --json`
verb with documented exit codes lets helpdesk tooling, RMM platforms, and scheduled tasks run
collections. Enterprise adoption is usually decided by whether this exists.

### 77. MSI/winget packaging + Authenticode-signed helper
Value: Medium · Difficulty: Medium (process, cert) · Risk: Low · User impact: High · Forensic impact: None · When: Later
The README already flags the unsigned helper. Signing, an MSI with per-machine install, and a
winget manifest are prerequisites for Dell/Lenovo/HP-grade distribution and for the UAC prompt to
look trustworthy.

### 78. SIEM/ITSM export connectors
Value: Low–Medium · Difficulty: Medium · Risk: Low · User impact: Medium (enterprise) · Forensic impact: Low · When: Later
On top of item 62's NDJSON: Windows Event Log write-back of a summary record (one write, clearly
disclosed, optional), Sentinel/Splunk-friendly flat export, and ServiceNow attachment formatting
of the one-pager (item 59).

### 79. Knowledge-pack update channel (signed)
Value: Medium–High · Difficulty: Medium · Risk: Medium (supply chain — must be signed + reviewable) · User impact: High · Forensic impact: Medium · When: Later
Items 3/4/7/12's data tables age. Distribute them as versioned, signed JSON packs the app can
refresh independently of binaries, with the active pack version stamped into every report for
reproducibility. Deliberately data-only — no executable plugin model, which would be an
unjustifiable attack surface for a forensic tool.

### 80. Public evidence-model whitepaper
Value: Medium · Difficulty: Low–Medium · Risk: Low · User impact: Medium (trust/adoption) · Forensic impact: Medium (methodological scrutiny) · When: Later
Document the confidence taxonomy, the weight tables (item 29), the association veto rules, and the
crash-time hierarchy as a citable methodology paper in `docs/`. Enterprise evaluators and
Microsoft-adjacent engineers will test the tool against its own published rules — exactly the
scrutiny that makes it credible.

---

## Tier 4 — Ambitious / gated (81–100): external data, opt-in capture, ML-adjacent

### 81. Curated Microsoft known-issue/KB regression matching
Value: Medium–High · Difficulty: High (data curation forever) · Risk: Medium (stale data misleads) · User impact: High · Forensic impact: Medium–High · When: Later
Map failure buckets/signatures to a curated table of Microsoft-documented known issues and the KB
that fixed them ("this bucket matches the documented storahci regression fixed in KB50xxxxx —
your build predates it"). High payoff, but only as good as its maintenance; ship via item 79 with
per-entry citations and dates.

### 82. Intel/AMD errata and microcode advisory matching
Value: Medium · Difficulty: High · Risk: Medium · User impact: Medium · Forensic impact: Medium–High · When: Later
With CPUID + loaded microcode revision (item 21), match against curated public errata/AGESA
advisories for the 0x101/0x124/0x20001 families. Guidance-only ("errata XY affects this stepping;
fixed in microcode ≥Z; you have W"), never attribution.

### 83. Crash similarity search
Value: Medium · Difficulty: Medium (after 42) · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
Free-text and signature search across the retained report archive ("show every incident with this
pool tag / driver / cluster"), locally indexed. The investigator-facing face of items 41/42.

### 84. Subsystem probability graphs
Value: Low–Medium · Difficulty: Medium · Risk: Medium (visual overclaiming) · User impact: Medium · Forensic impact: Low–Medium · When: Later
Visualize the evidence-engine posterior across subsystems (storage/graphics/memory/power/…)
per incident and across the history. Only after item 29 exists — a graph of today's ad-hoc scores
would lend false authority (the exact failure mode the project goals prohibit).

### 85. Memory-corruption visualization
Value: Low–Medium · Difficulty: Medium–High · Risk: Low · User impact: Medium · Forensic impact: Medium · When: Later
Render `!chkimg` deltas (item 9) and pool-corruption layouts (item 12) as byte-level diagrams —
which bits flipped, where the overrun ran, what the fill pattern was. Strong educational and
report-credibility value once those evidence sources exist.

### 86. IRP flow visualization
Value: Low–Medium · Difficulty: Medium · Risk: Low · User impact: Medium · Forensic impact: Low–Medium · When: Later
Device-stack diagrams with the blocked IRP's position and ownership timeline for the 0x9F/storage
families (items 33/34 supply the data). Answers "where exactly is it stuck" at a glance.

### 87. Object reference and handle evidence (verifier-gated)
Value: Low–Medium · Difficulty: High · Risk: Low · User impact: Low–Medium · Forensic impact: Medium · When: Later
`!obtrace`/handle-leak analysis only produces evidence when tracking was enabled pre-crash, so
this pairs with item 40's guided verifier plan: detect when tracking data exists in the dump and
mine it; state its absence honestly otherwise.

### 88. Driver reputation scoring
Value: Medium · Difficulty: High · Risk: High (fairness/defamation-adjacent) · User impact: Medium · Forensic impact: Medium · When: Later
Aggregate local fleet history (item 61) + curated KB (item 7) into a per-driver-version stability
summary. Must remain descriptive counts with denominators ("involved in 3 of 4 incidents on this
machine; on-stack in 2"), never a publishable "badness score" — the credibility cost of one unfair
number exceeds the feature's value.

### 89. Opt-in ETW flight recorder
Value: Medium–High · Difficulty: High · Risk: Medium–High (violates read-only default; perf cost) · User impact: High (for reproducible crashers) · Forensic impact: High · When: Later
A circular kernel ETW session (autologger) capturing IRQL/IRP/power/storage precursors that
survive to the next boot. It is the single biggest evidence upgrade for intermittent crashes — and
it changes system state, so it must be an explicit, reversible, loudly-labelled opt-in with the
removal path shown before enabling, in its own UI area, never a default or a recommendation
one-click.

### 90. On-demand live kernel snapshot (opt-in)
Value: Medium · Difficulty: Medium–High · Risk: Medium (elevation; writes a dump) · User impact: Medium–High (freeze investigations) · Forensic impact: High for hangs · When: Later
"Capture the current kernel state now" via the documented live-dump API for investigating
freezes/slowdowns that never bugcheck. Requires elevation through a second allow-listed helper
operation (the IPC protocol was built to be extended exactly this carefully) and clear disclosure
that a dump file is written.

### 91. Freeze/no-dump incident mode
Value: Medium · Difficulty: Medium · Risk: Low · User impact: High (freezes are common) · Forensic impact: Medium · When: Later
A first-class incident kind for hard freezes: evidence = absence patterns (log silence interval),
last-written events before silence, power button reset signatures, plus items 89/90 when opted in.
Today `PowerLossOrFreeze` exists as a detection; give it its own analyzer and report narrative
("last evidence of life", "what the system was doing when logging stopped").

### 92. Predictive failure probability — deliberately constrained
Value: Low (honest) · Difficulty: Very high (data) · Risk: High · User impact: Misleading if done badly · Forensic impact: Negative if done badly · When: Later/never without fleet-scale data
Stated plainly because the brief asks: per-machine failure *prediction* requires labelled
fleet-scale outcome data this project does not have, and a fabricated probability would violate
the project's core promise. Until real data exists (item 61 at scale), ship item 44's descriptive
trends and item 16's precursor analysis — which deliver the same user value truthfully.

### 93. Machine health score (composite, descriptive)
Value: Low–Medium · Difficulty: Medium · Risk: Medium (oversimplification) · User impact: Medium · Forensic impact: Low · When: Later
If shipped at all: a transparent checklist score (storage health, WHEA corrected rate, crash
frequency trend, driver staleness, pending reboots) where every deducted point links to its
evidence — a summary of facts, not a prediction (contrast item 92).

### 94. Windows Insider/pre-release regression detection
Value: Low · Difficulty: Low · Risk: Low · User impact: Low–Medium · Forensic impact: Low–Medium · When: Later
Detect Insider/preview builds and flight ring (read-only registry), flag that crash patterns on
pre-release builds may be OS regressions, and weight the knowledge-base matching (item 81)
accordingly.

### 95. Online advisory lookups (KB/firmware/CVE) — opt-in
Value: Medium · Difficulty: Medium · Risk: Medium (privacy: queries reveal machine details) · User impact: Medium–High · Forensic impact: Medium · When: Later
Live lookups of vendor firmware advisories and driver CVEs for the *implicated* components only,
strictly opt-in with a preview of exactly what identifiers would be sent, and offline knowledge
packs (item 79) as the default path.

### 96. DbgEng COM hosting investigation
Value: Medium · Difficulty: High · Risk: Medium (deployment/licensing complexity) · User impact: Low directly · Forensic impact: Medium (robustness) · When: Later
Hosting `dbgeng.dll` directly would eliminate text scraping, give per-command control (item 22
free), and expose the data model. Investigate seriously *after* item 2 (which captures most of the
benefit at a fraction of the cost) — and keep CDB text as the evidentiary record either way, since
raw debugger output is what third parties can independently verify.

### 97. Multi-user/terminal-server awareness
Value: Low · Difficulty: Low–Medium · Risk: Low · User impact: Low (but blocking where it matters) · Forensic impact: Low · When: Later
HKCU program inventory, profile redaction, and output paths currently assume the interactive user
is the only user. Enumerate loaded user hives for inventory completeness (read-only) and redact
all local account names, not just the current one — required for RDS/shared-workstation fleets.

### 98. Report language QA harness
Value: Low–Medium · Difficulty: Low · Risk: Low · User impact: Medium (trust) · Forensic impact: Medium (epistemics) · When: Later
Automate the "remove every statement that could overstate certainty" review: a test that scans
generated reports for unhedged causal verbs ("caused", "due to", "faulty X") outside
debugger-quoted text and fails when new copy violates the evidence-origin rules. Locks in the
project's key differentiator against regression as contributors add features.

### 99. Crash-clustering research corpus (shareable, consented)
Value: Low–Medium · Difficulty: High (consent, anonymization) · Risk: Medium–High (privacy) · User impact: Low near-term · Forensic impact: High long-term · When: Later
An opt-in, aggressively-scrubbed signature-sharing scheme (fingerprints only — no dumps, no
identifiers) so instances can learn cluster prevalence collectively. This is the only honest path
to items 88/92 ever becoming real; gate it behind published anonymization methodology (item 80).

### 100. Full A11y/security/privacy third-party audit before enterprise launch
Value: Medium · Difficulty: Medium (external) · Risk: Low · User impact: High (procurement gate) · Forensic impact: Low · When: Later
Enterprises procuring a tool that reads crash dumps (which contain credentials and document
fragments — the README already warns) will require an independent security review of the helper
IPC, the parser attack surface (item 66), and the redaction claims (item 60). Scheduling it as a
roadmap item makes the preceding hardening items coherent.

---

## Incorrect or fragile assumptions found during this review

These are defects-in-waiting rather than features; several are fixed by items above.

1. **`Get-HotFix` is treated as "Windows update history"** — it misses feature updates and WU
   driver updates entirely (items 37/38). The 48-hour recent-changes observation is blind to the
   most common relevant change class.
2. **`SignificantTimeline` filters on English words** ("error", "failed", "unexpected") in
   rendered messages — locale-fragile; provider/ID/level-based filtering is stable (item 75).
3. **`DebuggerLocator.FindCdb` assumes the x64 kit path** — misses ARM64 kits and non-default
   install roots; `CDB_PATH` is the only escape hatch (item 68).
4. **`DumpClassifier` filename heuristic (`Mini*`)** — self-heals via `Refine`, but the initial
   classification drives command planning (`!stacks 2` exclusion) *before* refinement can occur,
   so a misnamed kernel dump gets minidump-grade commands (fix: plan after a first `vertarget`
   probe, or re-plan when `Refine` upgrades the type).
5. **`ParseUnsupportedCommands` substring matching** ("unable to read") can mark a command
   unsupported when the *dump content* legitimately contains that phrase in an error field —
   marker-scoped structured checks would be safer (items 2/66).
6. **One global CDB timeout** conflates "slow symbol download" with "hung extension" and discards
   completed output on kill (item 22).
7. **Uptime from `Environment.TickCount64`** counts time asleep differently than wall-clock boot
   time on modern standby machines; boot-time derivation can disagree with the event-log boot
   record (minor, but crash-time logic leans on boot boundaries).
8. **Redaction covers the current account only** — machine name, other local accounts, and IPs in
   event messages pass through (items 60/97).
9. **The registry `InstallDate` value is frequently absent** (validated live: 101 of 181 entries)
   — item 19 recovers most of these from key last-write times.
10. **Symbol server access assumes direct HTTPS** — corporate proxies/TLS inspection produce
    confusing "poor symbols" verdicts with no diagnosis (item 23).

## Suggested sequencing

- **Milestone 1 (evidence without new dependencies):** 1, 3, 4, 5, 13, 14, 15, 16, 17, 18, 19, 20, 21.
- **Milestone 2 (debugger depth):** 2, 8, 9, 10, 11, 12, 22, 23, 24, 25.
- **Milestone 3 (intelligence):** 6, 7, 26, 27, 28, 29, 30, 31, plus fixture farm (65) started in parallel.
- **Milestone 4+:** remaining Tier 2, then enterprise/visualization tiers as adoption demands.

Every milestone keeps the existing invariants testable: deterministic CDB scripts, evidence-origin
labelling on every new sentence, association vetoes untouched, and the read-only guarantee intact.
