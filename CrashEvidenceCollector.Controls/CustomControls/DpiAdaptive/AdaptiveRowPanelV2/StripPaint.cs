using System.Drawing.Drawing2D;

namespace ProfessorSnowsVideoDownloader.CustomControls;

internal static class StripPaint
{
    public static void DrawButtonFace(Graphics graphics, Rectangle bounds, StripItemState state, StripStyle style)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = RoundedRectangle(bounds, style.Scale(style.CornerRadius));
        Color fill = (state & StripItemState.Pressed) != 0
            ? style.PressedBackColor
            : (state & StripItemState.Hot) != 0 ? style.HotBackColor : Color.Transparent;
        if (fill.A > 0)
        {
            using var brush = new SolidBrush(fill);
            graphics.FillPath(brush, path);
        }
        using var pen = new Pen(style.BorderColor);
        graphics.DrawPath(pen, path);
    }

    public static void DrawCenteredText(Graphics graphics, string text, Rectangle bounds, StripStyle style, bool enabled)
    {
        TextRenderer.DrawText(graphics, text ?? string.Empty, style.Font, bounds,
            enabled ? style.ForeColor : style.DisabledForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    public static void DrawFocusRect(Graphics graphics, Rectangle bounds)
    {
        Rectangle focus = Rectangle.Inflate(bounds, -2, -2);
        if (focus.Width > 0 && focus.Height > 0)
            ControlPaint.DrawFocusRectangle(graphics, focus);
    }

    public static void DrawDottedArrow(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width < 8 || bounds.Height < 6) return;
        int y = bounds.Top + bounds.Height / 2;
        int left = bounds.Left + 3;
        int right = bounds.Right - 4;
        int arrow = Math.Min(5, Math.Max(2, bounds.Height / 5));
        using var linePen = new Pen(Color.FromArgb(70, 130, 180)) { DashStyle = DashStyle.Dot };
        using var arrowPen = new Pen(Color.FromArgb(55, 95, 135));
        graphics.DrawLine(linePen, left + arrow, y, right - arrow, y);
        graphics.DrawLine(arrowPen, left, y, left + arrow, y - arrow);
        graphics.DrawLine(arrowPen, left, y, left + arrow, y + arrow);
        graphics.DrawLine(arrowPen, right, y, right - arrow, y - arrow);
        graphics.DrawLine(arrowPen, right, y, right - arrow, y + arrow);
    }

    public static void DrawToggle(Graphics graphics, Rectangle bounds, bool isChecked, string text,
        StripItemState state, StripStyle style, bool enabled)
    {
        DrawButtonFace(graphics, bounds, state, style);
        int inset = style.Scale(6);
        int switchWidth = Math.Min(style.Scale(34), Math.Max(style.Scale(18), bounds.Width / 3));
        int switchHeight = Math.Min(style.Scale(18), Math.Max(style.Scale(12), bounds.Height - inset * 2));
        var switchBounds = new Rectangle(bounds.Right - switchWidth - inset,
            bounds.Top + (bounds.Height - switchHeight) / 2, switchWidth, switchHeight);
        var textBounds = new Rectangle(bounds.Left + inset, bounds.Top,
            Math.Max(0, switchBounds.Left - bounds.Left - inset * 2), bounds.Height);

        TextRenderer.DrawText(graphics, text ?? string.Empty, style.Font, textBounds,
            enabled ? style.ForeColor : style.DisabledForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath track = RoundedRectangle(switchBounds, switchHeight / 2);
        using var trackBrush = new SolidBrush(isChecked ? Color.FromArgb(64, 145, 220) : Color.FromArgb(180, 180, 180));
        graphics.FillPath(trackBrush, track);
        int knobSize = Math.Max(4, switchHeight - 4);
        int knobX = isChecked ? switchBounds.Right - knobSize - 2 : switchBounds.Left + 2;
        using var knobBrush = new SolidBrush(Color.White);
        graphics.FillEllipse(knobBrush, knobX, switchBounds.Top + 2, knobSize, knobSize);
    }

    public static void DrawDropDownShell(Graphics graphics, Rectangle bounds, string text,
        StripItemState state, StripStyle style, bool enabled)
    {
        DrawButtonFace(graphics, bounds, state, style);
        int arrowAreaWidth = style.Scale(22);
        var textBounds = new Rectangle(bounds.Left + style.Scale(6), bounds.Top,
            Math.Max(0, bounds.Width - arrowAreaWidth - style.Scale(8)), bounds.Height);
        TextRenderer.DrawText(graphics, text ?? string.Empty, style.Font, textBounds,
            enabled ? style.ForeColor : style.DisabledForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        int centerX = bounds.Right - arrowAreaWidth / 2;
        int centerY = bounds.Top + bounds.Height / 2;
        int half = Math.Max(2, style.Scale(3));
        Point[] arrow = [new(centerX - half, centerY - 1), new(centerX + half, centerY - 1), new(centerX, centerY + half)];
        using var brush = new SolidBrush(enabled ? style.ForeColor : style.DisabledForeColor);
        graphics.FillPolygon(brush, arrow);
    }

    private static GraphicsPath RoundedRectangle(Rectangle source, int requestedRadius)
    {
        var bounds = new Rectangle(source.X, source.Y, Math.Max(1, source.Width - 1), Math.Max(1, source.Height - 1));
        int radius = Math.Min(Math.Max(0, requestedRadius), Math.Min(bounds.Width, bounds.Height) / 2);
        var path = new GraphicsPath();
        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        int diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
