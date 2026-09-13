using CrashEvidenceCollector.Theming;

namespace CrashEvidenceCollector.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        // The theme is chosen before any window exists, so every view is built in it.
        ThemeManager.SetTheme(Theme.Dark);
#pragma warning disable WFO5001 // Colour mode is marked experimental in some .NET releases.
        // Standard Windows controls (list headers, scrollbars, date pickers) follow the theme too.
        Application.SetColorMode(ThemeManager.Current.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
#pragma warning restore WFO5001
        Application.Run(new MainForm());
    }    
}
