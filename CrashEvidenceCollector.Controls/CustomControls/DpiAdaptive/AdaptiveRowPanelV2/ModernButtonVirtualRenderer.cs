using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ProfessorSnowsVideoDownloader.CustomControls;

namespace ProfessorSnowsVideoDownloader.CustomControls;

internal static class ModernButtonVirtualRenderer
{
    private const int HorizontalPadding = 8;
    private const int ImageTextSpacing = 4;

    public static void Draw(Graphics graphics, Rectangle bounds,
        ModernButton button, VirtualControlState state)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool hot = button.Enabled && (state & VirtualControlState.Hot) != 0;
        bool pressed = button.Enabled && (state & VirtualControlState.Pressed) != 0;
        int borderSize = pressed ? button.PressedBorderSize
            : hot ? button.HoverBorderSize : button.BorderSize;
        Color border = pressed ? button.PressedBorderColor
            : hot ? button.HoverBorderColor : button.BorderColor;
        Color face = pressed && button.PressedColorFx ? button.PressedColor
            : hot && button.HoverColorFx ? button.HoverColor : button.BackgroundColor;

        int fade = button.Enabled ? 0 : button.DisabledFadePercent;
        Color parentBack = button.Parent?.BackColor ?? SystemColors.Control;
        face = FadeToward(face, parentBack, fade);
        border = FadeToward(border, parentBack, fade);
        Color textColor = FadeToward(button.TextColor, parentBack, fade);

        if (button.BackColor.A == 255)
        {
            using var backdrop = new SolidBrush(button.BackColor);
            graphics.FillRectangle(backdrop, bounds);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        Rectangle surface = new(bounds.X, bounds.Y,
            Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
        int radius = Math.Min(button.BorderRadius,
            Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2));
        int inset = Math.Min(Math.Max(0, borderSize),
            Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2));
        Rectangle borderBounds = Rectangle.Inflate(surface, -inset, -inset);

        using (var faceBrush = new SolidBrush(face))
        {
            if (radius > 2)
            {
                using GraphicsPath facePath = RoundedRectangle(surface, radius);
                graphics.FillPath(faceBrush, facePath);
                if (borderSize >= 1 && borderBounds.Width > 0 && borderBounds.Height > 0)
                {
                    using GraphicsPath borderPath = RoundedRectangle(borderBounds,
                        Math.Max(1, radius - 1));
                    using var pen = new Pen(border, borderSize);
                    graphics.DrawPath(pen, borderPath);
                }
            }
            else
            {
                graphics.FillRectangle(faceBrush, surface);
                if (borderSize >= 1)
                {
                    using var pen = new Pen(border, borderSize) { Alignment = PenAlignment.Inset };
                    graphics.DrawRectangle(pen, surface);
                }
            }
        }

        Image? image = pressed && button.ButtonImagePressed != null
            ? button.ButtonImagePressed
            : button.Toggled && button.ButtonImageToggled != null
            ? button.ButtonImageToggled
            : hot && button.ButtonImageHovered != null
                ? button.ButtonImageHovered
                : button.ButtonImage;
        Font font = hot && button.FontHovered != null ? button.FontHovered : button.Font;

        if (button.IconOnlyMode && (image != null || !string.IsNullOrWhiteSpace(button.TextIcon)))
        {
            DrawIconOnly(graphics, surface, button, image, font, textColor, hot, pressed, fade);
            return;
        }

        Rectangle textBounds = surface;
        if (image != null)
        {
            Rectangle imageBounds = GetImageBounds(surface, button.ImageSize, button.ImageAlign);
            imageBounds = GrowImageIfPossible(imageBounds, surface,
                button.HoverGrowsImage && hot && !pressed);
            DrawImage(graphics, image, imageBounds, fade);

            if (IsLeft(button.ImageAlign))
            {
                textBounds = new Rectangle(imageBounds.Right + ImageTextSpacing, surface.Y,
                    Math.Max(0, surface.Right - imageBounds.Right
                        - HorizontalPadding - ImageTextSpacing), surface.Height);
            }
            else if (IsRight(button.ImageAlign))
            {
                textBounds = new Rectangle(surface.X, surface.Y,
                    Math.Max(0, imageBounds.Left - surface.Left - ImageTextSpacing), surface.Height);
            }
        }

