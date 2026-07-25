using System.Diagnostics;
using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App;

public sealed class MainForm : Form
{
    private readonly Color _nav = Color.FromArgb(20, 35, 59);
    private readonly Color _accent = Color.FromArgb(40, 105, 190);
    private readonly TabControl _pages = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons, ItemSize = new Size(0, 1), SizeMode = TabSizeMode.Fixed };
    private readonly ListView _timeline = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None };
    private readonly ComboBox _timelineRange = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly NumericUpDown _customRangeHours = new() { Minimum = 0.5m, Maximum = 720, Value = 2, Increment = 0.5m, DecimalPlaces = 1, Width = 70 };
    private readonly ComboBox _timelineType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Label _timelineCount = new() { AutoSize = true, ForeColor = Color.FromArgb(75, 85, 99), Padding = new Padding(8, 7, 0, 0) };
    private readonly ListView _evidence = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BorderStyle = BorderStyle.None };
    private readonly Label _headline = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 18), Text = "Checking recent crash evidence…" };
    private readonly Label _incidentDetail = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(75, 85, 99) };
    private readonly Label _modeBanner = new() { Dock = DockStyle.Top, Height = 34, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(255, 238, 184), ForeColor = Color.FromArgb(95, 63, 0), Visible = false };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 8 };
    private readonly Label _progressText = new() { Dock = DockStyle.Bottom, Height = 32, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 85, 99) };
    private readonly Button _collect = PrimaryButton("Collect Evidence");
    private readonly Button _cancel = SecondaryButton("Cancel");
    private readonly Button _openOutput = SecondaryButton("Open output folder");
    private readonly Button _copySummary = SecondaryButton("Copy summary");
    private readonly Button _copyReport = SecondaryButton("Copy report text");
    private readonly Button _copyJson = SecondaryButton("Copy JSON");
    private readonly Button _saveText = SecondaryButton("Save text as…");
    private readonly Button _printPdf = SecondaryButton("Print / save PDF");
    private readonly Button _history = SecondaryButton("Review past history");
    private readonly WebBrowser _reportViewer = new() { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };
    private readonly RichTextBox _rawDebuggerViewer = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9), BackColor = Color.FromArgb(17, 28, 45), ForeColor = Color.FromArgb(220, 232, 248), WordWrap = false };
    private readonly TabControl _workspaceTabs = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _before = new() { Minimum = 1, Maximum = 1440, Value = 10, Width = 90 };
    private readonly NumericUpDown _after = new() { Minimum = 1, Maximum = 1440, Value = 5, Width = 90 };
    private readonly NumericUpDown _debuggerTimeout = new() { Minimum = 15, Maximum = 1800, Value = 180, Increment = 15, Width = 90 };
    private readonly CheckBox _analyzeDumps = new() { Text = "Analyse copied crash dumps with the Microsoft debugger", AutoSize = true, Checked = true };
    private readonly CheckBox _fullDump = new() { Text = "Include full MEMORY.DMP in ZIP (may be very large and sensitive)", AutoSize = true };
    private readonly CheckBox _redact = new() { Text = "Redact Windows account name and profile path in reports", AutoSize = true, Checked = true };
    private readonly CheckBox _testMode = new() { Text = "Use local test-data mode", AutoSize = true };
    private readonly TextBox _output = new() { Width = 530 };
    private readonly TextBox _testData = new() { Width = 530 };
    private readonly StructuredLog _log = new();
    private IReadOnlyList<Incident> _incidents = [];
    private HashSet<string> _newIncidentIds = [];
    private CollectionOptions _settings = new();
    private CancellationTokenSource? _collectionCts;
    private CollectionResult? _lastResult;

    public MainForm()
    {
        Text = "Crash Evidence Collector"; MinimumSize = new Size(1050, 680); Size = new Size(1240, 790); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(246, 248, 251);
        Controls.Add(_pages); Controls.Add(BuildNavigation()); Controls.Add(_modeBanner);
        _pages.TabPages.Add(BuildWorkspace()); _pages.TabPages.Add(BuildSettings());
        _timeline.Columns.Add("When", 180); _timeline.Columns.Add("Type", 150); _timeline.Columns.Add("Code / incident", 330); _timeline.Columns.Add("Plain-English meaning", 560); _timeline.Columns.Add("Source", 300);
        _timeline.SelectedIndexChanged += (_, _) => UpdateSelection(); _timeline.DoubleClick += (_, _) => UpdateSelection();
        _timelineRange.Items.AddRange(["Last 30 minutes", "Last hour", "Last 2 hours", "Last 3 hours", "Last 6 hours", "Last 12 hours", "Last 24 hours", "Last 3 days", "Last 7 days", "Last 30 days", "Custom hours"]);
        _timelineRange.SelectedIndex = 1;
        _timelineRange.SelectedIndexChanged += (_, _) => PopulateTimeline();
        _customRangeHours.ValueChanged += (_, _) => { if (_timelineRange.SelectedIndex != 10) _timelineRange.SelectedIndex = 10; else PopulateTimeline(); };
        _timelineType.Items.AddRange(["System crashes and shutdowns", "All incident types", "Application crashes only", "Hardware errors only"]); _timelineType.SelectedIndex = 0;
        _timelineType.SelectedIndexChanged += (_, _) => PopulateTimeline();
        _evidence.Columns.Add("Category", 245); _evidence.Columns.Add("Status", 110); _evidence.Columns.Add("Details", 590);
        _collect.Click += async (_, _) => await CollectAsync(); _cancel.Click += (_, _) => _collectionCts?.Cancel(); _openOutput.Click += (_, _) => OpenOutput(); _copySummary.Click += (_, _) => CopySummary();
        _copyReport.Click += async (_, _) => await CopyFileContentsAsync(_lastResult?.TextPath);
        _copyJson.Click += async (_, _) => await CopyFileContentsAsync(_lastResult?.JsonPath);
        _saveText.Click += (_, _) => SaveReportText();
        _printPdf.Click += (_, _) => PrintOrSavePdf();
        _reportViewer.DocumentCompleted += (_, _) => _printPdf.Enabled = _lastResult is not null;
        _history.Click += (_, _) => _timelineRange.SelectedIndex = 9;
        Shown += async (_, _) => await InitializeAsync(); FormClosing += (_, _) => _collectionCts?.Cancel();
    }

    private Control BuildNavigation()
    {
        var panel = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = _nav, Padding = new Padding(14, 22, 14, 14) };
        var title = new Label { Dock = DockStyle.Top, Height = 70, Text = "Crash Evidence\nCollector", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15) }; panel.Controls.Add(title);
        var buttons = new[] { ("Crash workspace", 0), ("Settings", 1) };
        foreach (var (text, page) in buttons.Reverse()) { var button = NavButton(text); button.Click += (_, _) => _pages.SelectedIndex = page; panel.Controls.Add(button); }
        var safety = new Label { Dock = DockStyle.Bottom, Height = 92, Text = "READ-ONLY\nNever changes drivers, services, registry settings or crash configuration.", ForeColor = Color.FromArgb(180, 198, 222), Font = new Font("Segoe UI", 8.5f) }; panel.Controls.Add(safety);
        return panel;
    }

    /// <summary>
    /// Builds the primary one-screen workflow. The outer splitter keeps the incident
    /// timeline visible while the inner splitter places actions above evidence/report
    /// tabs. Users can resize each area to suit the amount of detail they want to read.
    /// </summary>
    private TabPage BuildWorkspace()
    {
        var page = Page();
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
        var workspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Size = new Size(1000, 650),
            SplitterDistance = 485,
            SplitterWidth = 7,
            Panel1MinSize = 330,
            Panel2MinSize = 360,
            BackColor = Color.FromArgb(220, 226, 234)
        };

        var timelinePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };
        var timelineTitle = new Label { Dock = DockStyle.Top, Height = 40, Text = "Incident timeline", Font = new Font("Segoe UI Semibold", 14) };
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        filters.Controls.Add(new Label { Text = "Show", AutoSize = true, Padding = new Padding(0, 7, 6, 0) });
        filters.Controls.Add(_timelineRange);
        filters.Controls.Add(new Label { Text = "Custom:", AutoSize = true, Padding = new Padding(8, 7, 2, 0) });
        filters.Controls.Add(_customRangeHours);
        filters.Controls.Add(new Label { Text = "hours", AutoSize = true, Padding = new Padding(0, 7, 8, 0) });
        filters.Controls.Add(_timelineType);
        filters.Controls.Add(_timelineCount);
        timelinePanel.Controls.Add(_timeline);
        timelinePanel.Controls.Add(filters);
        timelinePanel.Controls.Add(timelineTitle);
        workspace.Panel1.Padding = new Padding(0, 0, 4, 0);
        workspace.Panel1.Controls.Add(timelinePanel);

        var detailWorkspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(500, 600),
            SplitterDistance = 250,
            SplitterWidth = 7,
            Panel1MinSize = 225,
            Panel2MinSize = 220,
            BackColor = Color.FromArgb(220, 226, 234)
        };

        var selectedPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 82, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        actions.Controls.AddRange([_collect, _cancel, _history, _openOutput, _copySummary]);
        _cancel.Enabled = false; _openOutput.Enabled = false; _copySummary.Enabled = false;
        var progressPanel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
        progressPanel.Controls.Add(_progress); progressPanel.Controls.Add(_progressText);
        var incidentHeader = new Label { Dock = DockStyle.Top, Height = 32, Text = "Selected incident", Font = new Font("Segoe UI Semibold", 12), ForeColor = _accent };
        selectedPanel.Controls.Add(_incidentDetail);
        selectedPanel.Controls.Add(progressPanel);
        selectedPanel.Controls.Add(actions);
        selectedPanel.Controls.Add(incidentHeader);
        detailWorkspace.Panel1.Controls.Add(selectedPanel);

        _workspaceTabs.Padding = new Point(14, 6);
        var evidencePage = new TabPage("Evidence status") { BackColor = Color.White, Padding = new Padding(10) };
        evidencePage.Controls.Add(_evidence);
        var reportPage = new TabPage("Readable report") { BackColor = Color.White, Padding = new Padding(4) };
        var reportPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var reportActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(6) };
        reportActions.Controls.AddRange([_copyReport, _copyJson, _saveText, _printPdf]);
        _copyReport.Enabled = _copyJson.Enabled = _saveText.Enabled = _printPdf.Enabled = false;
        reportPanel.Controls.Add(_reportViewer);
        reportPanel.Controls.Add(reportActions);
        reportPage.Controls.Add(reportPanel);
        var rawPage = new TabPage("Raw debugger output") { BackColor = Color.FromArgb(17, 28, 45), Padding = new Padding(4) };
        rawPage.Controls.Add(_rawDebuggerViewer);
        _workspaceTabs.TabPages.Add(evidencePage);
        _workspaceTabs.TabPages.Add(reportPage);
        _workspaceTabs.TabPages.Add(rawPage);
        detailWorkspace.Panel2.Controls.Add(_workspaceTabs);

        workspace.Panel2.Padding = new Padding(4, 0, 0, 0);
        workspace.Panel2.Controls.Add(detailWorkspace);
        root.Controls.Add(workspace);
        root.Controls.Add(_headline);
        _headline.Dock = DockStyle.Top;
        _headline.Height = 48;
        root.Controls.SetChildIndex(_headline, 0);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildSettings()
    {
        var page = Page(); var root = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        root.Controls.Add(new Label { Text = "Settings", Font = new Font("Segoe UI Semibold", 18), AutoSize = true, Margin = new Padding(0, 0, 0, 18) });
        root.Controls.Add(Row("Minutes before incident", _before)); root.Controls.Add(Row("Minutes after incident / startup", _after));
        root.Controls.Add(_analyzeDumps); root.Controls.Add(Row("Debugger timeout (seconds)", _debuggerTimeout));
        root.Controls.Add(_redact); root.Controls.Add(_fullDump);
        root.Controls.Add(new Label { Text = "Crash dumps can contain passwords, document fragments and other sensitive data. Full dumps are excluded by default.", ForeColor = Color.FromArgb(140, 75, 20), AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 10, 0, 14) });
        var outputBrowse = SecondaryButton("Browse…"); outputBrowse.Click += (_, _) => BrowseFolder(_output); root.Controls.Add(Row("Output folder", _output, outputBrowse));
        root.Controls.Add(_testMode); var testBrowse = SecondaryButton("Browse…"); testBrowse.Click += (_, _) => BrowseFolder(_testData); root.Controls.Add(Row("Copied sample-log folder", _testData, testBrowse));
        root.Controls.Add(new Label { Text = "TEST-DATA MODE reads copied XML logs and inventory text from the selected folder. It is clearly labelled and never substitutes sample values into normal collection.", ForeColor = Color.FromArgb(95, 63, 0), AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 8, 0, 14) });
        var save = PrimaryButton("Save settings"); save.Click += async (_, _) => await SaveSettingsAsync(); root.Controls.Add(save);
        root.Controls.Add(new Label { Text = "Why administrator access?\nThe normal interface always runs unelevated. If Windows denies access to protected minidumps or MEMORY.DMP, a separate signed-boundary helper prompts through UAC and performs only a streamed copy from known dump locations. Declining the prompt simply omits those protected files.", AutoSize = true, MaximumSize = new Size(780, 0), Margin = new Padding(0, 26, 0, 0), ForeColor = Color.FromArgb(75, 85, 99) });
        page.Controls.Add(root); return page;
    }

    private async Task InitializeAsync()
    {
        _settings = await SettingsStore.LoadAsync(); LoadSettingsControls();
        try
        {
            var since = _settings.IsTestDataMode ? DateTimeOffset.MinValue : await IncidentDetector.ReadLastSuccessfulRunAsync(CancellationToken.None);
            var detector = new IncidentDetector(new EventLogReader(), _log);
            var newlyDetected = await detector.DetectAsync(since, _settings.TestDataDirectory, CancellationToken.None); _newIncidentIds = newlyDetected.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            _incidents = _settings.IsTestDataMode ? newlyDetected : await detector.DetectAsync(DateTimeOffset.Now.AddDays(-30), null, CancellationToken.None);
            PopulateTimeline(); if (!_settings.IsTestDataMode) await IncidentDetector.MarkSuccessfulRunAsync(CancellationToken.None);
        }
        catch (Exception ex) { _headline.Text = "Incident detection could not complete"; _incidentDetail.Text = ex.Message; await _log.WriteAsync("error", "Startup detection failed", new { ex.Message }); }
    }

    private async Task CollectAsync()
    {
        if (_timeline.SelectedItems.Count == 0) { MessageBox.Show(this, "Select an incident in the timeline first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        await SaveSettingsAsync(showConfirmation: false); var incident = (Incident)_timeline.SelectedItems[0].Tag!;
        _collectionCts = new(); ToggleCollecting(true); _evidence.Items.Clear();
        try
        {
            var progress = new Progress<CollectionProgress>(p => { _progress.Value = Math.Clamp(p.Percent, 0, 100); _progressText.Text = $"{p.Category}: {p.Message}"; AddOrUpdateEvidence(p.Category, p.State, p.Message); });
            var engine = new EvidenceCollectionEngine(new EventLogReader(), new SystemEvidenceCollector(), new FileEvidenceCollector(), new ReportWriter(), _log);
            Func<string, bool, CancellationToken, Task<HelperResponse>> elevated = (destination, full, token) => ElevatedHelperIpc.RequestDumpCopyAsync(Path.Combine(AppContext.BaseDirectory, "CrashEvidenceCollector.Helper.exe"), destination, full, token, incident.Timestamp);
            _lastResult = await engine.CollectAsync(incident, _incidents, _settings, progress, _collectionCts.Token, elevated);
            PopulateEvidence(_lastResult.Report);
            _reportViewer.Navigate(_lastResult.HtmlPath);
            var primaryDump = _lastResult.Report.DumpAnalyses.FirstOrDefault(item => item.Association.IsPrimaryForIncident);
            _rawDebuggerViewer.Text = primaryDump?.Dump.RawOutputPath is { } rawPath && File.Exists(rawPath) ? await File.ReadAllTextAsync(rawPath) : "No debugger output was available for this incident.";
            _openOutput.Enabled = _copySummary.Enabled = _copyReport.Enabled = _copyJson.Enabled = _saveText.Enabled = true;
            _printPdf.Enabled = _reportViewer.ReadyState == WebBrowserReadyState.Complete;
            _workspaceTabs.SelectedIndex = 1;
            _pages.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { _progressText.Text = "Collection cancelled. Partial files were left intact for inspection."; }
        catch (Exception ex) { _progressText.Text = "Collection failed: " + ex.Message; await _log.WriteAsync("error", "Collection failed", new { ex.Message }); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { ToggleCollecting(false); _collectionCts.Dispose(); _collectionCts = null; }
    }

    private void PopulateTimeline()
    {
        _timeline.Items.Clear();
        var referenceTime = _settings.IsTestDataMode && _incidents.Count > 0 ? _incidents.Max(x => x.Timestamp) : DateTimeOffset.Now;
        // The immediate-history selector deliberately provides fine-grained hours
        // rather than forcing users to jump from one hour directly to a full day.
        var range = _timelineRange.SelectedIndex switch
        {
            0 => TimeSpan.FromMinutes(30),
            2 => TimeSpan.FromHours(2),
            3 => TimeSpan.FromHours(3),
            4 => TimeSpan.FromHours(6),
            5 => TimeSpan.FromHours(12),
            6 => TimeSpan.FromHours(24),
            7 => TimeSpan.FromDays(3),
            8 => TimeSpan.FromDays(7),
            9 => TimeSpan.FromDays(30),
            10 => TimeSpan.FromHours((double)_customRangeHours.Value),
            _ => TimeSpan.FromHours(1)
        };
        bool MatchesType(Incident incident) => _timelineType.SelectedIndex switch
        {
            1 => true,
            2 => incident.Kind == IncidentKind.ApplicationCrash,
            3 => incident.Kind == IncidentKind.HardwareError,
            _ => incident.Kind is IncidentKind.BugCheck or IncidentKind.UnexpectedShutdown or IncidentKind.PowerLossOrFreeze or IncidentKind.HardwareError
        };
        var matchingType = _incidents.Where(MatchesType).ToList();
        var visible = matchingType.Where(x => x.Timestamp >= referenceTime - range && x.Timestamp <= referenceTime.AddMinutes(1)).OrderByDescending(x => x.Timestamp).ToList();
        foreach (var incident in visible) { var item = new ListViewItem(incident.Timestamp.LocalDateTime.ToString("g")) { Tag = incident }; item.SubItems.Add(incident.Kind.ToString()); item.SubItems.Add(incident.Title); item.SubItems.Add(CodeDecoder.DescribeIncident(incident)); item.SubItems.Add(incident.Source); _timeline.Items.Add(item); }
        _timelineCount.Text = $"{visible.Count} shown · {matchingType.Count} matching · {_incidents.Count} total";
        var lastHourCount = _incidents.Count(x => x.Kind is IncidentKind.BugCheck or IncidentKind.UnexpectedShutdown or IncidentKind.PowerLossOrFreeze or IncidentKind.HardwareError && x.Timestamp >= referenceTime.AddHours(-1) && x.Timestamp <= referenceTime.AddMinutes(1));
        _headline.Text = lastHourCount == 0 ? "No system crash incidents in the last hour" : $"{lastHourCount} system incident{(lastHourCount == 1 ? string.Empty : "s")} in the last hour";
        _incidentDetail.Text = visible.Count == 0
            ? (_incidents.Count == 0 ? "No matching crash, bugcheck, hardware-error or unexpected-shutdown records were found in the retained timeline." : "The immediate window is clear. Choose Review past history to inspect older incidents.")
            : "Recent incidents are shown first. Select one to review and collect its focused evidence window.";
        _history.Enabled = _incidents.Any(x => x.Timestamp < referenceTime.AddHours(-1));
        _collect.Enabled = visible.Count > 0; if (_timeline.Items.Count > 0) _timeline.Items[0].Selected = true;
    }
    private void UpdateSelection()
    {
        if (_timeline.SelectedItems.Count == 0) return;
        var i = (Incident)_timeline.SelectedItems[0].Tag!;
        var reboot = i.RebootTime is null ? "Not identified" : i.RebootTime.Value.ToLocalTime().ToString("F");
        _incidentDetail.Text = $"Crash/shutdown time: {i.Timestamp.ToLocalTime():F}\r\nTime source: {i.TimestampBasis}\r\nNext Windows boot: {reboot}\r\n{i.Title}\r\n{CodeDecoder.DescribeIncident(i)}";
        _collect.Enabled = true;
    }
    private void PopulateEvidence(EvidenceReport report) { _evidence.Items.Clear(); foreach (var item in report.Evidence) AddOrUpdateEvidence(item.Category, item.State, item.Summary + (item.Error is null ? string.Empty : " — " + item.Error)); }
    private void AddOrUpdateEvidence(string category, EvidenceState state, string detail) { var item = _evidence.Items.Cast<ListViewItem>().FirstOrDefault(x => x.Text == category) ?? _evidence.Items.Add(category); while (item.SubItems.Count < 3) item.SubItems.Add(string.Empty); item.SubItems[1].Text = state.ToString(); item.SubItems[2].Text = detail; item.ForeColor = state switch { EvidenceState.Failure => Color.Firebrick, EvidenceState.Warning => Color.DarkGoldenrod, EvidenceState.Success => Color.SeaGreen, _ => Color.FromArgb(35, 45, 58) }; }
    private async Task SaveSettingsAsync(bool showConfirmation = true) { _settings.MinutesBefore = (int)_before.Value; _settings.MinutesAfterStartup = (int)_after.Value; _settings.AnalyzeCrashDumps = _analyzeDumps.Checked; _settings.DebuggerTimeoutSeconds = (int)_debuggerTimeout.Value; _settings.IncludeFullMemoryDump = _fullDump.Checked; _settings.RedactAccountName = _redact.Checked; _settings.OutputRoot = _output.Text.Trim(); _settings.TestDataDirectory = _testMode.Checked ? _testData.Text.Trim() : null; await SettingsStore.SaveAsync(_settings); _modeBanner.Visible = _settings.IsTestDataMode; _modeBanner.Text = _settings.IsTestDataMode ? "TEST-DATA MODE — reading copied sample logs only" : string.Empty; if (showConfirmation) MessageBox.Show(this, "Settings saved. Restart the app to re-run incident detection with a changed data source.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
    private void LoadSettingsControls() { _before.Value = Math.Clamp(_settings.MinutesBefore, 1, 1440); _after.Value = Math.Clamp(_settings.MinutesAfterStartup, 1, 1440); _analyzeDumps.Checked = _settings.AnalyzeCrashDumps; _debuggerTimeout.Value = Math.Clamp(_settings.DebuggerTimeoutSeconds, 15, 1800); _fullDump.Checked = _settings.IncludeFullMemoryDump; _redact.Checked = _settings.RedactAccountName; _output.Text = _settings.OutputRoot; _testMode.Checked = _settings.IsTestDataMode; _testData.Text = _settings.TestDataDirectory ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestData")); _modeBanner.Visible = _settings.IsTestDataMode; _modeBanner.Text = _settings.IsTestDataMode ? "TEST-DATA MODE — reading copied sample logs only" : string.Empty; }
    private void ToggleCollecting(bool collecting) { _collect.Enabled = !collecting && _timeline.SelectedItems.Count > 0; _cancel.Enabled = collecting; }
    private void OpenOutput() { if (_lastResult is not null) Process.Start(new ProcessStartInfo("explorer.exe", _lastResult.OutputDirectory) { UseShellExecute = true }); }
    private void CopySummary() { if (_lastResult is null) return; var report = _lastResult.Report; Clipboard.SetText($"{report.Incident.Title} at {report.Incident.Timestamp:F}\r\nMeaning: {CodeDecoder.DescribeIncident(report.Incident)}\r\nDecoded codes: {string.Join(" | ", report.CodeInterpretations.Select(x => $"{x.RawValue} = {x.Name}").Take(8))}\r\nObservations: {string.Join(" | ", report.Observations.Select(x => x.Detail))}\r\nArchive: {_lastResult.ZipPath}"); }
    private async Task CopyFileContentsAsync(string? path)
    {
        if (path is null || !File.Exists(path)) return;
        Clipboard.SetText(await File.ReadAllTextAsync(path));
        _progressText.Text = $"Copied {Path.GetFileName(path)} to the clipboard.";
    }
    private void SaveReportText()
    {
        if (_lastResult is null || !File.Exists(_lastResult.TextPath)) return;
        using var dialog = new SaveFileDialog { Title = "Save readable text report", Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*", FileName = $"Crash-Evidence-{_lastResult.Report.Incident.Timestamp:yyyyMMdd-HHmmss}.txt", OverwritePrompt = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { File.Copy(_lastResult.TextPath, dialog.FileName, true); _progressText.Text = $"Saved text report to {dialog.FileName}"; }
    }
    private void PrintOrSavePdf()
    {
        if (_lastResult is null) return;
        _progressText.Text = "Choose Microsoft Print to PDF in the Windows print dialog to save a PDF.";
        _reportViewer.ShowPrintDialog();
    }
    private static void BrowseFolder(TextBox target) { using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(target.Text) ? target.Text : string.Empty, ShowNewFolderButton = true }; if (dialog.ShowDialog() == DialogResult.OK) target.Text = dialog.SelectedPath; }
    private static Panel Card(Control child) { var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(0, 6, 0, 6) }; panel.Controls.Add(child); return panel; }
    private static TabPage Page() => new() { BackColor = Color.FromArgb(246, 248, 251), Padding = new Padding(0), UseVisualStyleBackColor = false };
    private static Button PrimaryButton(string text) => new() { Text = text, AutoSize = true, Height = 36, Padding = new Padding(12, 4, 12, 4), BackColor = Color.FromArgb(40, 105, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };
    private static Button SecondaryButton(string text) => new() { Text = text, AutoSize = true, Height = 36, Padding = new Padding(10, 4, 10, 4), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };
    private static Button NavButton(string text) => new() { Text = text, Dock = DockStyle.Top, Height = 46, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, BackColor = Color.FromArgb(20, 35, 59), Padding = new Padding(10, 0, 0, 0) };
    private static FlowLayoutPanel Row(string label, params Control[] controls) { var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 10) }; row.Controls.Add(new Label { Text = label, Width = 230, Height = 32, TextAlign = ContentAlignment.MiddleLeft }); row.Controls.AddRange(controls); return row; }
}
