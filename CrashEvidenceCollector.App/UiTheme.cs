using CrashEvidenceCollector.Theming;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Shared visual language for the investigation workspace, expressed as roles read
/// from the current <see cref="Theme"/>. Views ask for "Surface" or "Danger"; which
/// colour that is depends on the active theme, not on this file.
/// </summary>
internal static class UiTheme
{
    private static Theme T => ThemeManager.Current;

    public static Color Canvas => T.Canvas;
    public static Color Surface => T.Surface;
    public static Color SurfaceRaised => T.SurfaceRaised;
    public static Color Input => T.Input;
    public static Color Border => T.Border;
    public static Color BorderStrong => T.BorderStrong;
    public static Color Ink => T.Text;
    public static Color InkStrong => T.TextStrong;
    public static Color Muted => T.TextMuted;
    public static Color Faint => T.TextFaint;
    public static Color Nav => T.Nav;
    public static Color NavHover => T.NavActive;
    public static Color NavText => T.NavText;
    public static Color NavTextActive => T.NavTextActive;
    public static Color Accent => T.Accent;
    public static Color AccentStrong => T.AccentStrong;
    public static Color AccentSoft => T.AccentSoft;
    public static Color OnAccent => T.OnAccent;
    public static Color Cyan => T.Info;
    public static Color Warning => T.Warning;
    public static Color Danger => T.Danger;
    public static Color Success => T.Success;
    public static Color Hardware => T.Hardware;

    /// <summary>A meaning colour laid thinly over the surface.</summary>
    public static Color Wash(Color meaning) => T.Wash(meaning);

    public static Color KindColor(Core.IncidentKind kind) => kind switch
    {
        Core.IncidentKind.BugCheck => Danger,
        Core.IncidentKind.HardwareError => Hardware,
        Core.IncidentKind.PowerLossOrFreeze => Warning,
        Core.IncidentKind.UnexpectedShutdown => T.Caution,
        Core.IncidentKind.ApplicationCrash => Accent,
        _ => Muted
    };

    public static Color KindWash(Core.IncidentKind kind) => kind switch
    {
        Core.IncidentKind.BugCheck or Core.IncidentKind.HardwareError or Core.IncidentKind.PowerLossOrFreeze
            or Core.IncidentKind.UnexpectedShutdown or Core.IncidentKind.ApplicationCrash => Wash(KindColor(kind)),
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

    public static Button ActionButton(string text, bool primary = false) => new ThemedFlatButton()
    {
        Text = text,
        AutoSize = true,
        MinimumSize = new Size(0, 34),
        Padding = new Padding(12, 3, 12, 3),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? AccentStrong : SurfaceRaised,
        ForeColor = primary ? OnAccent : Ink,
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 8, 0),
        FlatAppearance = { BorderColor = primary ? AccentStrong : Border, BorderSize = 1, MouseOverBackColor = primary ? Accent : Border }
    };
}
