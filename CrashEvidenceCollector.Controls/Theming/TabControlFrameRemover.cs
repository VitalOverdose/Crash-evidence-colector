using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CrashEvidenceCollector.Theming;

/// <summary>
/// Removes the light 3D frame a <see cref="TabControl"/> draws around its pages.
///
/// A TabControl used only as a page switcher (hidden tabs, pages filling the window)
/// still insets its pages by a system-coloured frame, which shows as a pale line on
/// a dark theme. The control asks Windows for its page area through TCM_ADJUSTRECT;
/// widening that answer lets the pages cover the frame. Nothing is painted over.
/// </summary>
public sealed class TabControlFrameRemover : NativeWindow
{
    private const int TCM_ADJUSTRECT = 0x1328;
    private const int FrameWidth = 4;
    private readonly TabControl _tabs;

    private TabControlFrameRemover(TabControl tabs)
    {
        _tabs = tabs;
        tabs.HandleCreated += (_, _) => AssignHandle(tabs.Handle);
        tabs.HandleDestroyed += (_, _) => ReleaseHandle();
        if (tabs.IsHandleCreated) AssignHandle(tabs.Handle);
    }

    /// <summary>Attaches to <paramref name="tabs"/> for its lifetime, including handle re-creation.</summary>
    public static void Attach(TabControl tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        _ = new TabControlFrameRemover(tabs);
        if (tabs.IsHandleCreated) tabs.PerformLayout();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // wParam 0: "given the window rectangle, return the page area".
        if (m.Msg != TCM_ADJUSTRECT || m.WParam != IntPtr.Zero || m.LParam == IntPtr.Zero) return;
        RECT area = Marshal.PtrToStructure<RECT>(m.LParam);
        area.Left -= FrameWidth;
        area.Top -= FrameWidth;
        area.Right += FrameWidth;
        area.Bottom += FrameWidth;
        Marshal.StructureToPtr(area, m.LParam, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}
