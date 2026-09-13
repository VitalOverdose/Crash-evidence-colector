using CrashEvidenceCollector.App;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// Report-host overlay shown while evidence is being gathered and rendered. The
/// spinner remains allocated but does no timer work while this overlay is hidden.
/// </summary>
internal sealed class ReportLoadingView : UserControl
{
    private readonly SpinnerBox _spinner = new()
    {
        Look = SpinnerLook.Segments,
        AccentColor = UiTheme.Accent,
        SoftColor = UiTheme.Border,
        Size = new Size(52, 52),
        Spinning = false,
        Anchor = AnchorStyles.None
    };
    private readonly Label _title = new()
    {
        Text = "Building crash evidence report",
        AutoSize = true,
        Anchor = AnchorStyles.None,
        Font = new Font("Segoe UI Semibold", 13f),
        ForeColor = UiTheme.Ink,
        TextAlign = ContentAlignment.MiddleCenter
    };
    private readonly Label _detail = new()
    {
        Text = "Preparing collection…",
        AutoSize = true,
        MaximumSize = new Size(520, 0),
        Anchor = AnchorStyles.None,
        Font = new Font("Segoe UI", 11f),
        ForeColor = UiTheme.Muted,
        TextAlign = ContentAlignment.MiddleCenter
    };

    public ReportLoadingView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Visible = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = BackColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        layout.Controls.Add(_spinner, 0, 1);
        layout.Controls.Add(_title, 0, 2);
        layout.Controls.Add(_detail, 0, 3);
        Controls.Add(layout);
    }

    public void ShowProgress(string message)
    {
        _detail.Text = string.IsNullOrWhiteSpace(message) ? "Preparing collection…" : message;
        Visible = true;
        BringToFront();
        _spinner.Spinning = true;
    }

    public void HideProgress()
    {
        _spinner.Spinning = false;
        Visible = false;
    }
}
