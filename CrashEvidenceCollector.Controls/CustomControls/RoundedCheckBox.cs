using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// Rounded checkbox that can optionally render its own text, so forms do not need to
    /// pair a separate Label next to every checkbox just to get aligned captions.
    /// </summary>
    public class RoundedCheckBox : Control, IAdaptiveRowPanelItem
    {
        private bool _checked = false;
        private int _radius = 18;
        private Color _borderColor = Color.FromArgb(170, 170, 170);
        private Color _checkedColor = Color.FromArgb(220, 255, 255);   // the house checked colour (David)
        private Color _tickColor = Color.FromArgb(110, 110, 110);       // theme glyph grey: 110 normal
        private Color _tickHoverColor = Color.FromArgb(60, 60, 60);      // 60 = the highlight tier (David)
        private Color _hoverBorderColor = Color.FromArgb(60, 60, 60);   // hover feedback lives in the BORDER; the fill never changes (David)
        private Color _backgroundColor = Color.White;
        private bool _isHovered = false;
        private HorizontalAlignment _checkBoxAlignment = HorizontalAlignment.Left;
        private int _textGap = 8;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public RoundedCheckBox()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Opaque, false);

            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(20, 20);
            Cursor = Cursors.Hand;
            ForeColor = Color.FromArgb(31, 31, 31);
            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Padding = new Padding(0);

            MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
            Click += (s, e) => { Checked = !Checked; };
        }

        // ============================================================
        // PROPERTIES
        // ============================================================

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Transparent")]
        public override Color BackColor
        {
            get => base.BackColor;
            set => base.BackColor = value;
        }

        [Category("Appearance")]
        [DefaultValue(18)]
        public int Radius
        {
            get => _radius;
            set
            {
                _radius = Math.Max(8, value);
                EnsureMinimumControlSize();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Whether the circular checkbox is drawn on the left or right side of the control.")]
        [DefaultValue(HorizontalAlignment.Left)]
        public HorizontalAlignment CheckBoxAlignment
        {
            get => _checkBoxAlignment;
            set
            {
                if (_checkBoxAlignment != value)
                {
                    _checkBoxAlignment = value == HorizontalAlignment.Right
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left;
                    Invalidate();
                }
            }
        }

        [Category("Layout")]
        [Description("Horizontal space between the checkbox glyph and the rendered text.")]
        [DefaultValue(8)]
        public int TextGap
        {
            get => _textGap;
            set
            {
                int newGap = Math.Max(0, value);
                if (_textGap != newGap)
                {
                    _textGap = newGap;
                    EnsureMinimumControlSize();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CheckedColor
        {
            get => _checkedColor;
            set { _checkedColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Colour of the tick itself. Dark by default to pair with the light checked fill.")]
        public Color TickColor
        {
            get => _tickColor;
            set { _tickColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Tick colour while hovered — the highlight tier, matching the hover border.")]
        public Color TickHoverColor
        {
            get => _tickHoverColor;
            set { _tickHoverColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Border colour while hovered. Hover never changes the fill — only the border darkens.")]
        public Color HoverBorderColor
        {
            get => _hoverBorderColor;
            set { _hoverBorderColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CheckBoxBackColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    OnCheckedChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        // ============================================================
        // EVENTS
        // ============================================================

        [Category("Action")]
        public event EventHandler? CheckedChanged;

        protected virtual void OnCheckedChanged(EventArgs e)
        {
            CheckedChanged?.Invoke(this, e);
        }

        // ============================================================
        // PAINTING
        // ============================================================

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            if (BackColor == Color.Transparent && Parent != null)
            {
                PaintParentBackground(pevent);
                return;
            }

            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle circleRect = GetCircleRectangle();

            // Determine fill color
            Color fillColor = _checked ? _checkedColor : _backgroundColor;

            // Apply fade if disabled
            if (!Enabled)
            {
                Color parentBg = Parent?.BackColor ?? SystemColors.Control;
                fillColor = FadeToward(fillColor, parentBg, _disabledFadePercent);
            }

            // Draw circle background
            using (SolidBrush brush = new SolidBrush(fillColor))
            {
                e.Graphics.FillEllipse(brush, circleRect);
            }

            // Draw border
            Color borderColor = _isHovered ? _hoverBorderColor : _borderColor;
            if (!Enabled)
            {
                Color parentBg = Parent?.BackColor ?? SystemColors.Control;
                borderColor = FadeToward(borderColor, parentBg, _disabledFadePercent);
            }

            using (Pen pen = new Pen(borderColor, 2))
            {
                e.Graphics.DrawEllipse(pen, circleRect);
            }

            // Draw checkmark if checked
            if (_checked)
            {
                DrawCheckmark(e.Graphics, circleRect, !Enabled);
            }

            DrawText(e.Graphics);
        }

        private void PaintParentBackground(PaintEventArgs e)
        {
            GraphicsState state = e.Graphics.Save();

            try
            {
                Rectangle parentClip = new Rectangle(
                    Left + e.ClipRectangle.Left,
                    Top + e.ClipRectangle.Top,
                    e.ClipRectangle.Width,
                    e.ClipRectangle.Height);

                e.Graphics.TranslateTransform(-Left, -Top);

                using PaintEventArgs parentArgs = new PaintEventArgs(e.Graphics, parentClip);
                InvokePaintBackground(Parent!, parentArgs);
                InvokePaint(Parent!, parentArgs);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }

        private void DrawCheckmark(Graphics g, Rectangle rect, bool faded = false)
        {
            // Calculate checkmark points (scaled to circle size)
            float scale = _radius / 18f;
            int centerX = rect.Left + rect.Width / 2;
            int centerY = rect.Top + rect.Height / 2;

            Point[] checkPoints = new Point[] {
        new Point(centerX - (int)(4 * scale), centerY),
        new Point(centerX - (int)(1 * scale), centerY + (int)(3 * scale)),
        new Point(centerX + (int)(5 * scale), centerY - (int)(3 * scale))
    };

            Color checkColor = _isHovered ? _tickHoverColor : _tickColor;
            if (faded)
            {
                Color parentBg = Parent?.BackColor ?? SystemColors.Control;
                checkColor = FadeToward(checkColor, parentBg, _disabledFadePercent);
            }

            using (Pen pen = new Pen(checkColor, Math.Max(2, scale * 2)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, checkPoints);
            }
        }

        // ========================== DISABLED FADE ==========================

        private int _disabledFadePercent = 60; // 60% fade-out (i.e., keep 40% intensity)

        [Category("Appearance")]
        [Description("When disabled, fade the control toward the parent background by this percent (0-100).")]
        [DefaultValue(60)]
        public int DisabledFadePercent
        {
            get => _disabledFadePercent;
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (_disabledFadePercent != v)
                {
                    _disabledFadePercent = v;
                    Invalidate(); // refresh immediately
                }
            }
        }

        private static Color FadeToward(Color source, Color background, int fadePercent)
        {
            float t = Math.Clamp(fadePercent / 100f, 0f, 1f);
            int r = (int)Math.Round(source.R * (1f - t) + background.R * t);
            int g = (int)Math.Round(source.G * (1f - t) + background.G * t);
            int b = (int)Math.Round(source.B * (1f - t) + background.B * t);
            int a = (int)Math.Round(source.A * (1f - t) + background.A * t);
            return Color.FromArgb(a, r, g, b);
        }

        private static void DrawImageFaded(Graphics g, Image img, Rectangle dest, int fadePercent)
        {
            if (fadePercent <= 0)
            {
                g.DrawImage(img, dest);
                return;
            }

            float keep = 1f - Math.Clamp(fadePercent / 100f, 0f, 1f);
            using var ia = new ImageAttributes();
            var cm = new ColorMatrix
            {
                Matrix00 = 1f,
                Matrix11 = 1f,
                Matrix22 = 1f,
                Matrix33 = keep,
                Matrix44 = 1f
            };
            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            g.DrawImage(
                img,
                dest,
                0, 0, img.Width, img.Height,
                GraphicsUnit.Pixel,
                ia
            );
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            EnsureMinimumControlSize();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            EnsureMinimumControlSize();
            Invalidate();
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            EnsureMinimumControlSize();
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int minimumHeight = _radius + 2 + Padding.Vertical;
            int width = _radius + 2 + Padding.Horizontal;
            int height = minimumHeight;

            if (!string.IsNullOrWhiteSpace(Text))
            {
                Size textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine);
                width += _textGap + textSize.Width;
                height = Math.Max(minimumHeight, textSize.Height + Padding.Vertical);
            }

            return new Size(width, height);
        }

        private Rectangle GetCircleRectangle()
        {
            int diameter = _radius;
            int top = Padding.Top + Math.Max(0, (ClientSize.Height - Padding.Vertical - diameter) / 2);
            int left;

            if (_checkBoxAlignment == HorizontalAlignment.Right)
            {
                left = Math.Max(Padding.Left, ClientSize.Width - Padding.Right - diameter - 1);
            }
            else
            {
                left = Padding.Left + 1;
            }

            return new Rectangle(left, top, diameter, diameter);
        }

        private void DrawText(Graphics g)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return;
            }

            Rectangle circleRect = GetCircleRectangle();
            Rectangle textRect;

            if (_checkBoxAlignment == HorizontalAlignment.Right)
            {
                textRect = new Rectangle(
                    Padding.Left,
                    Padding.Top,
                    Math.Max(0, circleRect.Left - Padding.Left - _textGap),
                    Math.Max(0, ClientSize.Height - Padding.Vertical));
            }
            else
            {
                int textLeft = circleRect.Right + _textGap;
                textRect = new Rectangle(
                    textLeft,
                    Padding.Top,
                    Math.Max(0, ClientSize.Width - textLeft - Padding.Right),
                    Math.Max(0, ClientSize.Height - Padding.Vertical));
            }

            Color textColor = Enabled
                ? ForeColor
                : FadeToward(ForeColor, Parent?.BackColor ?? SystemColors.Control, _disabledFadePercent);

            TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPadding;

            if (RightToLeft == RightToLeft.Yes)
            {
                flags |= TextFormatFlags.RightToLeft;
            }

            if (_checkBoxAlignment == HorizontalAlignment.Right)
            {
                flags |= TextFormatFlags.Right;
            }

            TextRenderer.DrawText(g, Text, Font, textRect, textColor, flags);
        }

        private void EnsureMinimumControlSize()
        {
            Size preferred = GetPreferredSize(Size.Empty);
            int newWidth = Math.Max(Width, preferred.Width);
            int newHeight = Math.Max(Height, preferred.Height);

            if (newWidth != Width || newHeight != Height)
            {
                Size = new Size(newWidth, newHeight);
            }
        }
    }
}
