using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CrashEvidenceCollector.Theming;

/// <summary>
/// A flat button that paints itself.
///
/// Under Windows dark colour mode, standard WinForms buttons are redrawn with their
/// own rounded light border that ignores <see cref="FlatButtonAppearance.BorderSize"/>
/// and <see cref="FlatButtonAppearance.BorderColor"/>. Owning the paint keeps the fill,
/// border and text exactly as the theme sets them — including no border at all.
/// Behaviour (clicks, AutoSize, keyboard) is unchanged: only drawing is replaced.
/// </summary>
public class ThemedFlatButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public ThemedFlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        FlatButtonAppearance flat = FlatAppearance;
        Theme theme = ThemeManager.Current;

        Color fill = BackColor;
        if (Enabled && _pressed && !flat.MouseDownBackColor.IsEmpty) fill = flat.MouseDownBackColor;
        else if (Enabled && _hovered && !flat.MouseOverBackColor.IsEmpty) fill = flat.MouseOverBackColor;

        using (var brush = new SolidBrush(fill))
            g.FillRectangle(brush, ClientRectangle);

        if (flat.BorderSize > 0 && Width > 1 && Height > 1)
        {
            Color border = flat.BorderColor.IsEmpty ? theme.Border : flat.BorderColor;
            using var pen = new Pen(border, flat.BorderSize) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // Keyboard focus gets a quiet accent line inside the edge, never a dotted box.
        if (Focused && ShowFocusCues && Width > 6 && Height > 6)
        {
            using var focus = new Pen(theme.Accent);
            g.DrawRectangle(focus, 2, 2, Width - 5, Height - 5);
        }

        Rectangle textBounds = new(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
        Color textColor = Enabled ? ForeColor : theme.TextFaint;
        TextRenderer.DrawText(g, Text, Font, textBounds, textColor,
            AlignmentFlags(TextAlign) | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    private static TextFormatFlags AlignmentFlags(ContentAlignment alignment)
    {
        TextFormatFlags horizontal = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter
        };
        TextFormatFlags vertical = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => TextFormatFlags.Top,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
            _ => TextFormatFlags.VerticalCenter
        };
        return horizontal | vertical;
    }
}
