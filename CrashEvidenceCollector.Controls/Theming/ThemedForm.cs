using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CrashEvidenceCollector.Theming;

/// <summary>
/// A form that wears the current theme: its controls are themed when it loads and
/// again whenever the theme changes, and its title bar follows light or dark.
/// Everything else lives in <see cref="ThemeManager"/>; this only connects a window to it.
/// </summary>
public class ThemedForm : Form
{
    private bool _subscribed;

    private static bool IsDesignTime => LicenseManager.UsageMode == LicenseUsageMode.Designtime;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBar();
    }

    protected override void OnLoad(EventArgs e)
    {
        // Themed before Load handlers run, so they see the final colours.
        if (!IsDesignTime && !DesignMode)
        {
            ThemeManager.Apply(this);
            if (!_subscribed)
            {
                ThemeManager.ThemeChanged += OnThemeChanged;
                _subscribed = true;
            }
        }
        base.OnLoad(e);
    }

    private void OnThemeChanged(Theme theme) => ApplyTitleBar();

    /// <summary>Asks Windows for a dark or light caption to match the theme.</summary>
    private void ApplyTitleBar()
    {
        if (!IsHandleCreated || IsDesignTime) return;
        int dark = ThemeManager.Current.IsDark ? 1 : 0;
        // Attribute 20 on current Windows builds; 19 on early Windows 10 builds.
        if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _subscribed)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _subscribed = false;
        }
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
