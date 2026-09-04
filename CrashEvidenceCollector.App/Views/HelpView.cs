namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// Built-in documentation, rendered in the same locked report surface used for
/// crash reports. The content is embedded rather than shipped as a file, so help
/// is always present and always matches the running build.
/// </summary>
public sealed class HelpView : UserControl
{
    private readonly ReportView _view = new() { Dock = DockStyle.Fill };
    private bool _loaded;

    public HelpView()
    {
        // AutoScaleMode.None: the shell's VStack/ARP engine owns scaling.
        AutoScaleMode = AutoScaleMode.None;
        Dock = DockStyle.Fill;
        Controls.Add(_view);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_loaded || DesignMode) return;
        _loaded = true;
        _view.ShowHtml(Html);
    }

    private const string Html = """
<!doctype html><html><head><meta charset='utf-8'><title>Using Crash Evidence Collector</title><style>
body{font-family:'Segoe UI',sans-serif;background:#f4f7fb;color:#18202b;margin:0;line-height:1.5}
header{background:#14233b;color:white;padding:24px 5vw}
main{max-width:900px;margin:20px auto;padding:0 24px 60px}
.card{background:white;border:1px solid #dce3ec;border-radius:12px;padding:18px 22px;margin:14px 0}
h1{margin:0 0 6px}h2{color:#17375e;margin-top:0}h3{margin-bottom:4px}
code{font-family:Consolas,monospace;background:#eef2f8;padding:1px 5px;border-radius:4px}
.tip{background:#eaf1fa;border-left:5px solid #2869be;padding:10px 14px;margin:10px 0;border-radius:0 8px 8px 0}
.warn{background:#fff8e8;border-left:5px solid #d48a00;padding:10px 14px;margin:10px 0;border-radius:0 8px 8px 0}
table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #e4e8ee;text-align:left;padding:7px;vertical-align:top}
th{color:#44566d}ul{padding-left:22px}li{margin:4px 0}
</style></head><body>
<header><h1>Using Crash Evidence Collector</h1><div>Collect &middot; Correlate &middot; Explain</div></header><main>

<section class='card'>
<h2>What this tool does</h2>
<p>It gathers the evidence Windows keeps about a crash &mdash; event records, crash dumps, hardware
inventory, storage health &mdash; analyses the dump with the Microsoft debugger, and writes a report
that separates what was <em>observed</em> from what is <em>inferred</em>.</p>
<p><strong>It never changes your system.</strong> No drivers, services, BIOS settings, registry
settings, page-file or dump configuration are modified, and Driver Verifier is never enabled. The
single exception is opt-in: the &ldquo;Start with Windows&rdquo; setting writes one startup entry for
the app itself, and unticking it removes that entry.</p>
</section>

<section class='card'>
<h2>The basic workflow</h2>
<ol>
<li><strong>Pick an incident</strong> in the list on the left. Use <em>Show</em> to widen or narrow the
date range and the type box to filter what kinds of incident appear.</li>
<li><strong>Collect Evidence.</strong> If Windows denies access to protected crash dumps, approve the
administrator prompt &mdash; declining it means the report has no dump analysis at all.</li>
<li><strong>Read the report</strong> on the <em>Readable report</em> tab. Every collection also writes
<code>report.html</code>, <code>report.json</code>, <code>report.txt</code>, a <code>Raw\</code> folder
and a ZIP archive to your output folder.</li>
</ol>
<div class='tip'><strong>Tip:</strong> select any text in the report, right-click it, and choose
<em>Search the web</em> to look it up in a browser tab without leaving the app.</div>
</section>

<section class='card'>
<h2>The tabs</h2>
<table>
<tr><th>Metrics &amp; trends</th><td>Counts, a 30-day daily breakdown, incident mix and the most
repeated stop codes. <strong>Click any day in the bar chart</strong> to see exactly which incidents
happened that day, including which applications failed.</td></tr>
<tr><th>Selected incident</th><td>Crash time, how that time was established, next boot, and the
plain-English meaning of the stop code.</td></tr>
<tr><th>Live monitor</th><td>Background sampling every 2 seconds: CPU utility and effective clock,
temperature, memory load and commit charge, plus WHEA and thermal events captured as they happen.
Samples are written to disk immediately, so the minutes <em>before</em> a crash survive it and are
attached to the next report.</td></tr>
<tr><th>Evidence status</th><td>Every evidence category and whether it succeeded, warned or failed
&mdash; so you can see what is missing, not just what was found.</td></tr>
<tr><th>Raw debugger output</th><td>The debugger's unmodified output, so any conclusion in the report
can be checked against its source.</td></tr>
<tr><th>Readable report</th><td>The full A&ndash;L analysis. Copy it as text or JSON, save it, or print
to PDF.</td></tr>
<tr><th>Web tabs</th><td>Opened with the <strong>+</strong> button or from a report search. Browsing
uses its own profile and never shares storage with rendered evidence.</td></tr>
</table>
</section>

<section class='card'>
<h2>Reading a report honestly</h2>
<p>The report deliberately keeps these apart:</p>
<ul>
<li><strong>Directly observed</strong> &mdash; facts from the dump or the event log.</li>
<li><strong>Debugger conclusions</strong> &mdash; what the Microsoft debugger decided.</li>
<li><strong>Analyzer inference</strong> &mdash; this tool's reasoning.</li>
<li><strong>Possible correlation</strong> &mdash; things that co-occurred, which is not causation.</li>
<li><strong>Unknown</strong> &mdash; stated plainly rather than guessed at.</li>
</ul>
<div class='warn'><strong>Two traps worth knowing.</strong> The <em>active process</em> in a dump is
usually just what was running when Windows detected the fault, not the culprit. And a Microsoft
component such as <code>ntoskrnl.exe</code> appearing at the crash location normally means it
<em>detected</em> the problem, not that it caused it.</div>
<p>Confidence is separate from analysis quality: a complete dump with good symbols can still leave the
cause undetermined, and the report will say so rather than name a plausible-looking suspect.</p>
</section>

<section class='card'>
<h2>Summary report</h2>
<p>Use the <em>Summary report</em> page for a date range rather than a single incident. It lists every
incident in the range, highlights repeated stop codes, modules and failure buckets, and shows any
incidents that were never collected. Filters let you narrow it &mdash; for a hardware warranty claim,
<em>Kernel crashes only</em> removes application noise. A filtered summary always states what it
excluded, so it can be shared as evidence without hiding anything.</p>
</section>

<section class='card'>
<h2>Settings worth knowing</h2>
<ul>
<li><strong>Always run as administrator</strong> &mdash; one prompt at startup instead of one per
collection, so a dismissed prompt can never cost you the dump analysis.</li>
<li><strong>Resume monitoring on start</strong> and <strong>Start with Windows</strong> &mdash; together
these keep the flight recorder running across reboots.</li>
<li><strong>Debugger timeout</strong> &mdash; raise it if analysis is cut short while symbols download
for the first time.</li>
<li><strong>Include full MEMORY.DMP</strong> &mdash; off by default. Dumps can contain passwords and
document fragments, so inspect an archive before sharing it.</li>
</ul>
</section>

<section class='card'>
<h2>Limitations</h2>
<ul>
<li>A power cut can prevent Windows writing any dump or final event. Missing evidence cannot be
reconstructed.</li>
<li>Public symbols do not resolve private symbols for third-party drivers, so some frames stay
unresolved even with a healthy symbol setup.</li>
<li>A small dump may lack the memory, IRPs or context needed to identify a culprit. The report lowers
its confidence and recommends better collection rather than guessing.</li>
<li>Windows APIs do not expose every vendor NVMe/ATA health field, and raw SMART values are
vendor-specific: a non-zero counter is a warning, not an attribution.</li>
</ul>
</section>

</main></body></html>
""";
}