        textBounds.Offset(button.TextXOffset, 0);
        if (!string.IsNullOrEmpty(button.TextLine2))
        {
            int half = textBounds.Height / 2;
            var first = new Rectangle(textBounds.X, textBounds.Y + 2, textBounds.Width, half);
            var second = new Rectangle(textBounds.X, textBounds.Y + half,
                textBounds.Width, textBounds.Height - half);
            TextRenderer.DrawText(graphics, button.Text, font, first, textColor,
                ToTextFlags(button.TextAlign, false));
            TextRenderer.DrawText(graphics, button.TextLine2, font, second, textColor,
                ToTextFlags(button.TextLine2Align, false));
        }
        else
        {
            TextRenderer.DrawText(graphics, button.Text, font, textBounds, textColor,
                ToTextFlags(button.TextAlign, button.Multiline));
        }
    }

    private static void DrawIconOnly(Graphics graphics, Rectangle bounds,
        ModernButton button, Image? image, Font font, Color textColor,
        bool hot, bool pressed, int fade)
    {
        if (image != null)
        {
            int width = Math.Min(button.ImageSize.Width, Math.Max(0, bounds.Width - 8));
            int height = Math.Min(button.ImageSize.Height, Math.Max(0, bounds.Height - 8));
            var destination = new Rectangle(bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2, width, height);
            destination = GrowImageIfPossible(destination, bounds,
                button.HoverGrowsImage && hot && !pressed);
            DrawImage(graphics, image, destination, fade);
            return;
        }

        TextRenderer.DrawText(graphics, button.TextIcon, font, bounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding
            | TextFormatFlags.EndEllipsis);
    }

    private static Rectangle GetImageBounds(Rectangle bounds, Size size,
        ContentAlignment alignment)
    {
        int x = IsRight(alignment) ? bounds.Right - size.Width - HorizontalPadding
            : IsCenter(alignment) ? bounds.X + (bounds.Width - size.Width) / 2
            : bounds.X + HorizontalPadding;
        int y = IsBottom(alignment) ? bounds.Bottom - size.Height - 5
            : IsMiddle(alignment) ? bounds.Y + (bounds.Height - size.Height) / 2
            : bounds.Y + 5;
        return new Rectangle(x, y, size.Width, size.Height);
    }

    private static Rectangle GrowImageIfPossible(Rectangle image, Rectangle bounds, bool grow)
    {
        if (!grow) return image;
        Rectangle result = image;
        result.Inflate(1, 1);
        return bounds.Contains(result) ? result : image;
    }

    private static void DrawImage(Graphics graphics, Image image,
        Rectangle destination, int fade)
    {
        if (fade <= 0)
        {
            graphics.DrawImage(image, destination);
            return;
        }

        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix
        {
            Matrix00 = 1f,
            Matrix11 = 1f,
            Matrix22 = 1f,
            Matrix33 = 1f - Math.Clamp(fade / 100f, 0f, 1f),
            Matrix44 = 1f,
        };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(image, destination, 0, 0, image.Width, image.Height,
            GraphicsUnit.Pixel, attributes);
    }

    private static Color FadeToward(Color source, Color background, int fadePercent)
    {
        float amount = Math.Clamp(fadePercent / 100f, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(source.A + (background.A - source.A) * amount),
            (int)Math.Round(source.R + (background.R - source.R) * amount),
            (int)Math.Round(source.G + (background.G - source.G) * amount),
            (int)Math.Round(source.B + (background.B - source.B) * amount));
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(Math.Max(1f, radius * 2f),
            Math.Min(bounds.Width, bounds.Height));
        path.StartFigure();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static TextFormatFlags ToTextFlags(ContentAlignment alignment, bool multiline)
    {
        TextFormatFlags flags = alignment switch
        {
            ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
            ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
            ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
            ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter | TextFormatFlags.Left,
            ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
            ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
            ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
            ContentAlignment.BottomRight => TextFormatFlags.Bottom | TextFormatFlags.Right,
            _ => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
        };
        return multiline ? flags | TextFormatFlags.WordBreak : flags;
    }

    private static bool IsLeft(ContentAlignment value) =>
        value is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft;
    private static bool IsRight(ContentAlignment value) =>
        value is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight;
    private static bool IsCenter(ContentAlignment value) =>
        value is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter;
    private static bool IsBottom(ContentAlignment value) =>
        value is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight;
    private static bool IsMiddle(ContentAlignment value) =>
        value is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight;
}
