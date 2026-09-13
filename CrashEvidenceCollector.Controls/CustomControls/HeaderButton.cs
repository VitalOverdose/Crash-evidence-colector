using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// HeaderButton — same as ModernButton but with rounded top corners only.
    /// The bottom edge is flat with no border so it sits flush against a list
    /// or panel placed directly below it.
    /// Inherits all ModernButton appearance properties (colors, border, image etc).
    /// </summary>
    public class HeaderButton : ModernButton
    {
        private bool _hovered;
        private bool _pressed;

        public HeaderButton()
        {
            BorderRadius = 10;
            MouseEnter += (s, e) => { _hovered = true;  Invalidate(); };
            MouseLeave += (s, e) => { _hovered = false; _pressed = false; Invalidate(); };
            MouseDown  += (s, e) => { if (e.Button == MouseButtons.Left) { _pressed = true;  Invalidate(); } };
            MouseUp    += (s, e) => { _pressed = false; Invalidate(); };
        }

        // Rounded top corners, straight bottom edge — no arc on bottom-left / bottom-right.
        private static GraphicsPath GetHeaderPath(Rectangle rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);                       // top-left
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);               // top-right
            path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom); // right side
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);         // bottom (flat)
            path.CloseFigure();                                                  // left side
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color backColor = _pressed ? PressedColor
                            : _hovered ? HoverColor
                            : BackgroundColor;

            Color borderCol = _pressed ? PressedBorderColor
                            : _hovered ? HoverBorderColor
                            : BorderColor;

            int bSize = _pressed ? PressedBorderSize
                      : _hovered ? HoverBorderSize
                      : BorderSize;

            Color textCol = ForeColor;

            if (!Enabled)
            {
                Color bg = Parent?.BackColor ?? SystemColors.Control;
                float t = DisabledFadePercent / 100f;
                backColor = Blend(backColor, bg, t);
                borderCol = Blend(borderCol, bg, t);
                textCol   = Blend(textCol,   bg, t);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            float r  = Math.Min(BorderRadius, Height / 2f);

            using var pathOuter = GetHeaderPath(rect, r);
            Region = new Region(pathOuter);

            // Fill background
            using (var fill = new SolidBrush(backColor))
                g.FillPath(fill, pathOuter);


            // Border — top and sides only, no bottom line
            if (bSize >= 1)
            {
                var inset = Rectangle.Inflate(rect, -bSize, -bSize);
                float ri  = Math.Max(r - 1f, 0f);

                using var topSides = new GraphicsPath();
                topSides.AddArc(inset.X, inset.Y, ri * 2f, ri * 2f, 180, 90);              // top-left
                topSides.AddArc(inset.Right - ri * 2f, inset.Y, ri * 2f, ri * 2f, 270, 90); // top-right
                topSides.AddLine(inset.Right, inset.Y + ri, inset.Right, inset.Bottom);       // right side only
                topSides.StartFigure();
                topSides.AddLine(inset.X, inset.Y + ri, inset.X, inset.Bottom);                // left side only

                using var penBorder = new Pen(borderCol, bSize);
                g.DrawPath(penBorder, topSides);
            }

            // Text
            TextRenderer.DrawText(g, Text, Font, rect, textCol,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static Color Blend(Color src, Color dst, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(src.A * (1 - t) + dst.A * t),
                (int)(src.R * (1 - t) + dst.R * t),
                (int)(src.G * (1 - t) + dst.G * t),
                (int)(src.B * (1 - t) + dst.B * t));
        }
    }
}
