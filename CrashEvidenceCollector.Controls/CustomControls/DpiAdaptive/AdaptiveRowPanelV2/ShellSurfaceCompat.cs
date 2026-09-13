using System.Drawing;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls;

// SHELL DIVERGENCE: the app resolves designer surface colours through the v1 contents
// editor's zone walk (top/bottom bars etc.). This shell's older v1 has no such helper,
// so the designer actions fall back to the nearest opaque ancestor colour.
internal static class ShellSurfaceCompat
{
    internal static Color? Resolve(Control target)
    {
        for (Control? p = target.Parent; p != null; p = p.Parent)
            if (p.BackColor.A == 255) return p.BackColor;
        return null;
    }
}
