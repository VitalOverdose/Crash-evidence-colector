using System.Drawing.Drawing2D;
using ProfessorSnowsVideoDownloader.CustomControls;

namespace ProfessorSnowsVideoDownloader.CustomControls;

internal static class RoundedDropdownButtonVirtualRenderer
{
    private const int ArrowGraphicWidth = 10;
    private const int ArrowGraphicHeight = 6;
    private const int ArrowEdgeMargin = 12;
    private const int ReservedArrowWidth = 18;
    private const int ImageTextGap = 6;

    /// <param name="button">
    /// Typed to RoundedComboBox, not to the virtual dropdown button, so the plain combo can share
    /// it. RoundedDropdownButton IS a RoundedComboBox — it only changes two default property
    /// values — and every property read below lives on the base, so one renderer covers both.
    /// </param>
    public static void Draw(Graphics graphics, Rectangle bounds,
        RoundedComboBox button, VirtualControlState state)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;

        bool hot = button.Enabled && button.HoverPropertiesEnabled
            && (state & VirtualControlState.Hot) != 0;
        bool pressed = button.Enabled && (state & VirtualControlState.Pressed) != 0;
        Color inner = !button.Enabled ? button.DisabledInnerColor
            : button.IsSelected ? button.SelectedInnerColor
            : hot ? button.HoverInnerColor : button.InnerColor;
        Color border = !button.Enabled ? button.DisabledBorderColor
            : button.IsSelected ? button.SelectedBorderColor
            : hot ? button.HoverBorderColor : button.BorderColor;
        int thickness = button.IsSelected && button.Enabled
            ? button.SelectedBorderThickness
            : hot ? button.HoverBorderThickness : button.BorderThickness;

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        Rectangle face = new(bounds.X, bounds.Y,
            Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
        using GraphicsPath path = RoundedRectangle(face, button.CornerRadius);
        using (Brush brush = CreateFaceBrush(face, inner, button))
            graphics.FillPath(brush, path);
        if (thickness > 0)
        {
            // Half-pen inset, matching RoundedComboBox: stroking the fill's own path leaves a
            // pale ring between the two anti-aliased edges. Renderer parity - a paint change to
            // a house control visits its virtual renderer in the same commit.
            float inset = thickness / 2f;
            RectangleF strokeBounds = RectangleF.Inflate(face, -inset, -inset);
            float strokeRadius = Math.Max(0f, Math.Max(1, button.CornerRadius) - inset);

            using GraphicsPath borderPath = RoundedRectangleF(strokeBounds, strokeRadius);
            using var pen = new Pen(border, thickness);
            graphics.DrawPath(pen, borderPath);
        }

        // Mirrors the real control: whichever end the arrow is on is the end the text gives way
        // at. This renderer draws its own arrow, so it has to honour ArrowSide itself or a
        // virtualised menu title would paint arrow-right while the control says left.
        bool arrowLeft = button.ArrowSide == RoundedComboBoxSide.Left;

        int textLeftEdge = face.Left + button.BorderThickness + button.TextPaddingHorizontal
                           + (arrowLeft ? ReservedArrowWidth : 0);
        int textRightEdge = face.Right - button.BorderThickness - button.TextPaddingHorizontal
                            - (arrowLeft ? 0 : ReservedArrowWidth);

        Rectangle textBounds = Rectangle.FromLTRB(
            textLeftEdge,
            face.Top + button.BorderThickness,
            Math.Max(textLeftEdge, textRightEdge),
            face.Bottom - button.BorderThickness);

        Image? image = ResolveImage(button, hot, pressed);
        if (image != null)
        {
            Size baseSize = button.ButtonImageSize.IsEmpty ? image.Size : button.ButtonImageSize;
            // Center-grow on hover, unconditionally — matching the REAL control. The old
            // fit-to-text-slot gate meant icon-only dropdowns (narrow slot, wide image)
            // never grew at all.
            int grow = hot && button.Enabled ? Math.Max(0, button.ButtonImageHoverGrow) : 0;
            Size drawSize = new(baseSize.Width + grow, baseSize.Height + grow);
            Rectangle slot = new(textBounds.Left,
                textBounds.Top + (textBounds.Height - baseSize.Height) / 2,
                baseSize.Width, baseSize.Height);
            var imageBounds = new Rectangle(
                slot.Left + (slot.Width - drawSize.Width) / 2,
                slot.Top + (slot.Height - drawSize.Height) / 2,
                drawSize.Width, drawSize.Height);
            InterpolationMode oldMode = graphics.InterpolationMode;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, imageBounds);
            graphics.InterpolationMode = oldMode;
            int textLeft = Math.Min(slot.Right + ImageTextGap, textBounds.Right);
            textBounds = Rectangle.FromLTRB(textLeft, textBounds.Top,
                textBounds.Right, textBounds.Bottom);
        }

