using System.Drawing.Drawing2D;
using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Compact, code-drawn dashboard. It has no browser or chart-package
/// dependency and scales with the WinForms workspace.
/// </summary>
internal sealed class IncidentMetricsView : UserControl
{
    private static readonly Color Navy = Color.FromArgb(20, 35, 59);
    private static readonly Color Muted = Color.FromArgb(88, 101, 118);
    private readonly Label _incidents = ValueLabel();
    private readonly Label _bugchecks = ValueLabel();
    private readonly Label _newIncidents = ValueLabel();
    private readonly Label _retained = ValueLabel();
    private readonly DailyIncidentChart _daily = new() { Dock = DockStyle.Fill, Margin = new Padding(5) };
    private readonly IncidentTypeDonut _types = new() { Dock = DockStyle.Fill, Margin = new Padding(5) };
    private readonly TopBugCheckChart _codes = new() { Dock = DockStyle.Fill, Margin = new Padding(5) };

    public IncidentMetricsView()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(246, 248, 251);
        Padding = new Padding(6);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        for (var index = 0; index < 4; index++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.Controls.Add(Card("Incidents in range", _incidents), 0, 0);
        cards.Controls.Add(Card("Bugchecks", _bugchecks), 1, 0);
        cards.Controls.Add(Card("New since last run", _newIncidents), 2, 0);
        cards.Controls.Add(Card("Retained reports", _retained), 3, 0);

        var charts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        charts.RowStyles.Add(new RowStyle(SizeType.Percent, 57));
        charts.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        charts.Controls.Add(_daily, 0, 0);
        charts.SetColumnSpan(_daily, 2);
        charts.Controls.Add(_types, 0, 1);
        charts.Controls.Add(_codes, 1, 1);

        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(charts, 0, 1);
        Controls.Add(root);
    }

    public void SetMetrics(CrashMetricsSnapshot snapshot, string rangeLabel)
    {
        _incidents.Text = snapshot.IncidentsInRange.ToString("N0");
        _incidents.Tag = rangeLabel;
        _bugchecks.Text = snapshot.BugChecksInRange.ToString("N0");
        _newIncidents.Text = snapshot.NewIncidentsInRange.ToString("N0");
        _retained.Text = snapshot.RetainedIncidentCount.ToString("N0");
        _daily.SetData(snapshot.DailyIncidentMix);
        _types.SetData(snapshot.IncidentTypes, rangeLabel);
        _codes.SetData(snapshot.TopBugChecks);
    }

    private static Control Card(string title, Label value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(12, 8, 12, 8) };
        var caption = new Label { Dock = DockStyle.Top, Height = 24, Text = title, ForeColor = Muted, Font = new Font("Segoe UI", 9.5f), AutoEllipsis = true };
        panel.Controls.Add(value);
        panel.Controls.Add(caption);
        return panel;
    }

    private static Label ValueLabel() => new()
    {
        Dock = DockStyle.Fill,
        Text = "0",
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Navy,
        Font = new Font("Segoe UI Semibold", 28)
    };
}

internal abstract class MetricsChart : Control
{
    protected static readonly Color Navy = Color.FromArgb(20, 35, 59);
    protected static readonly Color Muted = Color.FromArgb(88, 101, 118);
    protected static readonly Color Grid = Color.FromArgb(225, 230, 237);
    protected static readonly Font ChartTitleFont = new("Segoe UI Semibold", 10);
    protected static readonly Font ChartTextFont = new("Segoe UI", 7.8f);
    protected static readonly Font ChartTinyFont = new("Segoe UI", 7.2f);
    protected static readonly Font ChartValueFont = new("Segoe UI Semibold", 12);
    protected static readonly Color[] SeriesColors =
    [
        Color.FromArgb(40, 105, 190),
        Color.FromArgb(226, 142, 42),
        Color.FromArgb(197, 67, 67),
        Color.FromArgb(74, 155, 105)
    ];

    protected MetricsChart()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        ForeColor = Navy;
        ResizeRedraw = true;
    }

    protected static void Title(Graphics graphics, string title, Rectangle bounds)
        => TextRenderer.DrawText(graphics, title, ChartTitleFont, bounds, Navy, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

    protected static void Empty(Graphics graphics, Rectangle bounds, string text)
        => TextRenderer.DrawText(graphics, text, SystemFonts.MessageBoxFont, bounds, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        base.OnPaint(e);
    }
}

internal sealed class DailyIncidentChart : MetricsChart
{
    private IReadOnlyList<CrashDailyMetric> _data = [];

