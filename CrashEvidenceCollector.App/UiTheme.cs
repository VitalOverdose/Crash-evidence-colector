namespace CrashEvidenceCollector.App;

/// <summary>
/// Shared visual language for the investigation workspace.  Keeping these values
/// together prevents each evidence pane from slowly becoming its own mini-app.
/// </summary>
internal static class UiTheme
{
    public static readonly Color Canvas = Color.FromArgb(242, 245, 249);
    public static readonly Color Surface = Color.FromArgb(255, 255, 255);
    public static readonly Color SurfaceRaised = Color.FromArgb(249, 251, 253);
    public static readonly Color Border = Color.FromArgb(218, 225, 234);
    public static readonly Color Ink = Color.FromArgb(22, 33, 48);
    public static readonly Color Muted = Color.FromArgb(91, 104, 122);
    public static readonly Color Faint = Color.FromArgb(133, 145, 160);
    public static readonly Color Nav = Color.FromArgb(13, 24, 39);
    public static readonly Color NavHover = Color.FromArgb(24, 42, 64);
    public static readonly Color Accent = Color.FromArgb(37, 112, 202);
    public static readonly Color AccentSoft = Color.FromArgb(230, 240, 252);
    public static readonly Color Cyan = Color.FromArgb(20, 139, 154);
    public static readonly Color Warning = Color.FromArgb(190, 116, 25);
    public static readonly Color Danger = Color.FromArgb(184, 56, 62);
    public static readonly Color Success = Color.FromArgb(43, 133, 89);

    public static Color KindColor(Core.IncidentKind kind) => kind switch
    {
        Core.IncidentKind.BugCheck => Danger,
        Core.IncidentKind.HardwareError => Color.FromArgb(153, 73, 180),
        Core.IncidentKind.PowerLossOrFreeze => Warning,
        Core.IncidentKind.UnexpectedShutdown => Color.FromArgb(205, 137, 38),
        Core.IncidentKind.ApplicationCrash => Accent,
        _ => Muted
    };

    public static Color KindWash(Core.IncidentKind kind) => kind switch
    {
        Core.IncidentKind.BugCheck => Color.FromArgb(253, 241, 242),
        Core.IncidentKind.HardwareError => Color.FromArgb(249, 242, 252),
        Core.IncidentKind.PowerLossOrFreeze or Core.IncidentKind.UnexpectedShutdown => Color.FromArgb(255, 248, 235),
        Core.IncidentKind.ApplicationCrash => Color.FromArgb(240, 247, 254),
        _ => Surface
    };

    public static string KindLabel(Core.IncidentKind kind) => kind switch
    {
        Core.IncidentKind.BugCheck => "BSOD / bugcheck",
        Core.IncidentKind.HardwareError => "Hardware / WHEA",
        Core.IncidentKind.PowerLossOrFreeze => "Power loss / freeze",
        Core.IncidentKind.UnexpectedShutdown => "Unexpected shutdown",
        Core.IncidentKind.ApplicationCrash => "Application crash",
        _ => kind.ToString()
    };

    public static Button ActionButton(string text, bool primary = false) => new()
    {
        Text = text,
        AutoSize = true,
        MinimumSize = new Size(0, 34),
        Padding = new Padding(12, 3, 12, 3),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Accent : Surface,
        ForeColor = primary ? Color.White : Ink,
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 8, 0),
        FlatAppearance = { BorderColor = primary ? Accent : Border, BorderSize = 1 }
    };
}