        Font font = hot ? button.FontHovered : button.Font;
        Color textColor = button.Enabled ? button.ForeColor : button.DisabledTextColor;
        TextRenderer.DrawText(graphics, button.Text, font, textBounds, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            | TextFormatFlags.EndEllipsis);

        int arrowCenterX = arrowLeft
            ? face.Left + ArrowEdgeMargin
            : face.Right - ArrowEdgeMargin;
        int arrowCenterY = face.Top + face.Height / 2;
        Point[] arrow =
        {
            new(arrowCenterX - ArrowGraphicWidth / 2,
                arrowCenterY - ArrowGraphicHeight / 2),
            new(arrowCenterX + ArrowGraphicWidth / 2,
                arrowCenterY - ArrowGraphicHeight / 2),
            new(arrowCenterX, arrowCenterY + ArrowGraphicHeight / 2),
        };
        using (var arrowBrush = new SolidBrush(textColor))
            graphics.FillPolygon(arrowBrush, arrow);

        if (button.SplitButtonMode)
        {
            int dividerX = arrowLeft
                ? face.Left + button.ArrowZoneWidth
                : face.Right - button.ArrowZoneWidth;
            using var divider = new Pen(Color.FromArgb(195, 195, 195));
            graphics.DrawLine(divider, dividerX, face.Top + 5,
                dividerX, face.Bottom - 5);
        }
    }

    private static Image? ResolveImage(RoundedComboBox button,
        bool hot, bool pressed)
    {
        if (button.ImageBUse == IconButtonImageBUse.Toggle && button.Toggled)
        {
            Image? normal = button.ButtonImageB ?? button.ButtonImage;
            return pressed ? button.ButtonImageBPressed ?? button.ButtonImageBHover ?? normal
                : hot ? button.ButtonImageBHover ?? normal : normal;
        }

        return pressed ? button.ButtonImagePressed ?? button.ButtonImageHover ?? button.ButtonImage
            : hot ? button.ButtonImageHover ?? button.ButtonImage : button.ButtonImage;
    }

    private static Brush CreateFaceBrush(Rectangle bounds, Color inner,
        RoundedComboBox button)
    {
        if (!button.UseInnerGradient)
            return new SolidBrush(inner);

        float angle = button.InnerGradientDirection switch
        {
            RoundedGradientDirection.Left => 0f,
            RoundedGradientDirection.Up => 270f,
            RoundedGradientDirection.Right => 180f,
            _ => 90f,
        };
        return new LinearGradientBrush(Rectangle.Inflate(bounds, 1, 1),
            inner, button.InnerGradientEndColor, angle);
    }

    private static GraphicsPath RoundedRectangleF(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return path;

        float diameter = Math.Min(Math.Max(1f, radius * 2f), Math.Min(bounds.Width, bounds.Height));

        path.StartFigure();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int safeRadius = Math.Min(Math.Max(1, radius),
            Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2));
        int diameter = safeRadius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
