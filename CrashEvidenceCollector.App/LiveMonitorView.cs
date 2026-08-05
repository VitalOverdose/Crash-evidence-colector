using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Live background-monitoring workspace: rolling temperature/clock/load charts
/// with hardware-event markers, current-value cards, and the recent hardware
/// event list. Sampling continues while the app stays open; every sample is
/// already on disk, so the history survives a bugcheck and feeds the next report.
/// </summary>
internal sealed class LiveMonitorView : UserControl
{
    private readonly SystemMonitorService _service = new();
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 2000 };
    private readonly Button _toggle = new() { Text = "Start monitoring", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(88, 101, 118), Padding = new Padding(10, 8, 0, 0) };
    private readonly Label _temperature = StatValue();
    private readonly Label _utility = StatValue();
    private readonly Label _clock = StatValue();
    private readonly Label _memory = StatValue();
    private readonly Label _hardwareEvents = StatValue();
    private readonly ToolTip _tips = new();
    private readonly MonitorLineChart _thermalChart = new("Temperature · HWiNFO package / ACPI zone", "°C") { Dock = DockStyle.Fill, Margin = new Padding(5) };
    private readonly MonitorLineChart _cpuChart = new("CPU utility and effective clock", "%") { Dock = DockStyle.Fill, Margin = new Padding(5) };
    private readonly MonitorLineChart _memoryChart = new("Memory load and commit charge", "%") { Dock = DockStyle.Fill, Margin = new Padding(5) };
    private readonly ListView _events = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.None };

    public LiveMonitorView()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(246, 248, 251);
        Padding = new Padding(6);
        _events.Columns.Add("Time", 130); _events.Columns.Add("Source / ID", 210); _events.Columns.Add("Hardware event", 560);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _toggle.Click += (_, _) => Toggle();
        header.Controls.Add(_toggle); header.Controls.Add(_status);
        root.Controls.Add(header, 0, 0);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty };
        for (var index = 0; index < 5; index++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        cards.Controls.Add(Card("Temperature · best available source", _temperature), 0, 0);
        cards.Controls.Add(Card("CPU utility", _utility), 1, 0);
        cards.Controls.Add(Card("Effective clock", _clock), 2, 0);
        cards.Controls.Add(Card("Memory · commit charge", _memory), 3, 0);
        cards.Controls.Add(Card("Hardware events (12 h)", _hardwareEvents), 4, 0);
        root.Controls.Add(cards, 0, 1);

        var charts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        charts.Controls.Add(_thermalChart, 0, 0);
        charts.Controls.Add(_cpuChart, 1, 0);
        charts.Controls.Add(_memoryChart, 2, 0);
        root.Controls.Add(charts, 0, 2);

        var eventsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(8) };
        eventsPanel.Controls.Add(_events);
        eventsPanel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 22, Text = "Hardware events captured live (WHEA, thermal, power)", Font = new Font("Segoe UI Semibold", 10), ForeColor = Color.FromArgb(20, 35, 59) });
        root.Controls.Add(eventsPanel, 0, 3);
        Controls.Add(root);

        _status.Text = "Monitoring is off. Samples are written crash-safe to disk so the window before a crash appears in the next report.";
        _refresh.Tick += (_, _) => Render();
    }

    /// <summary>Starts monitoring if it is not already running (used by the auto-start setting).</summary>
    public void StartMonitoring() { if (!_service.IsRunning) Toggle(); }

    private void Toggle()
    {
        if (_service.IsRunning)
        {
            _service.Stop(); _refresh.Stop();
            _toggle.Text = "Start monitoring";
            _status.Text = "Monitoring stopped. Existing history is retained for " + MonitorLog.RetentionDays + " days.";
            return;
        }
        _service.Start(); _refresh.Start();
        _toggle.Text = "Stop monitoring";
        var hwinfo = HwInfoGadgetReader.Read() is not null;
        _status.Text = hwinfo
            ? "Monitoring every 2 s — HWiNFO gadget sensors detected (package temperature, voltages, pump)."
            : "Monitoring every 2 s. For pump RPM, Vcore and true package temperature, enable HWiNFO → Settings → \"HWiNFO Gadget\" reporting; BSODGuru cannot read those sensors without it.";
    }

    private void Render()
    {
        var (samples, events) = _service.Snapshot();
        var latest = samples.Count > 0 ? samples[^1] : null;
        var temperature = latest is null ? null : HwInfoGadgetReader.SelectCpuTemperature(latest);
        _temperature.Text = temperature is null ? "—" : $"{temperature:0} °C" + (latest?.HwInfoSensors is null ? " (ACPI zone)" : string.Empty);
        _temperature.ForeColor = temperature >= 95 ? Color.Firebrick : temperature >= 85 ? Color.DarkGoldenrod : Color.FromArgb(20, 35, 59);
        _utility.Text = latest?.CpuUtilityPercent is { } utility ? $"{utility:0.0} %" : "—";
        _clock.Text = latest?.CpuEffectiveMhz is { } mhz ? $"{mhz:N0} MHz" : "—";
        _memory.Text = latest is { MemoryLoadPercent: { } load } ? $"{load:0}% · {(latest.CommitPercent is { } commit ? $"{commit:0}%" : "—")}" : "—";
        _memory.ForeColor = latest?.CommitPercent >= 90 ? Color.Firebrick : Color.FromArgb(20, 35, 59);
        _tips.SetToolTip(_memory, latest is null ? string.Empty : $"Physical memory load / commit charge. Kernel pool: nonpaged {latest.PoolNonpagedMb?.ToString("N0") ?? "—"} MB, paged {latest.PoolPagedMb?.ToString("N0") ?? "—"} MB.");
        _hardwareEvents.Text = events.Count.ToString("N0");
        _hardwareEvents.ForeColor = events.Any(record => record.Provider.Contains("WHEA", StringComparison.OrdinalIgnoreCase)) ? Color.Firebrick : Color.FromArgb(20, 35, 59);

        _thermalChart.SetData(
            samples.Select(sample => (sample.Timestamp, HwInfoGadgetReader.SelectCpuTemperature(sample))).ToList(),
            samples.Select(sample => (sample.Timestamp, (double?)null)).ToList(),
            events.Select(record => record.Timestamp).ToList());
        _cpuChart.SetData(
            samples.Select(sample => (sample.Timestamp, sample.CpuUtilityPercent)).ToList(),
            samples.Select(sample => (sample.Timestamp, sample.CpuPerformancePercent)).ToList(),
            events.Select(record => record.Timestamp).ToList());
        _memoryChart.SetData(
            samples.Select(sample => (sample.Timestamp, sample.MemoryLoadPercent)).ToList(),
            samples.Select(sample => (sample.Timestamp, sample.CommitPercent)).ToList(),
            events.Select(record => record.Timestamp).ToList());

        _events.BeginUpdate(); _events.Items.Clear();
        foreach (var record in events.OrderByDescending(record => record.Timestamp).Take(60))
        {
            var item = new ListViewItem(record.Timestamp.ToLocalTime().ToString("G"));
            item.SubItems.Add($"{record.Provider} / {record.EventId}");
            item.SubItems.Add(record.Summary);
            if (record.Provider.Contains("WHEA", StringComparison.OrdinalIgnoreCase)) item.ForeColor = Color.Firebrick;
            _events.Items.Add(item);
        }
        _events.EndUpdate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _refresh.Stop(); _refresh.Dispose(); _service.Dispose(); _tips.Dispose(); }
        base.Dispose(disposing);
    }

    private static Control Card(string title, Label value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(12, 8, 12, 8) };
        panel.Controls.Add(value);
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 24, Text = title, ForeColor = Color.FromArgb(88, 101, 118), Font = new Font("Segoe UI", 9.5f), AutoEllipsis = true });
        return panel;
    }

    private static Label StatValue() => new()
    {
        Dock = DockStyle.Fill,
        Text = "—",
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(20, 35, 59),
        // Sized so a five-card row still fits values like "98 °C" and "38% · 52%".
        Font = new Font("Segoe UI Semibold", 24),
        AutoEllipsis = true
    };
}

