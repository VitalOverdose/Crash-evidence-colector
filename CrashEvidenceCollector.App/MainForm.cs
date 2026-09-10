using System.Diagnostics;
using System.ComponentModel;
using CrashEvidenceCollector.Core;
using ProfessorSnowsVideoDownloader.CustomControls;
using ProfessorSnowsVideoDownloader.Composites;

namespace CrashEvidenceCollector.App;

public sealed partial class MainForm : Form
{
    private readonly Color _nav = UiTheme.Nav;
    private readonly Color _accent = UiTheme.Accent;
    private ListView _timeline = null!;
    private readonly Views.IncidentDetailView _incidentView = new();
    private readonly Views.IncidentComparisonView _comparisonView = new();
    private readonly List<Button> _navButtons = [];
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 8 };
    private readonly Label _progressText = new() { Dock = DockStyle.Bottom, Height = 32, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 85, 99) };
    private readonly Views.EvidenceStatusView _evidenceView = new();
    private readonly Views.RawDebuggerView _rawView = new();
    private readonly Views.ReadableReportView _reportPane = new();
    private readonly Views.HelpView _helpView = new();
    private TabWorkspaceController _tabs = null!;
    /// <summary>Index of the readable report pane in the fixed tab order.</summary>
    private const int ReportTabIndex = 5;
    /// <summary>Index of the comparison board in the fixed tab order.</summary>
    private const int CompareTabIndex = 1;
    /// <summary>
    /// Lets the user trade briefing height for timeline rows. The briefing sits
    /// between the filters and the list, so neither has to be a fixed compromise.
    /// </summary>
    private readonly Splitter _briefingSplitter = new() { Dock = DockStyle.Top, Height = 6, MinExtra = 180, MinSize = 150, BackColor = UiTheme.Border };
    private readonly IncidentMetricsView _metrics = new();
    private readonly LiveMonitorView _liveMonitor = new();
    /// <summary>
    /// Always-visible machine state. Docked on the form rather than inside a page,
    /// so the answer to "is this machine alright?" is on screen from every tab
    /// instead of being somewhere the user has to navigate to.
    /// </summary>
    private readonly Views.StatusBandView _statusBand = new();
    private readonly System.Windows.Forms.Timer _statusClock = new() { Interval = 15000 };
    private readonly ReportViewerPanel _summaryViewer = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _summaryFrom = new() { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today.AddDays(-29) };
    private readonly DateTimePicker _summaryTo = new() { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today };
    private readonly Button _summarySave = SecondaryButton("Save as…");
    private readonly CheckBox _filterBugChecks = new() { Text = "Bugchecks", AutoSize = true, Checked = true, Padding = new Padding(0, 6, 10, 0) };
    private readonly CheckBox _filterPower = new() { Text = "Power / shutdown", AutoSize = true, Checked = true, Padding = new Padding(0, 6, 10, 0) };
    private readonly CheckBox _filterHardware = new() { Text = "Hardware errors", AutoSize = true, Checked = true, Padding = new Padding(0, 6, 10, 0) };
    private readonly CheckBox _filterApplications = new() { Text = "Application crashes", AutoSize = true, Checked = true, Padding = new Padding(0, 6, 10, 0) };
    private readonly CheckBox _filterCollected = new() { Text = "Only incidents with collected evidence", AutoSize = true, Padding = new Padding(0, 6, 10, 0) };
    private readonly TextBox _filterCode = new() { Width = 90, PlaceholderText = "0x1E" };
    private readonly TextBox _filterText = new() { Width = 150, PlaceholderText = "module / text" };
    private readonly Label _summaryStatus = new() { Dock = DockStyle.Top, Height = 26, ForeColor = Color.FromArgb(75, 85, 99), Text = "Choose a date range and generate a summary of every incident it contains." };
    private CrashSummary? _lastSummary;
    private readonly NumericUpDown _before = new() { Minimum = 1, Maximum = 1440, Value = 10, Width = 90 };
    private readonly NumericUpDown _after = new() { Minimum = 1, Maximum = 1440, Value = 5, Width = 90 };
    private readonly NumericUpDown _debuggerTimeout = new() { Minimum = 15, Maximum = 1800, Value = 180, Increment = 15, Width = 90 };
    private readonly CheckBox _analyzeDumps = new() { Text = "Analyse copied crash dumps with the Microsoft debugger", AutoSize = true, Checked = true };
    private readonly CheckBox _fullDump = new() { Text = "Include full MEMORY.DMP in ZIP (may be very large and sensitive)", AutoSize = true };
    private readonly CheckBox _redact = new() { Text = "Redact Windows account name and profile path in reports", AutoSize = true, Checked = true };
    private readonly CheckBox _webLookup = new() { Text = "Look up recently installed programs online (Microsoft winget source) for report context", AutoSize = true, Checked = true };
    private readonly CheckBox _monitorAutoStart = new() { Text = "Resume background monitoring automatically when this app starts", AutoSize = true };
    private readonly CheckBox _alwaysElevate = new() { Text = "Always run Crash Evidence Collector as administrator (one prompt at startup instead of one per collection)", AutoSize = true };
    private readonly Button _elevateNow = SecondaryButton("Restart as administrator");
    private readonly CheckBox _startWithWindows = new() { Text = "Start Crash Evidence Collector when Windows starts (so monitoring survives reboots)", AutoSize = true };
    private readonly CheckBox _testMode = new() { Text = "Use local test-data mode", AutoSize = true };
    private readonly TextBox _output = new() { Width = 530 };
    private readonly TextBox _testData = new() { Width = 530 };
    private readonly StructuredLog _log = new();
    private IReadOnlyList<Incident> _incidents = [];
    private IReadOnlyList<Incident> _retainedReportIncidents = [];
    private HashSet<string> _newIncidentIds = [];
    private CollectionOptions _settings = new();
    private CancellationTokenSource? _collectionCts;
    private CollectionResult? _lastResult;

    private static bool IsDesignerHosted => LicenseManager.UsageMode == LicenseUsageMode.Designtime;

    public MainForm()
    {
        InitializeComponent();
        if (IsDesignerHosted) return;
        try { using var icoStream = typeof(MainForm).Assembly.GetManifestResourceStream("CrashEvidenceCollector.App.Assets.cec.ico"); if (icoStream is not null) Icon = new Icon(icoStream); } catch { /* branding must never block startup */ }
        ConfigureNavigation();
        Controls.Add(_statusBand);
        _pages.TabPages.Clear();
        _pages.TabPages.Add(BuildWorkspace()); _pages.TabPages.Add(BuildSummaryReport()); _pages.TabPages.Add(BuildSettings());
        // Timeline columns are defined in the designer template; adding them here too would duplicate them.
        _timeline.SelectedIndexChanged += (_, _) => UpdateSelection(); _timeline.DoubleClick += (_, _) => UpdateSelection();
        _workspace.FilterChanged += _ => PopulateTimeline();
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.Alt && e.KeyCode == Keys.Left) { e.SuppressKeyPress = true; _workspace.GoBackInTimeline(); } };
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.F) { e.SuppressKeyPress = true; _workspace.FocusTimelineSearch(); } };
        _reportPane.ReportSurface.NavigationFailed += (_, status) => _progressText.Text = $"The report could not be displayed in the embedded viewer ({status}). Use Open output folder to view report.html in your browser.";
        // The band follows the live sample when monitoring is on, and ticks anyway
        // so uptime and "since last crash" stay true while the app sits idle.
        _liveMonitor.SampleUpdated += UpdateStatusBand;
        _statusClock.Tick += (_, _) => UpdateStatusBand();
        _statusBand.AttentionClicked += () => { _pages.SelectedIndex = 0; _tabs.SelectTab(0); };
        _statusBand.LastSevenDaysClicked += () =>
        {
            _pages.SelectedIndex = 0;
            _workspace.SelectTimelineRange("Last 7 days");
        };
        _statusClock.Start();
        Shown += async (_, _) => await InitializeAsync(); FormClosing += (_, _) => { _statusClock.Stop(); _collectionCts?.Cancel(); };
    }

    private void ConfigureNavigation()
    {
        _navButtons.AddRange([commandCenterButton, summaryIntelligenceButton, systemSettingsButton]);
        commandCenterButton.Click += (_, _) => _pages.SelectedIndex = 0;
        summaryIntelligenceButton.Click += (_, _) => _pages.SelectedIndex = 1;
        systemSettingsButton.Click += (_, _) => _pages.SelectedIndex = 2;
        _pages.SelectedIndexChanged += (_, _) => UpdateNavigationState();
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        for (var index = 0; index < _navButtons.Count; index++)
        {
            var active = index == _pages.SelectedIndex;
            _navButtons[index].BackColor = active ? UiTheme.NavHover : UiTheme.Nav;
            _navButtons[index].ForeColor = active ? Color.White : Color.FromArgb(164, 184, 207);
            _navButtons[index].FlatAppearance.BorderColor = active ? UiTheme.Accent : UiTheme.Nav;
            _navButtons[index].FlatAppearance.BorderSize = active ? 1 : 0;
        }
    }

    /// <summary>
    /// Builds the primary one-screen workflow. The outer splitter keeps the incident
    /// timeline visible while the inner splitter places actions above evidence/report
    /// tabs. Users can resize each area to suit the amount of detail they want to read.
    /// </summary>
    /// <summary>
    /// Hosts the designer-authored workspace: David's splitter, timeline pane and
    /// vStack shell. Everything here binds behaviour to controls he named; the
    /// layout itself lives in the designer, not in code.
    /// </summary>
    private TabPage BuildWorkspace()
    {
        var page = Page();

        _timeline = _workspace.TimelineList;
        _workspace.InitializeTimelineFilters();

        // The briefing belongs beside the evidence it describes, not behind a tab.
        // Docking order in this panel runs bottom-up by child index, so the two new
        // controls are placed explicitly: headline, filters, briefing, splitter, list.
        _incidentView.Dock = DockStyle.Top;
        _incidentView.Height = 330;
        var timelinePane = _workspace.TimelinePanel;
        timelinePane.Controls.Add(_incidentView);
        timelinePane.Controls.Add(_briefingSplitter);
        timelinePane.Controls.SetChildIndex(_briefingSplitter, 1);
        timelinePane.Controls.SetChildIndex(_incidentView, 2);

        _tabs = new TabWorkspaceController(_workspace.TabHeaders, _workspace.TabContent, _workspace);
        _tabs.AddFixedTab("Metrics & trends", _metrics);
        _tabs.AddFixedTab("Compare", _comparisonView);
        _tabs.AddFixedTab("Live monitor", _liveMonitor);
        _tabs.AddFixedTab("Evidence status", _evidenceView);
        _tabs.AddFixedTab("Raw debugger output", _rawView);
        _tabs.AddFixedTab("Readable report", _reportPane);
        _tabs.AddFixedTab("Help", _helpView);

        // Clicking a day in the chart answers "which applications, and why".
        _metrics.DaySelected += ShowDayDetail;
        _incidentView.CollectRequested += async incident => { SelectIncidentInTimeline(incident); await CollectAsync(); };
        _incidentView.CompareRequested += incident => { _comparisonView.Add(incident); _tabs.SelectTab(CompareTabIndex); };
        _incidentView.SearchRequested += text => _tabs.Search(text, true);
        _comparisonView.IncidentRequested += SelectIncidentInTimeline;
        BuildTimelineContextMenu();

        // The navigation row drives whichever web tab is showing; it collapses on
        // every other tab, so these only ever act on a live page.
        _workspace.BackButton.Click += (_, _) => _tabs.CurrentWeb?.GoBack();
        _workspace.RefreshButton.Click += (_, _) => _tabs.CurrentWeb?.Reload();
        _workspace.GoButton.Click += (_, _) => NavigateFromAddressBox();
        _workspace.PasteButton.Click += (_, _) => { if (_tabs.CurrentWeb is null) _tabs.Search(null, true); else _tabs.CurrentWeb.NavigateFromClipboard(); };
        _workspace.AddressBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateFromAddressBox(); } };

        // Report actions, raised by the pane so its toolbar can be restyled freely.
        _reportPane.CopyTextRequested += async (_, _) => await CopyFileContentsAsync(_lastResult?.TextPath);
        _reportPane.CopyJsonRequested += async (_, _) => await CopyFileContentsAsync(_lastResult?.JsonPath);
       _reportPane.SaveTextRequested += (_, _) => SaveReportText();
        _reportPane.CopySummaryRequested += (_, _) => CopySummary();
        _reportPane.OpenOutputRequested += (_, _) => OpenOutput();
        _reportPane.CancelCollectionRequested += (_, _) => _collectionCts?.Cancel();
       _reportPane.SearchClipboardRequested += (_, _) => _tabs.Search(SafeClipboardText(), false);
        _reportPane.SearchRequested += (text, newTab) => _tabs.Search(text, newTab);

        var statusStrip = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(18, 0, 18, 4) };
        statusStrip.Controls.Add(_progress);
        statusStrip.Controls.Add(_progressText);

        page.Controls.Add(_workspace);
        page.Controls.Add(statusStrip);
        return page;
    }

    private void BuildTimelineContextMenu()
    {
        var menu = new ContextMenuStrip { Font = Font };
        // No "open briefing" item: the briefing is now always on screen above this list.
        menu.Items.Add("Add to comparison board", null, (_, _) =>
        {
            if (_timeline.SelectedItems.Count == 0) return;
            _comparisonView.Add((Incident)_timeline.SelectedItems[0].Tag!);
            _tabs.SelectTab(CompareTabIndex);
        });
        menu.Items.Add("Research code / incident", null, (_, _) =>
        {
            if (_timeline.SelectedItems.Count == 0) return;
            var incident = (Incident)_timeline.SelectedItems[0].Tag!;
            _tabs.Search(!string.IsNullOrWhiteSpace(incident.BugCheckCode) ? $"Windows {CodeDecoder.GetBugCheckLabel(incident.BugCheckCode)}" : $"Windows {incident.Title}", true);
        });
        _timeline.ContextMenuStrip = menu;
    }

    /// <summary>
    /// Shows every incident recorded on the clicked day. A count of "application
    /// incidents" is not actionable without knowing which applications failed.
    /// </summary>
    private void ShowDayDetail(CrashDailyMetric day)
    {
        var start = day.Day;
        var end = start.AddDays(1);
        var incidents = MergedIncidents().All
            .Where(incident => incident.Timestamp >= start && incident.Timestamp < end)
            .ToList();
        using var dialog = new Views.DayDetailForm(start, incidents);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenIncident is null) return;
        SelectIncidentInTimeline(dialog.ChosenIncident);
    }

    /// <summary>Selects an incident in the timeline, widening the range if it is out of view.</summary>
    private void SelectIncidentInTimeline(Incident incident)
    {
        var row = _timeline.Items.Cast<ListViewItem>().FirstOrDefault(item => ((Incident)item.Tag!).Id == incident.Id);
        if (row is null && !string.IsNullOrEmpty(_workspace.GetFilter().Search))
        {
            _workspace.ClearTimelineSearch();
            row = _timeline.Items.Cast<ListViewItem>().FirstOrDefault(item => ((Incident)item.Tag!).Id == incident.Id);
        }
        if (row is null)
        {
            _workspace.SelectTimelineRange("All time");
            row = _timeline.Items.Cast<ListViewItem>().FirstOrDefault(item => ((Incident)item.Tag!).Id == incident.Id);
        }
        if (row is null) return;
        row.Selected = true;
        row.EnsureVisible();
        _timeline.Focus();
        // Selecting the row updates the briefing in place; there is no tab to switch to.
    }

    private void NavigateFromAddressBox()
    {
        var text = _workspace.AddressBox.Text;
        if (_tabs.CurrentWeb is null) _tabs.Search(text, true);
        else _tabs.CurrentWeb.Navigate(text);
    }

    private static string SafeClipboardText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty; }
        catch { return string.Empty; }
    }

    private TabPage BuildSummaryReport()
    {
        var page = Page();
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24) };
        var viewerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(2) };
        viewerPanel.Controls.Add(_summaryViewer);

        var controls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, WrapContents = false, Padding = new Padding(0, 8, 0, 8) };
        controls.Controls.Add(new Label { Text = "From", AutoSize = true, Padding = new Padding(0, 8, 6, 0) });
        controls.Controls.Add(_summaryFrom);
        controls.Controls.Add(new Label { Text = "To", AutoSize = true, Padding = new Padding(12, 8, 6, 0) });
        controls.Controls.Add(_summaryTo);
        foreach (var (text, days) in new[] { ("7 days", 7), ("30 days", 30), ("90 days", 90) })
        {
            var quick = SecondaryButton(text);
            quick.Click += (_, _) => { _summaryFrom.Value = DateTime.Today.AddDays(-days + 1); _summaryTo.Value = DateTime.Today; };
            controls.Controls.Add(quick);
        }
        var generate = PrimaryButton("Generate summary");
        generate.Click += async (_, _) => await GenerateSummaryAsync();
        controls.Controls.Add(generate);
        _summarySave.Enabled = false; _summarySave.Click += (_, _) => SaveSummary();
        controls.Controls.Add(_summarySave);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = true };
        filters.Controls.Add(new Label { Text = "Include:", AutoSize = true, Padding = new Padding(0, 6, 8, 0), Font = new Font("Segoe UI Semibold", 9f) });
        filters.Controls.AddRange([_filterBugChecks, _filterPower, _filterHardware, _filterApplications, _filterCollected]);
        filters.Controls.Add(new Label { Text = "Code:", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        filters.Controls.Add(_filterCode);
        filters.Controls.Add(new Label { Text = "Contains:", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        filters.Controls.Add(_filterText);
        var kernelOnly = SecondaryButton("Kernel crashes only");
        kernelOnly.Click += (_, _) => { _filterBugChecks.Checked = _filterPower.Checked = _filterHardware.Checked = true; _filterApplications.Checked = false; _filterCollected.Checked = false; _filterCode.Clear(); _filterText.Clear(); };
        filters.Controls.Add(kernelOnly);

        root.Controls.Add(viewerPanel);
        root.Controls.Add(filters);
        root.Controls.Add(_summaryStatus);
        root.Controls.Add(controls);
        root.Controls.Add(new Label { Dock = DockStyle.Top, Height = 44, Text = "Summary report", Font = new Font("Segoe UI Semibold", 16), AutoSize = false });
        page.Controls.Add(root);
        return page;
    }

    private async Task GenerateSummaryAsync()
    {
        var from = new DateTimeOffset(_summaryFrom.Value.Date, DateTimeOffset.Now.Offset);
        var to = new DateTimeOffset(_summaryTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeOffset.Now.Offset);
        if (to < from) { MessageBox.Show(this, "The end date is before the start date.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        _summaryStatus.Text = "Reading retained reports…";
        try
        {
            var filter = new CrashSummaryFilter(_filterBugChecks.Checked, _filterPower.Checked, _filterHardware.Checked, _filterApplications.Checked, _filterCollected.Checked,
                string.IsNullOrWhiteSpace(_filterCode.Text) ? null : _filterCode.Text, string.IsNullOrWhiteSpace(_filterText.Text) ? null : _filterText.Text);
            _lastSummary = await CrashSummaryBuilder.BuildAsync(_settings.OutputRoot, from, to, _incidents, MonitorLog.DefaultDirectory, CancellationToken.None, filter);
            var path = Path.Combine(Path.GetTempPath(), $"cec-summary-{from:yyyyMMdd}-{to:yyyyMMdd}.html");
            await File.WriteAllTextAsync(path, CrashSummaryWriter.BuildHtml(_lastSummary), new System.Text.UTF8Encoding(false));
            _summaryViewer.ShowReportFile(path);
            _summarySave.Enabled = true;
            _summaryStatus.Text = $"{_lastSummary.Entries.Count} incident(s) between {from:d MMM yyyy} and {to:d MMM yyyy}; {_lastSummary.Collected} with collected evidence."
                + (_lastSummary.ExcludedByFilter > 0 ? $" {_lastSummary.ExcludedByFilter} excluded by the filter (stated in the report)." : string.Empty);
        }
        catch (Exception ex) { _summaryStatus.Text = "Summary failed: " + ex.Message; await _log.WriteAsync("error", "Summary report failed", new { ex.Message }); }
    }

    private void SaveSummary()
    {
        if (_lastSummary is null) return;
        using var dialog = new SaveFileDialog { Title = "Save crash summary", Filter = "HTML report (*.html)|*.html|Text report (*.txt)|*.txt", FileName = $"Crash-Summary-{_lastSummary.From:yyyyMMdd}-{_lastSummary.To:yyyyMMdd}.html", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var text = dialog.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? CrashSummaryWriter.BuildPlainText(_lastSummary) : CrashSummaryWriter.BuildHtml(_lastSummary);
        File.WriteAllText(dialog.FileName, text, new System.Text.UTF8Encoding(false));
        _summaryStatus.Text = $"Saved to {dialog.FileName}";
    }

    private TabPage BuildSettings()
    {
        var page = Page(); var root = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        root.Controls.Add(new Label { Text = "Settings", Font = new Font("Segoe UI Semibold", 18), AutoSize = true, Margin = new Padding(0, 0, 0, 18) });
        root.Controls.Add(Row("Minutes before incident", _before)); root.Controls.Add(Row("Minutes after incident / startup", _after));
        root.Controls.Add(_analyzeDumps); root.Controls.Add(Row("Debugger timeout (seconds)", _debuggerTimeout));
        root.Controls.Add(_redact); root.Controls.Add(_fullDump);
        root.Controls.Add(new Label { Text = "Crash dumps can contain passwords, document fragments and other sensitive data. Full dumps are excluded by default.", ForeColor = Color.FromArgb(140, 75, 20), AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 10, 0, 14) });
        root.Controls.Add(_webLookup);
        root.Controls.Add(new Label { Text = "Online lookup sends only the names of recently installed programs to the Microsoft winget source to add publisher, version and homepage context. Matches are labelled as online metadata, never as local evidence.", ForeColor = Color.FromArgb(75, 85, 99), AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 4, 0, 14) });
        root.Controls.Add(_monitorAutoStart);
        root.Controls.Add(_startWithWindows);
        root.Controls.Add(_alwaysElevate);
        root.Controls.Add(new Label
        {
            Text = ElevationService.IsElevated
            ? "This instance is running as administrator: protected crash dumps are copied directly and no per-collection prompt appears. Analysis still runs against copied evidence only, and no system setting is ever changed."
            : "This instance runs unelevated (recommended default). Protected dumps are copied by the separate single-purpose helper, which asks for consent once per collection. Running the whole app elevated replaces that with a single prompt when it starts.",
            ForeColor = Color.FromArgb(75, 85, 99),
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Margin = new Padding(0, 4, 0, 8)
        });
        _elevateNow.Enabled = !ElevationService.IsElevated;
        _elevateNow.Click += (_, _) => RestartElevated();
        root.Controls.Add(_elevateNow);
        root.Controls.Add(new Label { Text = "Start-with-Windows adds a single startup entry for this app in your user registry (HKCU Run) — the only registry value the app ever writes. Unticking removes it. System diagnostics always remain read-only.", ForeColor = Color.FromArgb(75, 85, 99), AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 4, 0, 14) });
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
        // Honour the remembered preference once, at startup, where the consent
        // dialog cannot interrupt an in-progress collection.
        if (_settings.AlwaysRunElevated && !ElevationService.IsElevated && ElevationService.Relaunch() is null) { Close(); return; }
        if (_settings.StartMonitoringOnLaunch) _liveMonitor.StartMonitoring();
        if (ElevationService.IsElevated) { _modeBanner.Visible = true; _modeBanner.Text = "ADMINISTRATOR MODE — protected crash dumps are copied without a per-collection prompt. Nothing on the system is modified."; }
        try
        {
            var since = _settings.IsTestDataMode ? DateTimeOffset.MinValue : await IncidentDetector.ReadLastSuccessfulRunAsync(CancellationToken.None);
            var detector = new IncidentDetector(new EventLogReader(), _log);
            var newlyDetected = await detector.DetectAsync(since, _settings.TestDataDirectory, CancellationToken.None); _newIncidentIds = newlyDetected.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            _incidents = _settings.IsTestDataMode ? newlyDetected : await detector.DetectAsync(DateTimeOffset.Now.AddDays(-30), null, CancellationToken.None);
            _retainedReportIncidents = await ReportHistoryReader.LoadIncidentsAsync(_settings.OutputRoot, CancellationToken.None);
            PopulateTimeline(); if (!_settings.IsTestDataMode) await IncidentDetector.MarkSuccessfulRunAsync(CancellationToken.None);
        }
        catch (Exception ex) { _workspace.ShowTimelineSummary("Incident detection could not complete", ""); _incidentView.ShowEmpty(ex.Message); await _log.WriteAsync("error", "Startup detection failed", new { ex.Message }); }
    }

    private async Task CollectAsync()
    {
        if (_collectionCts is not null) return;
        if (_timeline.SelectedItems.Count == 0) { MessageBox.Show(this, "Select an incident in the timeline first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
       await SaveSettingsAsync(showConfirmation: false); var incident = (Incident)_timeline.SelectedItems[0].Tag!;
       _collectionCts = new(); ToggleCollecting(true); _evidenceView.Clear();
        _pages.SelectedIndex = 0;
        _tabs.SelectTab(ReportTabIndex);
        _reportPane.SetLoadingState(true, "Preparing focused evidence collection…");
       try
        {
            var progress = new Progress<CollectionProgress>(p =>
            {
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
                _progressText.Text = $"{p.Category}: {p.Message}";
                _reportPane.SetLoadingState(true, $"{p.Category}: {p.Message}");
                AddOrUpdateEvidence(p.Category, p.State, p.Message);
            });
            var engine = new EvidenceCollectionEngine(new EventLogReader(), new SystemEvidenceCollector(), new FileEvidenceCollector(), new ReportWriter(), _log);
            // An elevated instance already has dump access, so the helper — and its
            // consent prompt — is skipped entirely.
            Func<string, bool, CancellationToken, Task<HelperResponse>>? elevated = ElevationService.IsElevated
                ? null
                : (destination, full, token) => ElevatedHelperIpc.RequestDumpCopyAsync(Path.Combine(AppContext.BaseDirectory, "CrashEvidenceCollector.Helper.exe"), destination, full, token, incident.Timestamp);
            _lastResult = await engine.CollectAsync(incident, _incidents, _settings, progress, _collectionCts.Token, elevated);
            PopulateEvidence(_lastResult.Report);
            _reportPane.ShowReportFile(_lastResult.HtmlPath);
            _reportPane.SetActionsEnabled(true);
            var primaryDump = _lastResult.Report.DumpAnalyses.FirstOrDefault(item => item.Association.IsPrimaryForIncident);
            _rawView.SetOutput(primaryDump?.Dump.RawOutputPath is { } rawPath && File.Exists(rawPath) ? await File.ReadAllTextAsync(rawPath) : null);
            _retainedReportIncidents = await ReportHistoryReader.LoadIncidentsAsync(_settings.OutputRoot, CancellationToken.None);
            UpdateMetrics();
            _tabs.SelectTab(ReportTabIndex);
            _pages.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { _progressText.Text = "Collection cancelled. Partial files were left intact for inspection."; }
        catch (Exception ex) { _progressText.Text = "Collection failed: " + ex.Message; await _log.WriteAsync("error", "Collection failed", new { ex.Message }); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _reportPane.SetLoadingState(false);
            ToggleCollecting(false);
            _collectionCts.Dispose();
            _collectionCts = null;
        }
    }

    private void PopulateTimeline()
    {
        var selectedId = _timeline.SelectedItems.Count > 0 ? ((Incident)_timeline.SelectedItems[0].Tag!).Id : null;
        _timeline.Items.Clear();
        var referenceTime = _settings.IsTestDataMode && _incidents.Count > 0 ? _incidents.Max(x => x.Timestamp) : DateTimeOffset.Now;
        var range = SelectedTimelineRange();
        var (allIncidents, retainedOnlyIds) = MergedIncidents();
        var filter = _workspace.GetFilter();
        var result = IncidentQueryService.Query(allIncidents, filter, referenceTime);
        var matchingType = result.MatchingType;
        var inRange = result.InRange;
        var visible = result.Visible;
        var query = filter.Search.Trim();
        var recurrence = inRange.GroupBy(IncidentSignature, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        foreach (var incident in visible)
        {
            var repeats = recurrence.GetValueOrDefault(IncidentSignature(incident));
            var title = repeats > 1 ? $"{incident.Title}  •  {repeats}× in range" : incident.Title;
            var item = new ListViewItem(incident.Timestamp.LocalDateTime.ToString("g")) { Tag = incident, BackColor = UiTheme.KindWash(incident.Kind), ToolTipText = CodeDecoder.DescribeIncident(incident) };
            item.SubItems.Add(UiTheme.KindLabel(incident.Kind));
            item.SubItems.Add(title);
            item.SubItems.Add(CodeDecoder.DescribeIncident(incident));
            item.SubItems.Add(retainedOnlyIds.Contains(incident.Id) ? incident.Source + " • saved evidence" : incident.Source);
            _timeline.Items.Add(item);
        }
        var countText = $"{visible.Count} shown · {matchingType.Count} matching · {allIncidents.Count} total ({_retainedReportIncidents.Count} saved report(s))";
        // The headline must describe the range the user actually selected, or a
        // visible incident sits under a headline claiming there are none.
        var rangeLabel = (_workspace.GetFilter().Range ?? "the selected range").Replace("Last ", "last ").Replace("Custom hours", $"last {range.TotalHours:0.#} hours");
        var systemCount = visible.Count(x => x.Kind is IncidentKind.BugCheck or IncidentKind.UnexpectedShutdown or IncidentKind.PowerLossOrFreeze or IncidentKind.HardwareError);
        var headlineText = systemCount == 0 ? $"No system crash incidents in the {rangeLabel}" : $"{systemCount} system incident{(systemCount == 1 ? string.Empty : "s")} in the {rangeLabel}";
        _workspace.ShowTimelineSummary(headlineText, countText);
        if (visible.Count == 0) _incidentView.ShowEmpty(query.Length > 0 ? $"No incidents match “{query}” in this range." : (allIncidents.Count == 0 ? "No matching crash evidence was found in the retained timeline." : "The immediate window is clear. Review past history to inspect older incidents."));
        var selectedRow = _timeline.Items.Cast<ListViewItem>().FirstOrDefault(row => ((Incident)row.Tag!).Id == selectedId);
        if (selectedRow is not null) selectedRow.Selected = true;
        else if (_timeline.Items.Count > 0) _timeline.Items[0].Selected = true;
        UpdateMetrics();
        UpdateStatusBand();
    }

    // The immediate-history selector deliberately provides fine-grained hours
    // rather than forcing users to jump from one hour directly to a full day.
    private TimeSpan SelectedTimelineRange() => _workspace.GetFilter().Duration;

    /// <summary>
    /// Live event-log detections plus saved reports the live window no longer covers.
    /// Also returns the ids that came only from saved reports so the list can mark them.
    /// </summary>
    private (List<Incident> All, HashSet<string> RetainedOnlyIds) MergedIncidents()
    {
        var seen = new HashSet<string>(_incidents.Select(item => item.Id), StringComparer.Ordinal);
        var retainedOnly = _retainedReportIncidents.Where(item => seen.Add(item.Id)).ToList();
        return (_incidents.Concat(retainedOnly).ToList(), retainedOnly.Select(item => item.Id).ToHashSet(StringComparer.Ordinal));
    }

    private void UpdateMetrics()
    {
        var referenceTime = _settings.IsTestDataMode && _incidents.Count > 0 ? _incidents.Max(item => item.Timestamp) : DateTimeOffset.Now;
        var range = SelectedTimelineRange();
        var merged = MergedIncidents().All;
        var snapshot = CrashMetricsCalculator.Build(merged, _retainedReportIncidents, referenceTime - range, referenceTime, _newIncidentIds, merged);
        _metrics.SetMetrics(snapshot, _workspace.GetFilter().Range ?? "Selected range");
    }
    /// <summary>
    /// Refreshes the always-visible band. Cheap by design: it reads the incident
    /// list already in memory and the monitor's last published sample, so it can
    /// run on a timer without doing any collection of its own.
    /// </summary>
    private void UpdateStatusBand()
    {
        if (IsDisposed || Disposing) return;
        try
        {
            var status = MachineStatusBuilder.Build(
                MergedIncidents().All,
                _liveMonitor.LatestSample,
                _liveMonitor.IsMonitoring,
                TimeSpan.FromMilliseconds(Environment.TickCount64),
                DateTimeOffset.Now);
            _statusBand.SetStatus(status);
        }
        catch { /* Ambient chrome must never be able to break the workspace. */ }
    }

    private void UpdateSelection()
    {
        if (_timeline.SelectedItems.Count == 0) return;
        var i = (Incident)_timeline.SelectedItems[0].Tag!;
        var merged = MergedIncidents().All;
        var related = merged.Count(other => other.Id != i.Id && IncidentSignature(other).Equals(IncidentSignature(i), StringComparison.OrdinalIgnoreCase));
        _incidentView.ShowIncident(i, _retainedReportIncidents.Any(saved => saved.Id == i.Id), related);
    }

    private static string IncidentSignature(Incident incident)
    {
        if (!string.IsNullOrWhiteSpace(incident.BugCheckCode)) return $"code:{incident.BugCheckCode}";
        if (incident.Kind == IncidentKind.ApplicationCrash && incident.Title.StartsWith("Application crash", StringComparison.OrdinalIgnoreCase))
        {
            var app = incident.Title["Application crash".Length..].Trim(' ', '-', '—', '–', ':');
            if (app.Length > 0) return $"app:{app}";
        }
        return $"kind:{incident.Kind}";
    }
    private void PopulateEvidence(EvidenceReport report) => _evidenceView.Show(report);
    private void AddOrUpdateEvidence(string category, EvidenceState state, string detail) => _evidenceView.AddOrUpdate(category, state, detail);
    private async Task SaveSettingsAsync(bool showConfirmation = true) { _settings.MinutesBefore = (int)_before.Value; _settings.MinutesAfterStartup = (int)_after.Value; _settings.AnalyzeCrashDumps = _analyzeDumps.Checked; _settings.DebuggerTimeoutSeconds = (int)_debuggerTimeout.Value; _settings.IncludeFullMemoryDump = _fullDump.Checked; _settings.RedactAccountName = _redact.Checked; _settings.LookUpInstalledProgramsOnline = _webLookup.Checked; _settings.StartMonitoringOnLaunch = _monitorAutoStart.Checked; _settings.AlwaysRunElevated = _alwaysElevate.Checked; _settings.OutputRoot = _output.Text.Trim(); if (_startWithWindows.Checked != StartupRegistration.IsEnabled() && StartupRegistration.SetEnabled(_startWithWindows.Checked) is { } startupError) { MessageBox.Show(this, "The start-with-Windows entry could not be updated: " + startupError, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); _startWithWindows.Checked = StartupRegistration.IsEnabled(); } _settings.TestDataDirectory = _testMode.Checked ? _testData.Text.Trim() : null; await SettingsStore.SaveAsync(_settings); _modeBanner.Visible = _settings.IsTestDataMode; _modeBanner.Text = _settings.IsTestDataMode ? "TEST-DATA MODE — reading copied sample logs only" : string.Empty; if (showConfirmation) MessageBox.Show(this, "Settings saved. Restart the app to re-run incident detection with a changed data source.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
    private void LoadSettingsControls() { _before.Value = Math.Clamp(_settings.MinutesBefore, 1, 1440); _after.Value = Math.Clamp(_settings.MinutesAfterStartup, 1, 1440); _analyzeDumps.Checked = _settings.AnalyzeCrashDumps; _debuggerTimeout.Value = Math.Clamp(_settings.DebuggerTimeoutSeconds, 15, 1800); _fullDump.Checked = _settings.IncludeFullMemoryDump; _redact.Checked = _settings.RedactAccountName; _webLookup.Checked = _settings.LookUpInstalledProgramsOnline; _monitorAutoStart.Checked = _settings.StartMonitoringOnLaunch; _alwaysElevate.Checked = _settings.AlwaysRunElevated; _startWithWindows.Checked = StartupRegistration.IsEnabled(); _output.Text = _settings.OutputRoot; _testMode.Checked = _settings.IsTestDataMode; _testData.Text = _settings.TestDataDirectory ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestData")); _modeBanner.Visible = _settings.IsTestDataMode; _modeBanner.Text = _settings.IsTestDataMode ? "TEST-DATA MODE — reading copied sample logs only" : string.Empty; }
    private void ToggleCollecting(bool collecting)
    {
        _reportPane.SetCollectionActions(_lastResult is not null, collecting);
    }
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

    private void RestartElevated()
    {
        if (ElevationService.IsElevated) return;
        var error = ElevationService.Relaunch();
        if (error is null) { Close(); return; }
        MessageBox.Show(this, error + " The app continues unelevated; protected dumps will still be offered through the separate helper during collection.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void PrintOrSavePdf()
    {
        if (_lastResult is null || !File.Exists(_lastResult.HtmlPath)) return;
        if (_reportPane.Print()) { _progressText.Text = "Choose Microsoft Print to PDF in the print dialog to save a PDF."; return; }
        _progressText.Text = "The report opened in your default browser — press Ctrl+P and choose Microsoft Print to PDF to save a PDF.";
        Process.Start(new ProcessStartInfo(_lastResult.HtmlPath) { UseShellExecute = true });
    }
    private static void BrowseFolder(TextBox target) { using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(target.Text) ? target.Text : string.Empty, ShowNewFolderButton = true }; if (dialog.ShowDialog() == DialogResult.OK) target.Text = dialog.SelectedPath; }
    private static Panel Card(Control child) { var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(0, 6, 0, 6) }; panel.Controls.Add(child); return panel; }
    private static TabPage Page() => new() { BackColor = UiTheme.Canvas, Padding = new Padding(0), UseVisualStyleBackColor = false };
    private static Button PrimaryButton(string text) => new() { Text = text, AutoSize = true, Height = 36, Padding = new Padding(12, 4, 12, 4), BackColor = Color.FromArgb(40, 105, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };
    internal static Button SecondaryButton(string text) => new() { Text = text, AutoSize = true, Height = 36, Padding = new Padding(10, 4, 10, 4), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };
    private static Button NavButton(string text) => new() { Text = text, Dock = DockStyle.Top, Height = 52, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(164, 184, 207), BackColor = UiTheme.Nav, Padding = new Padding(12, 0, 0, 0), Font = new Font("Segoe UI Semibold", 8.7f), Cursor = Cursors.Hand };
    private static FlowLayoutPanel Row(string label, params Control[] controls) { var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 10) }; row.Controls.Add(new Label { Text = label, Width = 230, Height = 32, TextAlign = ContentAlignment.MiddleLeft }); row.Controls.AddRange(controls); return row; }
}
