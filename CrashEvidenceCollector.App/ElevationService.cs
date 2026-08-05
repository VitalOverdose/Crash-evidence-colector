using System.Diagnostics;
using System.Security.Principal;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Optional whole-app elevation. The application is manifested asInvoker and
/// remains unelevated by default: protected dumps are normally copied by the
/// separate single-purpose helper. Users who tire of approving that prompt per
/// collection can relaunch the whole app elevated instead — one prompt, at a
/// moment they chose, after which dump copying needs no further consent.
///
/// This is deliberately not automatic and deliberately not silent. Promptless
/// elevation requires a scheduled-task/service trick, which changes the system
/// and defeats UAC; a read-only forensic tool must never do that.
/// </summary>
internal static class ElevationService
{
    public static bool IsElevated { get; } = ReadElevation();

    private static bool ReadElevation()
    {
        try { using var identity = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
        catch { return false; }
    }

    /// <summary>
    /// Relaunches this executable with a UAC consent request. Returns null when the
    /// new instance started (the caller should exit), otherwise the reason it did not.
    /// </summary>
    public static string? Relaunch(string? arguments = null)
    {
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            if (!string.IsNullOrWhiteSpace(arguments)) info.Arguments = arguments;
            return Process.Start(info) is null ? "The elevated instance did not start." : null;
        }
        // 1223 is ERROR_CANCELLED: the user dismissed the consent dialog.
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return "The administrator prompt was declined."; }
        catch (Exception ex) { return ex.Message; }
    }
}
