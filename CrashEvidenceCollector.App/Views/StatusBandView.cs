using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// The strip that never leaves. It sits above every page so the answer to "is
/// this machine alright?" is already on screen rather than somewhere you have to
/// navigate to.
///
/// Two rules keep it from becoming noise. It is drawn quiet — muted labels, one
/// coloured word — and colour is reserved for state, never for decoration. A
/// console that shouts everywhere is a wall of noise.
///
/// Owner-drawn rather than composed from labels: the cells are measured from the
/// text they hold, which keeps them aligned at any DPI without depending on
/// automatic scaling.
/// </summary>
internal sealed class StatusBandView : Control
{
    private static readonly Font LabelFont = new("Segoe UI", 7.6f, FontStyle.Regular);
    private static readonly Font ValueFont = new("Cascadia Mono", 10f, FontStyle.Regular);
    private static readonly Font HealthFont = new("Segoe UI Semibold", 12.5f);
    private static readonly Font AttentionFont = new("Segoe UI", 9f);

    private MachineStatusSnapshot? _status;
    private readonly ToolTip _tips = new() { AutoPopDelay = 20000, InitialDelay = 350, ReshowDelay = 120 };
    private Rectangle _attentionBounds = Rectangle.Empty;
    private string _lastTip = string.Empty;

    /// <summary>Raised when the attention chip is clicked, so the shell can open what matters.</summary>
    public event Action? AttentionClicked;

    public StatusBandView()
    {
        // A plain Control carries no automatic scaling of its own, which is what
        // the surrounding layout engine needs; everything here is measured instead.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Top;
        Height = 62;
        BackColor = UiTheme.Surface;
        Cursor = Cursors.Default;
    }

    public void SetStatus(MachineStatusSnapshot status)
    {
        _status = status;
        var tip = status.HealthReason + (status.Attention.Count == 0 ? string.Empty : "\r\n\r\n" + string.Join("\r\n", status.Attention));
        if (tip != _lastTip) { _tips.SetToolTip(this, tip); _lastTip = tip; }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = _attentionBounds.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_attentionBounds.Contains(e.Location)) AttentionClicked?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(UiTheme.SurfaceRaised);

        var health = _status?.Health ?? MachineHealth.Unknown;
        var accent = HealthColor(health);

        // A three-pixel rule across the top is the whole alarm system: it is the
        // one thing that changes colour when the machine's state changes.
        using (var rule = new SolidBrush(accent)) g.FillRectangle(rule, 0, 0, Width, 3);
        using (var edge = new Pen(UiTheme.Border)) g.DrawLine(edge, 0, Height - 1, Width, Height - 1);

        var x = 18;
        TextRenderer.DrawText(g, "STATUS", LabelFont, new Point(x, 14), UiTheme.Faint);
        var healthText = MachineStatusBuilder.HealthLabel(health);
        TextRenderer.DrawText(g, healthText, HealthFont, new Point(x - 1, 28), accent);
        x += Math.Max(118, TextRenderer.MeasureText(healthText, HealthFont).Width + 24);

        foreach (var reading in _status?.Readings ?? [])
        {
            using (var divider = new Pen(UiTheme.Border)) g.DrawLine(divider, x - 12, 16, x - 12, Height - 16);
            TextRenderer.DrawText(g, reading.Label.ToUpperInvariant(), LabelFont, new Point(x, 14), UiTheme.Faint);
            TextRenderer.DrawText(g, reading.Value, ValueFont, new Point(x - 1, 29), SeverityColor(reading.Severity));
            var width = Math.Max(TextRenderer.MeasureText(reading.Label.ToUpperInvariant(), LabelFont).Width,
                                 TextRenderer.MeasureText(reading.Value, ValueFont).Width);
            x += width + 30;
        }

        DrawAttention(g, x);
    }

    /// <summary>
    /// The right-hand chip. It is absent — not empty, not a reassuring tick —
    /// when there is nothing to act on, so its presence always means something.
    /// </summary>
    private void DrawAttention(Graphics g, int leftLimit)
    {
        _attentionBounds = Rectangle.Empty;
        var attention = _status?.Attention;
        if (attention is null || attention.Count == 0) return;

        var headline = attention.Count == 1 ? attention[0] : $"{attention.Count} things need attention  ›";
        var width = Math.Min(TextRenderer.MeasureText(headline, AttentionFont).Width + 28, Math.Max(0, Width - leftLimit - 24));
        if (width < 90) return;

        var bounds = new Rectangle(Width - width - 16, 13, width, Height - 27);
        _attentionBounds = bounds;
        using var wash = new SolidBrush(Wash(_status!.Health));
        g.FillRectangle(wash, bounds);
        TextRenderer.DrawText(g, headline, AttentionFont, Rectangle.Inflate(bounds, -12, 0), HealthColor(_status.Health),
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static Color HealthColor(MachineHealth health) => health switch
    {
        MachineHealth.Stable => UiTheme.Success,
        MachineHealth.Watch => UiTheme.Warning,
        MachineHealth.Degrading => UiTheme.Warning,
        MachineHealth.Critical => UiTheme.Danger,
        _ => UiTheme.Muted
    };

    private static Color Wash(MachineHealth health) => health switch
    {
        MachineHealth.Stable => Color.FromArgb(236, 247, 241),
        MachineHealth.Watch or MachineHealth.Degrading => Color.FromArgb(255, 248, 235),
        MachineHealth.Critical => Color.FromArgb(253, 241, 242),
        _ => UiTheme.Canvas
    };

    private static Color SeverityColor(StatusSeverity severity) => severity switch
    {
        StatusSeverity.Good => UiTheme.Ink,
        StatusSeverity.Caution => UiTheme.Warning,
        StatusSeverity.Bad => UiTheme.Danger,
        _ => UiTheme.Ink
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }
}
