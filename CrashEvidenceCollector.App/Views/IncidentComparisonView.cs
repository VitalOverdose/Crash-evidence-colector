using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// A small, persistent comparison board. Analysts can keep up to four incidents
/// in view while moving through report, evidence and web-research tabs.
/// </summary>
internal sealed class IncidentComparisonView : UserControl
{
    private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(14, 8, 14, 14) };
    private readonly Label _status = new() { AutoSize = true, ForeColor = UiTheme.Muted, Padding = new Padding(10, 10, 0, 0) };
    private readonly List<Incident> _incidents = [];

    public IncidentComparisonView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Canvas;
        var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UiTheme.Surface, Padding = new Padding(18, 10, 18, 8) };
        var clear = UiTheme.ActionButton("Clear board");
        clear.Dock = DockStyle.Right;
        clear.Click += (_, _) => { _incidents.Clear(); Render(); };
        header.Controls.Add(clear);
        header.Controls.Add(_status);
        header.Controls.Add(new Label { Dock = DockStyle.Top, Height = 26, Text = "Incident comparison board", Font = new Font("Segoe UI Semibold", 15f), ForeColor = UiTheme.Ink });
        Controls.Add(_cards);
        Controls.Add(header);
        Render();
    }

    public event Action<Incident>? IncidentRequested;

    public void Add(Incident incident)
    {
        if (_incidents.Any(item => item.Id.Equals(incident.Id, StringComparison.Ordinal))) return;
        if (_incidents.Count == 4) _incidents.RemoveAt(0);
        _incidents.Add(incident);
        Render();
    }

    private void Render()
    {
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _status.Text = _incidents.Count == 0
            ? "Pin incidents from the timeline briefing to compare codes, meanings, timing and sources."
            : $"{_incidents.Count} pinned • click Open in timeline to return to the full evidence trail";
        foreach (var incident in _incidents) _cards.Controls.Add(BuildCard(incident));
        _cards.ResumeLayout();
    }

    private Control BuildCard(Incident incident)
    {
        var card = new Panel { Width = 335, Height = 330, BackColor = UiTheme.Surface, Margin = new Padding(6), Padding = new Padding(18) };
        var stripe = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = UiTheme.KindColor(incident.Kind) };
        var open = UiTheme.ActionButton("Open in timeline", true);
        open.Dock = DockStyle.Bottom;
        open.Click += (_, _) => IncidentRequested?.Invoke(incident);
        var remove = UiTheme.ActionButton("Remove");
        remove.Dock = DockStyle.Bottom;
        remove.Click += (_, _) => { _incidents.RemoveAll(item => item.Id == incident.Id); Render(); };
        var details = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Muted,
            Font = new Font("Segoe UI", 9f),
            Text = $"{incident.Timestamp.ToLocalTime():ddd d MMM yyyy, HH:mm:ss}\r\n\r\n{CodeDecoder.DescribeIncident(incident)}\r\n\r\nSource: {incident.Source}",
            AutoEllipsis = true
        };
        var code = new Label { Dock = DockStyle.Top, Height = 36, Text = CodeDecoder.GetBugCheckLabel(incident.BugCheckCode), Font = new Font("Cascadia Mono", 9f, FontStyle.Bold), ForeColor = UiTheme.KindColor(incident.Kind), AutoEllipsis = true };
        var title = new Label { Dock = DockStyle.Top, Height = 48, Text = incident.Title, Font = new Font("Segoe UI Semibold", 12f), ForeColor = UiTheme.Ink, AutoEllipsis = true };
        var kind = new Label { Dock = DockStyle.Top, Height = 25, Text = UiTheme.KindLabel(incident.Kind).ToUpperInvariant(), Font = new Font("Segoe UI Semibold", 8f), ForeColor = UiTheme.KindColor(incident.Kind) };
        card.Controls.Add(details);
        card.Controls.Add(remove);
        card.Controls.Add(open);
        card.Controls.Add(code);
        card.Controls.Add(title);
        card.Controls.Add(kind);
        card.Controls.Add(stripe);
        return card;
    }
}