/// <summary>
/// Rolling dual-series line chart with vertical markers for hardware events.
/// Series 2 is optional; missing points break the line rather than being
/// interpolated, so sensor gaps stay visibly honest.
/// </summary>
internal sealed class MonitorLineChart(string title, string unit) : MetricsChart
{
    private IReadOnlyList<(DateTimeOffset Time, double? Value)> _primary = [];
    private IReadOnlyList<(DateTimeOffset Time, double? Value)> _secondary = [];
    private IReadOnlyList<DateTimeOffset> _markers = [];

    public void SetData(IReadOnlyList<(DateTimeOffset, double?)> primary, IReadOnlyList<(DateTimeOffset, double?)> secondary, IReadOnlyList<DateTimeOffset> markers)
    { _primary = primary; _secondary = secondary; _markers = markers; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle; bounds.Inflate(-14, -10);
        Title(e.Graphics, title, new Rectangle(bounds.X, bounds.Y, bounds.Width, 22));
        var plot = new Rectangle(bounds.X + 34, bounds.Y + 30, Math.Max(10, bounds.Width - 40), Math.Max(10, bounds.Height - 56));
        var known = _primary.Concat(_secondary).Where(point => point.Value is not null).Select(point => point.Value!.Value).ToList();
        if (known.Count == 0) { Empty(e.Graphics, plot, "No sensor values yet. Start monitoring, and enable the HWiNFO gadget for temperature/voltage/pump sensors."); return; }

        var minimum = Math.Floor(Math.Min(known.Min(), 0) / 10) * 10;
        var maximum = Math.Ceiling(Math.Max(known.Max(), 10) / 10) * 10;
        if (Math.Abs(maximum - minimum) < 1) maximum = minimum + 1;
        using var gridPen = new Pen(Grid);
        for (var line = 0; line <= 4; line++)
        {
            var y = plot.Bottom - line * plot.Height / 4;
            e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            TextRenderer.DrawText(e.Graphics, $"{minimum + (maximum - minimum) * line / 4:0}", ChartTinyFont, new Rectangle(bounds.X, y - 8, 32, 16), Muted, TextFormatFlags.Right);
        }
        TextRenderer.DrawText(e.Graphics, unit, ChartTinyFont, new Rectangle(bounds.X, bounds.Y + 4, 32, 16), Muted, TextFormatFlags.Right);

        var window = _primary.Count > 1 ? (_primary[^1].Time - _primary[0].Time).TotalSeconds : 1;
        if (window <= 0) window = 1;
        float X(DateTimeOffset time) => plot.Left + (float)((time - _primary[0].Time).TotalSeconds / window) * plot.Width;
        float Y(double value) => plot.Bottom - (float)((value - minimum) / (maximum - minimum)) * plot.Height;

        using var markerPen = new Pen(Color.FromArgb(150, 197, 67, 67)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        foreach (var marker in _markers.Where(time => _primary.Count > 0 && time >= _primary[0].Time))
            e.Graphics.DrawLine(markerPen, X(marker), plot.Top, X(marker), plot.Bottom);

        DrawSeries(e.Graphics, _primary, SeriesColors[0], X, Y);
        DrawSeries(e.Graphics, _secondary, SeriesColors[1], X, Y);
        if (_primary.Count > 1)
        {
            TextRenderer.DrawText(e.Graphics, _primary[0].Time.ToLocalTime().ToString("HH:mm:ss"), ChartTinyFont, new Rectangle(plot.Left, plot.Bottom + 3, 80, 16), Muted, TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, _primary[^1].Time.ToLocalTime().ToString("HH:mm:ss"), ChartTinyFont, new Rectangle(plot.Right - 80, plot.Bottom + 3, 80, 16), Muted, TextFormatFlags.Right);
        }
    }

    private static void DrawSeries(Graphics graphics, IReadOnlyList<(DateTimeOffset Time, double? Value)> series, Color color, Func<DateTimeOffset, float> x, Func<double, float> y)
    {
        using var pen = new Pen(color, 1.8f);
        PointF? previous = null;
        foreach (var (time, value) in series)
        {
            if (value is null) { previous = null; continue; }
            var current = new PointF(x(time), y(value.Value));
            if (previous is not null) graphics.DrawLine(pen, previous.Value, current);
            previous = current;
        }
    }
}