    public void SetData(IReadOnlyList<CrashDailyMetric> data) { _data = data; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle;
        bounds.Inflate(-14, -10);
        Title(e.Graphics, "Daily incident types · last 14 days", new Rectangle(bounds.X, bounds.Y, bounds.Width, 22));
        DrawLegend(e.Graphics, new Rectangle(bounds.X, bounds.Y + 23, bounds.Width, 20));
        var plot = new Rectangle(bounds.X + 28, bounds.Y + 50, Math.Max(10, bounds.Width - 34), Math.Max(10, bounds.Height - 76));
        if (_data.Count == 0) { Empty(e.Graphics, plot, "No daily incident history is available."); return; }

        var maximum = Math.Max(1, _data.Max(item => item.Total));
        using var gridPen = new Pen(Grid);
        for (var line = 0; line <= 2; line++)
        {
            var y = plot.Bottom - line * plot.Height / 2;
            e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            TextRenderer.DrawText(e.Graphics, (maximum * line / 2d).ToString("0"), ChartTextFont, new Rectangle(bounds.X, y - 8, 25, 16), Muted, TextFormatFlags.Right);
        }

        var slot = plot.Width / (float)_data.Count;
        var barWidth = Math.Max(4f, slot * .64f);
        for (var index = 0; index < _data.Count; index++)
        {
            var item = _data[index];
            var x = plot.Left + slot * index + (slot - barWidth) / 2;
            var bottom = (float)plot.Bottom;
            var values = new[] { item.BugChecks, item.PowerOrShutdown, item.Hardware, item.Applications };
            for (var series = 0; series < values.Length; series++)
            {
                if (values[series] == 0) continue;
                var height = Math.Max(2f, values[series] / (float)maximum * plot.Height);
                bottom -= height;
                using var brush = new SolidBrush(SeriesColors[series]);
                e.Graphics.FillRectangle(brush, x, bottom, barWidth, height);
            }
            if (index % 2 == 0 || index == _data.Count - 1)
                TextRenderer.DrawText(e.Graphics, item.Label, ChartTinyFont, new Rectangle((int)(x - slot / 2), plot.Bottom + 3, (int)(slot * 2), 18), Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private static void DrawLegend(Graphics graphics, Rectangle bounds)
    {
        var labels = new[] { "Bugcheck", "Power/shutdown", "Hardware", "Application" };
        var x = bounds.X;
        for (var index = 0; index < labels.Length; index++)
        {
            using var brush = new SolidBrush(SeriesColors[index]);
            graphics.FillRectangle(brush, x, bounds.Y + 4, 10, 10);
            var width = TextRenderer.MeasureText(labels[index], ChartTextFont).Width;
            TextRenderer.DrawText(graphics, labels[index], ChartTextFont, new Point(x + 13, bounds.Y), Muted);
            x += width + 20;
            if (x > bounds.Right - 80) break;
        }
    }
}

internal sealed class IncidentTypeDonut : MetricsChart
{
    private IReadOnlyList<CrashMetricPoint> _data = [];
    private string _range = "selected range";

    public void SetData(IReadOnlyList<CrashMetricPoint> data, string range) { _data = data; _range = range; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle; bounds.Inflate(-12, -9);
        Title(e.Graphics, $"Incident mix · {_range}", new Rectangle(bounds.X, bounds.Y, bounds.Width, 22));
        var total = _data.Sum(item => item.Value);
        if (total == 0) { Empty(e.Graphics, new Rectangle(bounds.X, bounds.Y + 24, bounds.Width, bounds.Height - 24), "No incidents in this range."); return; }
        var diameter = Math.Max(44, Math.Min(bounds.Height - 38, bounds.Width / 2 - 12));
        var pie = new Rectangle(bounds.X + 4, bounds.Y + 30, diameter, diameter);
        var start = -90f;
        for (var index = 0; index < _data.Count; index++)
        {
            var sweep = _data[index].Value / (float)total * 360f;
            if (sweep <= 0) continue;
            using var brush = new SolidBrush(SeriesColors[index % SeriesColors.Length]);
            e.Graphics.FillPie(brush, pie, start, sweep);
            start += sweep;
        }
        var inner = pie; inner.Inflate(-diameter / 4, -diameter / 4);
        e.Graphics.FillEllipse(Brushes.White, inner);
        TextRenderer.DrawText(e.Graphics, total.ToString("N0"), ChartValueFont, inner, Navy, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var legendX = pie.Right + 10; var y = bounds.Y + 31;
        for (var index = 0; index < _data.Count; index++)
        {
            using var brush = new SolidBrush(SeriesColors[index % SeriesColors.Length]);
            e.Graphics.FillRectangle(brush, legendX, y + 4, 9, 9);
            TextRenderer.DrawText(e.Graphics, $"{_data[index].Label}: {_data[index].Value}", ChartTextFont, new Rectangle(legendX + 13, y, Math.Max(20, bounds.Right - legendX - 13), 18), Muted, TextFormatFlags.EndEllipsis);
            y += 19;
        }
    }
}

internal sealed class TopBugCheckChart : MetricsChart
{
    private IReadOnlyList<CrashMetricPoint> _data = [];
    public void SetData(IReadOnlyList<CrashMetricPoint> data) { _data = data; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle; bounds.Inflate(-12, -9);
        Title(e.Graphics, "Top bugchecks · retained history", new Rectangle(bounds.X, bounds.Y, bounds.Width, 22));
        var chart = new Rectangle(bounds.X, bounds.Y + 28, bounds.Width, bounds.Height - 32);
        if (_data.Count == 0) { Empty(e.Graphics, chart, "Collect a bugcheck report to build retained history."); return; }
        var maximum = Math.Max(1, _data.Max(item => item.Value));
        var rowHeight = Math.Max(18, chart.Height / Math.Max(1, _data.Count));
        var labelWidth = Math.Clamp(chart.Width * 58 / 100, 90, 260);
        for (var index = 0; index < _data.Count; index++)
        {
            var item = _data[index]; var y = chart.Y + index * rowHeight;
            TextRenderer.DrawText(e.Graphics, item.Label, ChartTextFont, new Rectangle(chart.X, y, labelWidth - 5, rowHeight), Navy, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var bar = new Rectangle(chart.X + labelWidth, y + 5, Math.Max(2, (chart.Width - labelWidth - 25) * item.Value / maximum), Math.Max(7, rowHeight - 10));
            using var brush = new SolidBrush(Color.FromArgb(76, 132, 204));
            e.Graphics.FillRectangle(brush, bar);
            TextRenderer.DrawText(e.Graphics, item.Value.ToString(), ChartTextFont, new Rectangle(bar.Right + 4, y, 22, rowHeight), Muted, TextFormatFlags.VerticalCenter);
        }
    }
}
