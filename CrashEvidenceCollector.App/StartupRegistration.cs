using Microsoft.Win32;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Manages the app's own start-with-Windows entry. This is the single registry
/// value the application ever writes — one HKCU Run value pointing at its own
/// executable, created only when the user ticks the setting and deleted when
/// they untick it. System diagnostics remain strictly read-only.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CrashEvidenceCollector";

    public static bool IsEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath); return string.Equals(key?.GetValue(ValueName) as string, Command(), StringComparison.OrdinalIgnoreCase) || key?.GetValue(ValueName) is string; }
        catch { return false; }
    }

    /// <summary>Returns null on success, otherwise the error to show the user.</summary>
    public static string? SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled) key.SetValue(ValueName, Command());
            else if (key.GetValue(ValueName) is not null) key.DeleteValue(ValueName);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    private static string Command() => $"\"{Application.ExecutablePath}\"";
}
