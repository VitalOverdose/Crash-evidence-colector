using System.Drawing;
using System.Windows.Forms;

namespace CrashEvidenceCollector.Theming;

/// <summary>
/// Right-click menus and dropdowns in the current theme. Installed globally by
/// <see cref="ThemeManager.SetTheme"/>, so every ContextMenuStrip follows it
/// without being styled one by one.
/// </summary>
internal sealed class ThemedToolStripRenderer(Theme theme) : ToolStripProfessionalRenderer(new ThemeColorTable(theme))
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? theme.Text : theme.TextFaint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? theme.TextFaint : theme.TextMuted;
        base.OnRenderArrow(e);
    }

    private sealed class ThemeColorTable(Theme theme) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => theme.Surface;
        public override Color ImageMarginGradientBegin => theme.Surface;
        public override Color ImageMarginGradientMiddle => theme.Surface;
        public override Color ImageMarginGradientEnd => theme.Surface;
        public override Color MenuBorder => theme.BorderStrong;
        public override Color MenuItemBorder => theme.Border;
        public override Color MenuItemSelected => theme.SurfaceRaised;
        public override Color MenuItemSelectedGradientBegin => theme.SurfaceRaised;
        public override Color MenuItemSelectedGradientEnd => theme.SurfaceRaised;
        public override Color MenuItemPressedGradientBegin => theme.SurfaceRaised;
        public override Color MenuItemPressedGradientMiddle => theme.SurfaceRaised;
        public override Color MenuItemPressedGradientEnd => theme.SurfaceRaised;
        public override Color MenuStripGradientBegin => theme.Surface;
        public override Color MenuStripGradientEnd => theme.Surface;
        public override Color SeparatorDark => theme.Border;
        public override Color SeparatorLight => theme.Surface;
        public override Color ToolStripBorder => theme.Border;
        public override Color ToolStripGradientBegin => theme.Surface;
        public override Color ToolStripGradientMiddle => theme.Surface;
        public override Color ToolStripGradientEnd => theme.Surface;
        public override Color CheckBackground => theme.AccentSoft;
        public override Color CheckSelectedBackground => theme.AccentSoft;
        public override Color CheckPressedBackground => theme.AccentSoft;
        public override Color ButtonSelectedHighlight => theme.SurfaceRaised;
        public override Color ButtonSelectedBorder => theme.Border;
    }
}
