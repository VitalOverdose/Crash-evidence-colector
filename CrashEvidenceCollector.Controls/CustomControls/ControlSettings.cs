namespace ProfessorSnowsVideoDownloader.CustomControls;

/// <summary>
/// CEC-local stand-ins for the few app-wide settings the controls read. In
/// EliteBrowserShell these lived on the browser app's SettingsManager, which pulled
/// bookmarks, downloads and the SafeCopy suite into any consumer. Defaults match the
/// shell's, so the controls behave exactly as they did there.
/// </summary>
public static class ControlSettings
{
    /// <summary>Step child fonts down 2pt at 150%+ DPI. Shell default: true.</summary>
    public static bool StepDownHighDpiFonts { get; set; } = true;

    /// <summary>Show ModernButtons that opt in as icon only. Shell default: false.</summary>
    public static bool AppIconOnlyMode { get; private set; }

    /// <summary>Raised when <see cref="AppIconOnlyMode"/> changes, so buttons repaint.</summary>
    public static event Action? AppIconOnlyModeChanged;

    public static void SetAppIconOnlyMode(bool value)
    {
        if (AppIconOnlyMode == value) return;
        AppIconOnlyMode = value;
        AppIconOnlyModeChanged?.Invoke();
    }
}
