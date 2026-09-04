using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App.Views;

/// <summary>A concise analyst briefing for the incident selected in the timeline.</summary>
internal sealed class IncidentDetailView : UserControl
{
    private readonly Panel _accent = new() { Dock = DockStyle.Left, Width = 5 };
    private readonly Label _eyebrow = new() { Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = UiTheme.Accent };
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 42, Font = new Font("Segoe UI Semibold", 19f), ForeColor = UiTheme.Ink, AutoEllipsis = true };
    private readonly Label _code = new() { Dock = DockStyle.Top, Height = 30, Font = new Font("Cascadia Mono", 10f, FontStyle.Bold), ForeColor = UiTheme.Danger, AutoEllipsis = true };
    private readonly Label _meaning = new() { Dock = DockStyle.Top, Height = 82, Font = new Font("Segoe UI", 10.5f), ForeColor = UiTheme.Ink };
    private readonly Label _metadata = new() { Dock = DockStyle.Top, Height = 94, Font = new Font("Segoe UI", 9.2f), ForeColor = UiTheme.Muted };
    private readonly Label _related = new() { Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UiTheme.Cyan };
    private readonly FlowLayoutPanel _actions = new() { Dock = DockStyle.Top, Height = 44, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
    private readonly Button _collect = UiTheme.ActionButton("Collect focused evidence", true);
    private readonly Button _compare = UiTheme.ActionButton("Add to comparison");
    private readonly Button _search = UiTheme.ActionButton("Research code");
    private readonly Button _copy = UiTheme.ActionButton("Copy briefing");
    private Incident? _incident;

    public IncidentDetailView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Padding = new Padding(24, 22, 24, 16);

        _actions.Controls.AddRange([_collect, _compare, _search, _copy]);
        var body = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(20, 0, 8, 0) };
        body.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Raw codes are always retained beside these interpretations. Conclusions remain evidence-weighted and never treat a crash marker as proof of root cause.",
            ForeColor = UiTheme.Faint,
            Font = new Font("Segoe UI", 8.5f),
            Padding = new Padding(0, 12, 0, 0)
        });
        body.Controls.Add(_actions);
        body.Controls.Add(_related);
        body.Controls.Add(_metadata);
        body.Controls.Add(_meaning);
        body.Controls.Add(_code);
        body.Controls.Add(_title);
        body.Controls.Add(_eyebrow);
        Controls.Add(body);
        Controls.Add(_accent);

        _collect.Click += (_, _) => { if (_incident is not null) CollectRequested?.Invoke(_incident); };
        _compare.Click += (_, _) => { if (_incident is not null) CompareRequested?.Invoke(_incident); };
        _search.Click += (_, _) => { if (_incident is not null) SearchRequested?.Invoke(SearchText(_incident)); };
        _copy.Click += (_, _) => CopyCurrent();
        ShowEmpty("Select an incident from the timeline to open its analyst briefing.");
    }

    public event Action<Incident>? CollectRequested;
    public event Action<Incident>? CompareRequested;
    public event Action<string>? SearchRequested;

    public void ShowIncident(Incident incident, bool retained, int relatedCount)
    {
        _incident = incident;
        var color = UiTheme.KindColor(incident.Kind);
        _accent.BackColor = color;
        _eyebrow.ForeColor = color;
        _eyebrow.Text = UiTheme.KindLabel(incident.Kind).ToUpperInvariant() + (retained ? "  •  SAVED EVIDENCE" : "  •  LIVE TIMELINE");
        _title.Text = incident.Title;
        // Code and plain English travel together: the number is what you search
        // for, the sentence is what you understand. Never one without the other.
        _code.Text = CodeDecoder.BugCheckHeadline(incident.BugCheckCode);
        _code.Visible = !string.IsNullOrWhiteSpace(incident.BugCheckCode);
        _meaning.Text = CodeDecoder.DescribeIncident(incident);
        var reboot = incident.RebootTime?.ToLocalTime().ToString("ddd d MMM yyyy, HH:mm:ss") ?? "Not identified";
        _metadata.Text = $"Incident time   {incident.Timestamp.ToLocalTime():ddd d MMM yyyy, HH:mm:ss}\r\n" +
                         $"Time basis      {incident.TimestampBasis}\r\n" +
                         $"Next boot       {reboot}\r\n" +
                         $"Source          {incident.Source}";
        _related.Text = relatedCount == 0
            ? "No other matching incident is visible in this range"
            : $"{relatedCount} related incident{(relatedCount == 1 ? string.Empty : "s")} share this code or incident type";
        _search.Enabled = !string.IsNullOrWhiteSpace(SearchText(incident));
        _collect.Enabled = true;
        _compare.Enabled = true;
        _copy.Enabled = true;
    }

    public void ShowEmpty(string message)
    {
        _incident = null;
        _accent.BackColor = UiTheme.Border;
        _eyebrow.Text = "INVESTIGATION BRIEFING";
        _title.Text = "No incident selected";
        _code.Visible = false;
        _meaning.Text = message;
        _metadata.Text = "Choose a time range or search the timeline to find a crash, shutdown, hardware event, or application failure.";
        _related.Text = string.Empty;
        _collect.Enabled = _compare.Enabled = _search.Enabled = _copy.Enabled = false;
    }

    private void CopyCurrent()
    {
        if (_incident is null) return;
        try
        {
            Clipboard.SetText($"{_incident.Title}\r\n{UiTheme.KindLabel(_incident.Kind)} at {_incident.Timestamp.ToLocalTime():F}\r\n" +
                              $"Code: {CodeDecoder.BugCheckHeadline(_incident.BugCheckCode)}\r\nMeaning: {CodeDecoder.DescribeIncident(_incident)}\r\n" +
                              $"Time basis: {_incident.TimestampBasis}\r\nSource: {_incident.Source}");
        }
        catch { /* Clipboard contention should never break navigation. */ }
    }

    private static string SearchText(Incident incident)
        => !string.IsNullOrWhiteSpace(incident.BugCheckCode)
            ? $"Windows {CodeDecoder.GetBugCheckLabel(incident.BugCheckCode)}"
            : $"Windows {incident.Title} {incident.Source}";
}
