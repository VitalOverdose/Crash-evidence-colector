#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public sealed class SwitchChangedEventArgs : EventArgs
    {
        public bool OnPosition { get; }
        public SwitchChangedEventArgs(bool onPosition) { OnPosition = onPosition; }
    }

    /// <summary>
    /// Where the text label sits relative to the pill. The first three members keep the same
    /// ordinal values as the old HorizontalAlignment-typed property (Left=0, Right=1, Center=2)
    /// so any numeric cast keeps meaning; Up/Down stack the label above/below the pill.
    /// </summary>
    public enum ToggleLabelSide
    {
        Left = 0,
        Right = 1,
        Center = 2,
        Up = 3,
        Down = 4
    }

    public class RoundedToggleSwitch : Control, IAdaptiveRowPanelItem
    {
        // ========================================================================================
        // STATE
        // ========================================================================================

        private bool _onPosition = false;
        private float _animationPosition = 0f;
        private float _animationTarget = 0f;
        private readonly System.Windows.Forms.Timer _animationTimer;

        // ========================================================================================
        // EVENTS
        // ========================================================================================

        public event EventHandler<SwitchChangedEventArgs>? SwitchChanged;

        // ========================================================================================
        // APPEARANCE PROPERTIES
        // ========================================================================================

        [Category("Appearance")]
        public Color KnobColorOff { get; set; } = Color.White;

        [Category("Appearance")]
        public Color KnobColorOn { get; set; } = Color.White;

        [Category("Appearance")]
        public Color BackgroundOff { get; set; } = Color.LightGray;   // matches the old hardcoded track fill

        [Category("Appearance")]
        public Color BorderColor { get; set; } = Color.FromArgb(120, 120, 120);

        [Category("Appearance")]
        public Color BackgroundOnLeft { get; set; } = Color.LightGray;

        [Category("Appearance")]
        public Color DisabledOverlay { get; set; } = Color.FromArgb(150, Color.Gray);

        [Category("Appearance")]
        [Description("Thin outline around the knob. Transparent = no outline.")]
        public Color KnobBorderColor { get; set; } = Color.FromArgb(170, 170, 170);

        // Default to the classic geometry as concrete pixel values (28px control - 2px*2 padding).
        private int _trackHeight = 24;
        private int _knobSize = 24;

        [Category("Layout")]
        [Description("Height of the backing the knob slides along, centred vertically.")]
        [DefaultValue(24)]
        public int TrackHeight
        {
            get => _trackHeight;
            set
            {
                int v = Math.Max(0, value);
                if (_trackHeight != v)
                {
                    _trackHeight = v;
                    Invalidate();
                }
            }
        }

        [Category("Layout")]
        [Description("Diameter of the knob, centred vertically — independent of the track, may overhang a slim track.")]
        [DefaultValue(24)]
        public int KnobSize
        {
            get => _knobSize;
            set
            {
                int v = Math.Max(0, value);
                if (_knobSize != v)
                {
                    _knobSize = v;
                    Invalidate();
                }
            }
        }

        // ========================================================================================
        // LABEL PROPERTIES
        // ========================================================================================

        private ToggleLabelSide _labelSide = ToggleLabelSide.Right;
        private int _textGap = 8;
        private int _pillWidth = 60;
        private int _textOffsetY;

        [Category("Appearance")]
        [Description("Side of the pill on which the text label is drawn. Up/Down stack the label above/below the pill.")]
        [DefaultValue(ToggleLabelSide.Right)]
        public ToggleLabelSide LabelSide
        {
            get => _labelSide;
            set
            {
                if (_labelSide != value)
                {
                    _labelSide = value;
                    EnsureMinimumControlSize();
                    Invalidate();
                }
            }
        }

        [Category("Layout")]
        [Description("Vertical nudge for the text label in pixels. Positive moves it down, negative up.")]
        [DefaultValue(0)]
        public int TextOffsetY
        {
            get => _textOffsetY;
            set
            {
                if (_textOffsetY != value)
                {
                    _textOffsetY = value;
                    Invalidate();
                }
            }
        }

        [Category("Layout")]
        [Description("Horizontal space between the pill and the text label.")]
        [DefaultValue(8)]
        public int TextGap
        {
            get => _textGap;
            set
            {
                int v = Math.Max(0, value);
                if (_textGap != v)
                {
                    _textGap = v;
                    Invalidate();
                }
            }
        }

        [Category("Layout")]
        [Description("Width of the pill portion of the control.")]
        [DefaultValue(60)]
        public int PillWidth
        {
            get => _pillWidth;
            set
            {
                int v = Math.Max(20, value);
                if (_pillWidth != v)
                {
                    _pillWidth = v;
                    Invalidate();
                }
            }
        }

        // ========================================================================================
        // STATE PROPERTY
        // ========================================================================================

        [Category("Behavior")]
        public bool OnPosition
        {
            get => _onPosition;
            set
            {
                if (_onPosition == value)
                    return;

                _onPosition = value;
                _animationTarget = _onPosition ? 1f : 0f;
                _animationTimer.Start();
                OnSwitchChanged(new SwitchChangedEventArgs(_onPosition));
                Invalidate();
            }
        }

        // ========================================================================================
        // CONSTRUCTOR
        // ========================================================================================

        public RoundedToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(60, 28);
            Cursor = Cursors.Hand;

            _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        // ========================================================================================
        // OVERRIDES
        // ========================================================================================

        protected virtual void OnSwitchChanged(SwitchChangedEventArgs e)
        {
            SwitchChanged?.Invoke(this, e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            EnsureMinimumControlSize();
            Invalidate();
        }

        // ========================================================================================
        // HOVER FONT
        // ========================================================================================

        private bool _isHovered;
        private Font? _fontHovered;

        /// <summary>
        /// Font used for the label while the mouse is over the control. Unset means no change on
        /// hover. Named to match IconButton / ModernButton / RoundedComboBox, which all call it
        /// FontHovered - one name for one idea across the set.
        /// </summary>
        [Category("Appearance")]
        [Description("Font used for the label while the mouse is over the control. Defaults to Font.")]
        public Font FontHovered
        {
            get => _fontHovered ?? Font;
            set { _fontHovered = value; Invalidate(); }
        }

        private bool ShouldSerializeFontHovered() => _fontHovered is not null;

        private void ResetFontHovered() { _fontHovered = null; Invalidate(); }

        /// <summary>The font the label is actually drawn in right now.</summary>
        private Font ActiveFont =>
            (_isHovered && Enabled && _fontHovered is not null) ? _fontHovered : Font;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);

            if (_fontHovered is null) return;   // nothing to repaint for

            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (!_isHovered) return;

            _isHovered = false;
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            EnsureMinimumControlSize();
            Invalidate();
        }

        private void EnsureMinimumControlSize()
        {
            Size preferred = GetPreferredSize(Size.Empty);
            int newWidth = Math.Max(Width, preferred.Width);
            int newHeight = Math.Max(Height, preferred.Height);
            if (newWidth != Width || newHeight != Height)
                Size = new Size(newWidth, newHeight);
        }

        private bool IsVerticalLabel => _labelSide is ToggleLabelSide.Up or ToggleLabelSide.Down;

        /// <summary>
        /// Stacked (Up/Down) geometry: the label strip, the gap and the pill band are ONE group
        /// centred vertically in the control, so the visible gap is exactly TextGap however tall
        /// the control is. (Centring the pill in the left-over space instead made every extra
        /// pixel of height inflate the gap.)
        /// </summary>
        private void GetStackedLayout(out int textTop, out int bandTop, out int bandBottom)
        {
            int textH = MeasureTextHeight();
            int pillBand = Math.Max(_trackHeight, _knobSize);
            int groupTop = Math.Max(0, (Height - (textH + _textGap + pillBand)) / 2);

            if (_labelSide == ToggleLabelSide.Up)
            {
                textTop = groupTop;
                bandTop = Math.Min(Height, groupTop + textH + _textGap);
                bandBottom = Math.Min(Height, bandTop + pillBand);
            }
            else
            {
                bandTop = groupTop;
                bandBottom = Math.Min(Height, groupTop + pillBand);
                textTop = bandBottom + _textGap;
            }
        }

        private int MeasureTextHeight()
        {
            // Measured against the LARGER of the two fonts, not the one currently showing: the
            // control must not resize when the mouse arrives, and a hover font that does not fit
            // would otherwise be clipped the moment it appears.
            int height = TextRenderer.MeasureText(Text, Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height;

            if (_fontHovered is null) return height;

            return Math.Max(height, TextRenderer.MeasureText(Text, _fontHovered, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = _pillWidth;
            int height = Height;

            if (!string.IsNullOrWhiteSpace(Text))
            {
                Size textSize = TextRenderer.MeasureText(Text, Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                // Same reason as MeasureTextHeight: reserve for the bigger font so hovering
                // never changes the control's size or clips the label.
                if (_fontHovered is not null)
                {
                    Size hovered = TextRenderer.MeasureText(Text, _fontHovered, Size.Empty,
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    textSize = new Size(Math.Max(textSize.Width, hovered.Width),
                                        Math.Max(textSize.Height, hovered.Height));
                }

                if (IsVerticalLabel)
                {
                    // Stacked: control must fit the wider of pill/text, and text + gap + pill band.
                    width = Math.Max(width, textSize.Width);
                    int pillBand = Math.Max(_trackHeight, _knobSize) + 4;
                    height = Math.Max(height, textSize.Height + _textGap + pillBand);
                }
                else
                {
                    width += _textGap + textSize.Width;
                    height = Math.Max(height, textSize.Height);
                }
            }

            return new Size(width, height);
        }

        // ========================================================================================
        // ANIMATION
        // ========================================================================================

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            const float speed = 0.15f;
            _animationPosition += (_animationTarget - _animationPosition) * speed;

            if (Math.Abs(_animationPosition - _animationTarget) < 0.01f)
            {
                _animationPosition = _animationTarget;
                _animationTimer.Stop();
            }

            Invalidate();
        }

        // ========================================================================================
        // MOUSE
        // ========================================================================================

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (Enabled)
                OnPosition = !OnPosition;
        }

        // ========================================================================================
        // PAINT
        // ========================================================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int padding = 2;

            // Pill bounds — position is always driven by LabelSide
            bool hasText = !string.IsNullOrWhiteSpace(Text);
            bool verticalLabel = hasText && IsVerticalLabel;

            // Vertical band the pill lives in. Full height normally; for Up/Down the label
            // claims its strip (plus TextGap) at the top/bottom and the pill gets the rest.
            int bandTop = 0;
            int bandBottom = Height;
            if (verticalLabel)
            {
                GetStackedLayout(out _, out bandTop, out bandBottom);
            }

            int pillLeft, pillRight;
            if (!hasText)
            {
                // No text — pill fills the whole control (backward compat)
                pillLeft = 0;
                pillRight = Width;
            }
            else if (verticalLabel)
            {
                // Stacked label — pill centred horizontally
                pillLeft = Math.Max(0, (Width - _pillWidth) / 2);
                pillRight = Math.Min(Width, pillLeft + _pillWidth);
            }
            else if (_labelSide == ToggleLabelSide.Left)
            {
                // Text on left → pill on right
                pillLeft = Math.Max(0, Width - _pillWidth);
                pillRight = Width;
            }
            else
            {
                // Text on right → pill on left
                pillLeft = 0;
                pillRight = Math.Min(Width, _pillWidth);
            }

            // Track and knob are sized INDEPENDENTLY, both centred vertically in the band,
            // so a large knob can overhang a slim track.
            int bandHeight = Math.Max(0, bandBottom - bandTop);
            int trackH = Math.Min(_trackHeight, bandHeight);
            int knobDiameter = Math.Min(_knobSize, bandHeight);

            int trackY = bandTop + (bandHeight - trackH) / 2;
            float knobY = bandTop + (bandHeight - knobDiameter) / 2f;

            Rectangle backgroundRect = new Rectangle(
                pillLeft + padding, trackY,
                pillRight - pillLeft - padding * 2, trackH);

            float knobLeftX = pillLeft + padding;
            float knobRightX = pillRight - padding - knobDiameter;
            float knobX = knobLeftX + (_animationPosition * (knobRightX - knobLeftX));
            RectangleF knobRect = new RectangleF(knobX, knobY, knobDiameter, knobDiameter);

            // Track — colour animates OFF → ON along with the knob travel.
            Color trackColor = Blend(BackgroundOff, BackgroundOnLeft, _animationPosition);
            using (GraphicsPath bgPath = GetRoundedRect(backgroundRect, trackH))
            {
                using (SolidBrush bgBrush = new SolidBrush(trackColor))
                    g.FillPath(bgBrush, bgPath);

                using (Pen borderPen = new Pen(BorderColor, 1f))
                {
                    borderPen.Alignment = PenAlignment.Inset;
                    g.DrawPath(borderPen, bgPath);
                }
            }

            // Knob — colour animates too, with an optional thin outline.
            Color knobColor = Blend(KnobColorOff, KnobColorOn, _animationPosition);
            using (SolidBrush knobBrush = new SolidBrush(knobColor))
                g.FillEllipse(knobBrush, knobRect);

            if (KnobBorderColor.A > 0)
            {
                using Pen knobPen = new Pen(KnobBorderColor, 1f);
                g.DrawEllipse(knobPen, knobRect.X, knobRect.Y, knobRect.Width - 1, knobRect.Height - 1);
            }

            // Disabled overlay (track + knob)
            if (!Enabled)
            {
                using SolidBrush disabledBrush = new SolidBrush(DisabledOverlay);
                using (GraphicsPath disabledPath = GetRoundedRect(backgroundRect, trackH))
                    g.FillPath(disabledBrush, disabledPath);
                g.FillEllipse(disabledBrush, knobRect);
            }

            DrawLabel(g);
        }

        private void DrawLabel(Graphics g)
        {
            if (string.IsNullOrWhiteSpace(Text))
                return;

            Rectangle textRect;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPadding;

            switch (_labelSide)
            {
                case ToggleLabelSide.Up:
                case ToggleLabelSide.Down:
                    GetStackedLayout(out int stackedTextTop, out _, out _);
                    textRect = new Rectangle(0, stackedTextTop, Width, MeasureTextHeight());
                    flags |= TextFormatFlags.HorizontalCenter;
                    break;

                case ToggleLabelSide.Right:
                    int textLeft = _pillWidth + _textGap;
                    textRect = new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height);
                    break;

                default:    // Left and Center keep their historical behavior
                    int textWidth = Math.Max(0, Width - _pillWidth - _textGap);
                    textRect = new Rectangle(0, 0, textWidth, Height);
                    flags |= TextFormatFlags.Right;
                    break;
            }

            textRect.Offset(0, _textOffsetY);
            TextRenderer.DrawText(g, Text, ActiveFont, textRect, ForeColor, flags);
        }

        // ========================================================================================
        // HELPER
        // ========================================================================================

        private static Color Blend(Color from, Color to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(from.A + (to.A - from.A) * t),
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }

        private static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
