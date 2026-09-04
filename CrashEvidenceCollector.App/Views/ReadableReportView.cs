namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// The readable report: an action row over the rendered report.
///
/// The report itself is hosted in <see cref="ReportView"/>, which owns a WebView2
/// and treats report content as untrusted input while still offering copy and
/// selection-driven web search from its context menu.
/// </summary>
public partial class ReadableReportView : UserControl
{
    private readonly ReportView _report = new() { Dock = DockStyle.Fill };

    /// <summary>Selected text the reader asked to look up; true when a new tab was requested.</summary>
    public event Action<string, bool>? SearchRequested;
    public event EventHandler? CopyTextRequested;
    public event EventHandler? CopyJsonRequested;
    public event EventHandler? SaveTextRequested;
    public event EventHandler? SearchClipboardRequested;

    public ReadableReportView()
    {
        InitializeComponent();
        reportHost.Controls.Add(_report);
        _report.SearchRequested += (text, newTab) => SearchRequested?.Invoke(text, newTab);
        copyTextButton.Click += (s, e) => CopyTextRequested?.Invoke(s, e);
        copyJsonButton.Click += (s, e) => CopyJsonRequested?.Invoke(s, e);
        saveTextButton.Click += (s, e) => SaveTextRequested?.Invoke(s, e);
        searchButton.Click += (s, e) => SearchClipboardRequested?.Invoke(s, e);
        printButton.Click += (_, _) => Print();
        SetActionsEnabled(false);
        _report.ShowHtml(WelcomeHtml);
    }

    /// <summary>
    /// Shown before any report exists, so the tab explains itself instead of
    /// presenting a blank page.
    /// </summary>
    private const string WelcomeHtml = """
<!doctype html><html><head><meta charset='utf-8'><style>
body{font-family:'Segoe UI',sans-serif;background:#f4f7fb;color:#18202b;margin:0;padding:40px;line-height:1.5}
.card{background:white;border:1px solid #dce3ec;border-radius:12px;padding:22px 26px;max-width:720px}
h2{color:#17375e;margin-top:0}ol{padding-left:20px}li{margin:6px 0}
.meta{color:#5b6675;margin-top:16px}
</style></head><body><div class='card'>
<h2>No report yet</h2>
<ol>
<li>Choose an incident in the list on the left.</li>
<li>Press <strong>Collect evidence</strong>, and approve the administrator prompt if one appears &mdash;
declining it means protected crash dumps cannot be copied and the report will contain no dump analysis.</li>
<li>The finished report appears here, and is also written to your output folder as HTML, JSON, text and a ZIP archive.</li>
</ol>
<p class='meta'>Once a report is loaded you can select any text, right-click, and search the web for it
without leaving the application.</p>
</div></body></html>
""";

    /// <summary>Report actions stay disabled until a report exists to act on.</summary>
    public void SetActionsEnabled(bool enabled)
        => copyTextButton.Enabled = copyJsonButton.Enabled = saveTextButton.Enabled = printButton.Enabled = enabled;

    public void ShowReportFile(string path) => _report.ShowReportFile(path);
    public void ShowHtml(string html) => _report.ShowHtml(html);

    /// <summary>Opens the print dialog; false when the view is not ready yet.</summary>
    public bool Print() => _report.Print();

    /// <summary>The underlying report view, for callers that need its navigation events.</summary>
    public ReportView ReportSurface => _report;
}
