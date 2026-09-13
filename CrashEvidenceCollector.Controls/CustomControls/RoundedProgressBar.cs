using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public class RoundedProgressBar : Control, IAdaptiveRowPanelItem
    {
        private int _value = 0;
        private int _maximum = 100;
        private int _cornerRadius = 10;
        private Color _progressColor = Color.FromArgb(0, 123, 255);
        private Color _progressColor2 = Color.FromArgb(0, 200, 255);
        private Color _backgroundColor = Color.FromArgb(45, 45, 48);
        private string _text = string.Empty;
        private bool _useGradient = true;

        public RoundedProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            ForeColor = Color.White;
            Size = new Size(300, 30);
        }

        // Properties
        public int Value
        {
            get => _value;
            set
            {
                if (value < 0) value = 0;
                if (value > _maximum) value = _maximum;

                if (_value != value)
                {
                    _value = value;
                    Invalidate();
                }
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                if (value < 1) value = 1;
                _maximum = value;
                if (_value > _maximum) _value = _maximum;
                Invalidate();
            }
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = value;
                Invalidate();
            }
        }

        public Color ProgressColor
        {
            get => _progressColor;
            set
            {
                _progressColor = value;
                Invalidate();
            }
        }

        public Color ProgressColor2
        {
            get => _progressColor2;
            set
            {
                _progressColor2 = value;
                Invalidate();
            }
        }

        public Color ProgressBackColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                Invalidate();
            }
        }

        public bool UseGradient
        {
            get => _useGradient;
            set
            {
                _useGradient = value;
                Invalidate();
            }
        }


        /// <summary>
        /// Thickness of the outline, in pixels. Zero draws no border at all - for a bar that sits
        /// inside a panel that already has an edge, an outline of its own just reads as clutter.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(1)]
        [Description("Outline thickness in pixels. 0 draws no border.")]
        public int BorderSize
        {
            get => _borderSize;
            set
            {
                int clamped = Math.Max(0, value);
                if (_borderSize == clamped) return;

                _borderSize = clamped;
                Invalidate();
            }
        }
        private int _borderSize = 1;

        #pragma warning disable CS8765
        public override string Text
        {
            get => _text;
            set
            {
                _text = value;  // Just assign it directly
                Invalidate();
            }
        }
        #pragma warning restore CS8765


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Draw background
            using (GraphicsPath bgPath = GetRoundedRectangle(ClientRectangle, _cornerRadius))
            using (SolidBrush bgBrush = new SolidBrush(_backgroundColor))
            {
                g.FillPath(bgBrush, bgPath);
            }

            // Calculate progress width
            float percent = _maximum > 0 ? (float)_value / _maximum : 0;
            int progressWidth = (int)(ClientRectangle.Width * percent);

            if (progressWidth > 0)
            {
                Rectangle progressRect = new Rectangle(0, 0, progressWidth, Height);

                using (GraphicsPath progressPath = GetRoundedRectangle(progressRect, _cornerRadius))
                {
                    if (_useGradient && progressWidth > _cornerRadius * 2)
                    {
                        using (LinearGradientBrush gradBrush = new LinearGradientBrush(
                            progressRect, _progressColor, _progressColor2, LinearGradientMode.Horizontal))
                        {
                            g.FillPath(gradBrush, progressPath);
                        }
                    }
                    else
                    {
                        using (SolidBrush solidBrush = new SolidBrush(_progressColor))
                        {
                            g.FillPath(solidBrush, progressPath);
                        }
                    }
                }
            }

            // Draw border. Skipped entirely at zero rather than drawn with a zero-width pen —
            // GDI+ treats width 0 as "thinnest possible line", which is 1px, not none.
            if (_borderSize > 0)
            {
                using GraphicsPath borderPath = GetRoundedRectangle(ClientRectangle, _cornerRadius);
                using var borderPen = new Pen(Color.FromArgb(60, 60, 60), _borderSize);
                g.DrawPath(borderPen, borderPath);
            }

            // Draw text
            if (!string.IsNullOrEmpty(_text))
            {
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;

                    // Draw shadow for better readability
                    using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                    {
                        Rectangle shadowRect = new Rectangle(ClientRectangle.X + 1, ClientRectangle.Y + 1,
                                                            ClientRectangle.Width, ClientRectangle.Height);
                        g.DrawString(_text, Font, shadowBrush, shadowRect, sf);
                    }

                    // Draw main text
                    using (SolidBrush textBrush = new SolidBrush(ForeColor))
                    {
                        g.DrawString(_text, Font, textBrush, ClientRectangle, sf);
                    }
                }
            }
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();

            if (radius <= 0 || rect.Width < radius || rect.Height < radius)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            // Top left
            path.AddArc(arc, 180, 90);

            // Top right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
