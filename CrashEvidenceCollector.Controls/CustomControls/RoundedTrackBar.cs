using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum TrackBarValueLabelSide
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    public class RoundedTrackBar : Control, IAdaptiveRowPanelItem
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value = 50;
        private int _smallChange = 1;
        private int _largeChange = 10;
        private Orientation _orientation = Orientation.Horizontal;
        private bool _swapSizeOnOrientationChange = true;

        private int _cornerRadius = 12;
        private int _borderThickness = 1;
        private Color _innerColor = Color.White;
        private Color _borderColor = Color.FromArgb(180, 180, 180);
        private Color _disabledInnerColor = Color.FromArgb(250, 250, 250);
        private Color _disabledBorderColor = Color.FromArgb(220, 220, 220);
        private Color _disabledTextColor = Color.FromArgb(160, 160, 160);

        private Color _trackColor = Color.FromArgb(190, 190, 190);
        private Color _trackFillColor = Color.FromArgb(70, 130, 220);
        private Color _trackFillThresholdColor = Color.FromArgb(220, 80, 70);
        private Color _trackFillGradientStartColor = Color.FromArgb(70, 180, 120);
        private Color _trackFillGradientMiddleColor = Color.FromArgb(230, 190, 70);
        private Color _trackFillGradientEndColor = Color.FromArgb(220, 80, 70);
        private Color _trackFillThresholdGradientStartColor = Color.FromArgb(230, 190, 70);
        private Color _trackFillThresholdGradientEndColor = Color.FromArgb(220, 80, 70);
        private Color _thumbColor = Color.White;
        private Color _thumbHoverColor = Color.FromArgb(245, 245, 245);
        private Color _thumbPressedColor = Color.FromArgb(230, 240, 250);
        private Color _thumbBorderColor = Color.Gray;
        private Color _tickColor = Color.FromArgb(130, 130, 130);
        private Color _majorTickColor = Color.FromArgb(80, 80, 80);
        private Color _sectionLineColor = Color.FromArgb(220, 220, 220);

        private int _trackThickness = 4;
        private bool _useTrackFillThresholdColor;
        private int _trackFillThresholdValue = 100;
        private bool _useTrackFillGradient;
        private bool _useTrackFillGradientMiddleColor;
        private bool _useTrackFillThresholdGradient;
        private int _thumbThickness = 16;
        private int _thumbLength = 22;
        private int _thumbCornerRadius = 8;
        private int _thumbBorderThickness = 1;
        private bool _rotateThumbWithOrientation = true;
        private bool _showValueLabel = true;
        private TrackBarValueLabelSide _valueLabelSide = TrackBarValueLabelSide.Right;
        private int _valueSectionWidth = 46;
        private int _sectionGap = 8;
        private bool _showValueSeparator = true;
        private string _valueFormat = "{0}";
        private string _innerLeftText = string.Empty;
        private int _innerLeftTextWidth = 28;
        private int _innerLeftTextGap = 8;

        private bool _showTicks = true;
        private int _tickSpacing = 10;
        private int _tickHeight = 6;
        private bool _showMajorTicks = true;
        private int _majorTickSpacing = 50;
        private int _majorTickHeight = 10;
        private int _tickOffsetFromTrack = 8;

        private bool _isDragging;
        private bool _isHovered;
        private bool _innerLeftTextHovered;
        private bool _innerLeftTextPressed;

        public RoundedTrackBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);

            Size = new Size(220, 34);
            MinimumSize = new Size(80, 24);
            Font = new Font("Segoe UI", 9F);

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                BackColor = SystemColors.Control;
            else
            {
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }
        }

        public event EventHandler? ValueChanged;

        [Category("Action")]
        public event EventHandler? InnerLeftTextClick;

        [Category("Data")]
        [DefaultValue(0)]
        public int Minimum
        {
            get => _minimum;
            set
            {
                if (_minimum == value) return;
                _minimum = value;
                if (_maximum < _minimum) _maximum = _minimum;
                Value = Math.Clamp(_value, _minimum, _maximum);
                Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                int newValue = Math.Max(_minimum, value);
                if (_maximum == newValue) return;
                _maximum = newValue;
                Value = Math.Clamp(_value, _minimum, _maximum);
                Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue(50)]
        public int Value
        {
            get => _value;
            set
            {
                int newValue = Math.Clamp(value, _minimum, _maximum);
                if (_value == newValue) return;
                _value = newValue;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        public int SmallChange
        {
            get => _smallChange;
            set => _smallChange = Math.Max(1, value);
        }

        [Category("Behavior")]
        [DefaultValue(10)]
        public int LargeChange
        {
            get => _largeChange;
            set => _largeChange = Math.Max(1, value);
        }

        [Category("Behavior")]
        [DefaultValue(Orientation.Horizontal)]
        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation == value) return;
                _orientation = value;
                if (_orientation == Orientation.Vertical && _valueLabelSide == TrackBarValueLabelSide.Right)
                    _valueLabelSide = TrackBarValueLabelSide.Bottom;
                else if (_orientation == Orientation.Horizontal && _valueLabelSide == TrackBarValueLabelSide.Bottom)
                    _valueLabelSide = TrackBarValueLabelSide.Right;
                if (_swapSizeOnOrientationChange)
                    SwapTrackBarSizeForOrientation();
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool SwapSizeOnOrientationChange
        {
            get => _swapSizeOnOrientationChange;
            set => _swapSizeOnOrientationChange = value;
        }

        [Category("Appearance")]
        [DefaultValue(12)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(1)]
        public int BorderThickness
        {
            get => _borderThickness;
            set { _borderThickness = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        public Color InnerColor
        {
            get => _innerColor;
            set { _innerColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledInnerColor
        {
            get => _disabledInnerColor;
            set { _disabledInnerColor = value; Invalidate(); }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledBorderColor
        {
            get => _disabledBorderColor;
            set { _disabledBorderColor = value; Invalidate(); }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledTextColor
        {
            get => _disabledTextColor;
            set { _disabledTextColor = value; Invalidate(); }
        }

        [Category("Appearance - Track")]
        public Color TrackColor
        {
            get => _trackColor;
            set { _trackColor = value; Invalidate(); }
        }

        [Category("Appearance - Track")]
        public Color TrackFillColor
        {
            get => _trackFillColor;
            set { _trackFillColor = value; Invalidate(); }
        }

        [Category("Appearance - Track")]
        [DefaultValue(4)]
        public int TrackThickness
        {
            get => _trackThickness;
            set { _trackThickness = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance - Track")]
        [DefaultValue(false)]
        public bool UseTrackFillThresholdColor
        {
            get => _useTrackFillThresholdColor;
            set { _useTrackFillThresholdColor = value; Invalidate(); }
        }

        [Category("Appearance - Track")]
        [DefaultValue(100)]
        public int TrackFillThresholdValue
        {
            get => _trackFillThresholdValue;
            set { _trackFillThresholdValue = value; Invalidate(); }
        }

        [Category("Appearance - Track")]
        public Color TrackFillThresholdColor
        {
            get => _trackFillThresholdColor;
            set { _trackFillThresholdColor = value; Invalidate(); }
        }

        [Category("Appearance - Track Threshold Gradient")]
        [DefaultValue(false)]
        public bool UseTrackFillThresholdGradient
        {
            get => _useTrackFillThresholdGradient;
            set { _useTrackFillThresholdGradient = value; Invalidate(); }
        }

        [Category("Appearance - Track Threshold Gradient")]
        public Color TrackFillThresholdGradientStartColor
        {
            get => _trackFillThresholdGradientStartColor;
            set { _trackFillThresholdGradientStartColor = value; Invalidate(); }
        }

        [Category("Appearance - Track Threshold Gradient")]
        public Color TrackFillThresholdGradientEndColor
        {
            get => _trackFillThresholdGradientEndColor;
            set { _trackFillThresholdGradientEndColor = value; Invalidate(); }
        }

        [Category("Appearance - Track Gradient")]
        [DefaultValue(false)]
        public bool UseTrackFillGradient
        {
            get => _useTrackFillGradient;
            set { _useTrackFillGradient = value; Invalidate(); }
        }

        [Category("Appearance - Track Gradient")]
        public Color TrackFillGradientStartColor
        {
            get => _trackFillGradientStartColor;
            set { _trackFillGradientStartColor = value; Invalidate(); }
        }

        [Category("Appearance - Track Gradient")]
        [DefaultValue(false)]
        public bool UseTrackFillGradientMiddleColor
        {
            get => _useTrackFillGradientMiddleColor;
            set { _useTrackFillGradientMiddleColor = value; Invalidate(); }
        }

        [Category("Appearance - Track Gradient")]
        public Color TrackFillGradientMiddleColor
        {
            get => _trackFillGradientMiddleColor;
            set { _trackFillGradientMiddleColor = value; Invalidate(); }
        }

        [Category("Appearance - Track Gradient")]
        public Color TrackFillGradientEndColor
        {
            get => _trackFillGradientEndColor;
            set { _trackFillGradientEndColor = value; Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        public Color ThumbColor
        {
            get => _thumbColor;
            set { _thumbColor = value; Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        public Color ThumbHoverColor
        {
            get => _thumbHoverColor;
            set { _thumbHoverColor = value; Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        public Color ThumbPressedColor
        {
            get => _thumbPressedColor;
            set { _thumbPressedColor = value; Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        public Color ThumbBorderColor
        {
            get => _thumbBorderColor;
            set { _thumbBorderColor = value; Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(16)]
        public int ThumbWidth
        {
            get => _thumbThickness;
            set { _thumbThickness = Math.Max(4, value); Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(22)]
        public int ThumbHeight
        {
            get => _thumbLength;
            set { _thumbLength = Math.Max(8, value); Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(22)]
        public int ThumbLength
        {
            get => _thumbLength;
            set { _thumbLength = Math.Max(8, value); Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(16)]
        public int ThumbThickness
        {
            get => _thumbThickness;
            set { _thumbThickness = Math.Max(4, value); Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(8)]
        public int ThumbCornerRadius
        {
            get => _thumbCornerRadius;
            set { _thumbCornerRadius = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(1)]
        public int ThumbBorderThickness
        {
            get => _thumbBorderThickness;
            set { _thumbBorderThickness = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance - Thumb")]
        [DefaultValue(true)]
        public bool RotateThumbWithOrientation
        {
            get => _rotateThumbWithOrientation;
            set { _rotateThumbWithOrientation = value; Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        [DefaultValue(true)]
        public bool ShowValueLabel
        {
            get => _showValueLabel;
            set { _showValueLabel = value; Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        [DefaultValue(TrackBarValueLabelSide.Right)]
        public TrackBarValueLabelSide ValueLabelSide
        {
            get => _valueLabelSide;
            set { _valueLabelSide = value; Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        [DefaultValue(46)]
        public int ValueSectionWidth
        {
            get => _valueSectionWidth;
            set { _valueSectionWidth = Math.Max(12, value); Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        [DefaultValue(8)]
        public int SectionGap
        {
            get => _sectionGap;
            set { _sectionGap = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        [DefaultValue(true)]
        public bool ShowValueSeparator
        {
            get => _showValueSeparator;
            set { _showValueSeparator = value; Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        [DefaultValue("{0}")]
        public string ValueFormat
        {
            get => _valueFormat;
            set { _valueFormat = string.IsNullOrWhiteSpace(value) ? "{0}" : value; Invalidate(); }
        }

        [Category("Appearance - Inner Text")]
        [DefaultValue("")]
        public string InnerLeftText
        {
            get => _innerLeftText;
            set
            {
                string next = value ?? string.Empty;
                if (_innerLeftText == next)
                    return;

                _innerLeftText = next;
                Invalidate();
            }
        }

        [Category("Appearance - Inner Text")]
        [DefaultValue(28)]
        public int InnerLeftTextWidth
        {
            get => _innerLeftTextWidth;
            set
            {
                int next = Math.Max(0, value);
                if (_innerLeftTextWidth == next)
                    return;

                _innerLeftTextWidth = next;
                Invalidate();
            }
        }

        [Category("Appearance - Inner Text")]
        [DefaultValue(8)]
        public int InnerLeftTextGap
        {
            get => _innerLeftTextGap;
            set
            {
                int next = Math.Max(0, value);
                if (_innerLeftTextGap == next)
                    return;

                _innerLeftTextGap = next;
                Invalidate();
            }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(true)]
        public bool ShowTicks
        {
            get => _showTicks;
            set { _showTicks = value; Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(10)]
        public int TickSpacing
        {
            get => _tickSpacing;
            set { _tickSpacing = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(10)]
        public int TickFrequency
        {
            get => _tickSpacing;
            set { _tickSpacing = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(6)]
        public int TickHeight
        {
            get => _tickHeight;
            set { _tickHeight = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(true)]
        public bool ShowMajorTicks
        {
            get => _showMajorTicks;
            set { _showMajorTicks = value; Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(50)]
        public int MajorTickSpacing
        {
            get => _majorTickSpacing;
            set { _majorTickSpacing = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(10)]
        public int MajorTickHeight
        {
            get => _majorTickHeight;
            set { _majorTickHeight = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        [DefaultValue(8)]
        public int TickOffsetFromTrack
        {
            get => _tickOffsetFromTrack;
            set { _tickOffsetFromTrack = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        public Color TickColor
        {
            get => _tickColor;
            set { _tickColor = value; Invalidate(); }
        }

        [Category("Appearance - Ticks")]
        public Color MajorTickColor
        {
            get => _majorTickColor;
            set { _majorTickColor = value; Invalidate(); }
        }

        [Category("Appearance - Value Label")]
        public Color SectionLineColor
        {
            get => _sectionLineColor;
            set { _sectionLineColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle surface = new Rectangle(0, 0, Width - 1, Height - 1);
            if (surface.Width <= 0 || surface.Height <= 0) return;

            Color inner = Enabled ? _innerColor : _disabledInnerColor;
            Color border = Enabled ? _borderColor : _disabledBorderColor;
            Color text = Enabled ? ForeColor : _disabledTextColor;

            using GraphicsPath path = GetRoundedRectangle(surface, Math.Min(_cornerRadius, Height / 2));
            Region = new Region(path);

            using (var brush = new SolidBrush(inner))
                g.FillPath(brush, path);

            DrawValueSection(g, surface, text);
            DrawInnerLeftText(g, surface, text);
            DrawTrack(g);
            DrawTicks(g);
            DrawThumb(g);

            if (_borderThickness > 0)
            {
                Rectangle borderRect = Rectangle.Inflate(surface, -_borderThickness, -_borderThickness);
                using GraphicsPath borderPath = GetRoundedRectangle(borderRect, Math.Max(0, _cornerRadius - 1));
                using var pen = new Pen(border, _borderThickness);
                g.DrawPath(pen, borderPath);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Focus();

            if (IsInnerLeftTextHit(e.Location))
            {
                _innerLeftTextPressed = true;
                Capture = true;
                Invalidate();
                return;
            }

            _isDragging = true;
            Capture = true;
            SetValueFromPoint(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                SetValueFromPoint(e.Location);
                return;
            }

            bool innerTextHovered = IsInnerLeftTextHit(e.Location);
            if (_innerLeftTextHovered != innerTextHovered)
            {
                _innerLeftTextHovered = innerTextHovered;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_innerLeftTextPressed)
            {
                bool raiseClick = e.Button == MouseButtons.Left && IsInnerLeftTextHit(e.Location);
                _innerLeftTextPressed = false;
                Capture = false;
                Invalidate();

                if (raiseClick)
                    InnerLeftTextClick?.Invoke(this, EventArgs.Empty);

                return;
            }

            _isDragging = false;
            Capture = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _innerLeftTextHovered = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Enabled) return;
            Value += e.Delta > 0 ? _smallChange : -_smallChange;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
                || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;

            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down:
                    Value -= _smallChange;
                    e.Handled = true;
                    break;
                case Keys.Right:
                case Keys.Up:
                    Value += _smallChange;
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    Value -= _largeChange;
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    Value += _largeChange;
                    e.Handled = true;
                    break;
                case Keys.Home:
                    Value = _minimum;
                    e.Handled = true;
                    break;
                case Keys.End:
                    Value = _maximum;
                    e.Handled = true;
                    break;
            }
        }

        private void SwapTrackBarSizeForOrientation()
        {
            if (_orientation == Orientation.Vertical && Width > Height)
            {
                Size = new Size(Math.Max(24, Height), Math.Max(80, Width));
                MinimumSize = new Size(24, 80);
            }
            else if (_orientation == Orientation.Horizontal && Height > Width)
            {
                Size = new Size(Math.Max(80, Height), Math.Max(24, Width));
                MinimumSize = new Size(80, 24);
            }
        }

        private Rectangle GetTrackArea()
        {
            if (_orientation == Orientation.Vertical)
            {
                int top = 12 + GetOrientedThumbHeight() / 2;
                int bottom = Height - 13 - GetOrientedThumbHeight() / 2;
                int x = Width / 2;
                TrackBarValueLabelSide innerTextSide = GetInnerTextSide();

                if (innerTextSide == TrackBarValueLabelSide.Top)
                    top += _innerLeftTextWidth + _innerLeftTextGap;
                else if (innerTextSide == TrackBarValueLabelSide.Bottom)
                    bottom -= _innerLeftTextWidth + _innerLeftTextGap;
                else if (innerTextSide == TrackBarValueLabelSide.Left)
                    x += (_innerLeftTextWidth + _innerLeftTextGap) / 2;
                else if (innerTextSide == TrackBarValueLabelSide.Right)
                    x -= (_innerLeftTextWidth + _innerLeftTextGap) / 2;

                if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Top)
                    top += _valueSectionWidth + _sectionGap;
                else if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Bottom)
                    bottom -= _valueSectionWidth + _sectionGap;
                else if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Left)
                    x += (_valueSectionWidth + _sectionGap) / 2;
                else if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Right)
                    x -= (_valueSectionWidth + _sectionGap) / 2;

                int half = Math.Max(1, _trackThickness / 2);
                int left = x - half;
                int right = x + Math.Max(1, _trackThickness - half);

                if (bottom < top)
                    bottom = top;

                return Rectangle.FromLTRB(left, top, right, bottom);
            }

            int leftH = 12 + GetOrientedThumbWidth() / 2;
            int rightH = Width - 13 - GetOrientedThumbWidth() / 2;
            TrackBarValueLabelSide innerSide = GetInnerTextSide();

            if (innerSide == TrackBarValueLabelSide.Left)
                leftH += _innerLeftTextWidth + _innerLeftTextGap;
            else if (innerSide == TrackBarValueLabelSide.Right)
                rightH -= _innerLeftTextWidth + _innerLeftTextGap;

            if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Left)
                leftH += _valueSectionWidth + _sectionGap;
            else if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Right)
                rightH -= _valueSectionWidth + _sectionGap;

            if (rightH < leftH)
                rightH = leftH;

            int y = Height / 2;
            if (innerSide == TrackBarValueLabelSide.Top)
                y += (_innerLeftTextWidth + _innerLeftTextGap) / 2;
            else if (innerSide == TrackBarValueLabelSide.Bottom)
                y -= (_innerLeftTextWidth + _innerLeftTextGap) / 2;

            if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Top)
                y += (_valueSectionWidth + _sectionGap) / 2;
            else if (_showValueLabel && _valueLabelSide == TrackBarValueLabelSide.Bottom)
                y -= (_valueSectionWidth + _sectionGap) / 2;

            return Rectangle.FromLTRB(leftH, y - _trackThickness / 2, rightH, y + Math.Max(1, _trackThickness / 2));
        }

        private Rectangle GetValueSectionRect(Rectangle surface)
        {
            if (!_showValueLabel || _valueLabelSide == TrackBarValueLabelSide.None)
                return Rectangle.Empty;

            if (_valueLabelSide == TrackBarValueLabelSide.Top)
                return new Rectangle(surface.Left + 4, surface.Top + 4, surface.Width - 8, _valueSectionWidth);

            if (_valueLabelSide == TrackBarValueLabelSide.Bottom)
                return new Rectangle(surface.Left + 4, surface.Bottom - _valueSectionWidth - 4, surface.Width - 8, _valueSectionWidth);

            if (_valueLabelSide == TrackBarValueLabelSide.Left)
            {
                int left = surface.Left + 6;
                if (GetInnerTextSide() == TrackBarValueLabelSide.Left)
                    left += _innerLeftTextWidth + _innerLeftTextGap;

                return new Rectangle(left, surface.Top + 2, _valueSectionWidth, surface.Height - 4);
            }

            return new Rectangle(surface.Right - _valueSectionWidth - 6, surface.Top + 2, _valueSectionWidth, surface.Height - 4);
        }

        private bool HasInnerLeftText => !string.IsNullOrWhiteSpace(_innerLeftText) && _innerLeftTextWidth > 0;

        private TrackBarValueLabelSide GetInnerTextSide()
        {
            if (!HasInnerLeftText)
                return TrackBarValueLabelSide.None;

            if (_showValueLabel)
            {
                return _valueLabelSide switch
                {
                    TrackBarValueLabelSide.Left => TrackBarValueLabelSide.Right,
                    TrackBarValueLabelSide.Right => TrackBarValueLabelSide.Left,
                    TrackBarValueLabelSide.Top => TrackBarValueLabelSide.Bottom,
                    TrackBarValueLabelSide.Bottom => TrackBarValueLabelSide.Top,
                    _ => GetDefaultInnerTextSide()
                };
            }

            return GetDefaultInnerTextSide();
        }

        private TrackBarValueLabelSide GetDefaultInnerTextSide()
        {
            return _orientation == Orientation.Vertical
                ? TrackBarValueLabelSide.Top
                : TrackBarValueLabelSide.Left;
        }

        private Rectangle GetInnerLeftTextRect(Rectangle surface)
        {
            TrackBarValueLabelSide side = GetInnerTextSide();
            if (side == TrackBarValueLabelSide.None)
                return Rectangle.Empty;

            int sectionSize = Math.Min(_innerLeftTextWidth, Math.Max(0, Math.Min(surface.Width, surface.Height) - 4));
            if (sectionSize <= 0)
                sectionSize = _innerLeftTextWidth;

            if (side == TrackBarValueLabelSide.Top)
                return new Rectangle(surface.Left + 4, surface.Top + 4, Math.Max(0, surface.Width - 8), sectionSize);

            if (side == TrackBarValueLabelSide.Bottom)
                return new Rectangle(surface.Left + 4, surface.Bottom - sectionSize - 4, Math.Max(0, surface.Width - 8), sectionSize);

            int width = Math.Min(_innerLeftTextWidth, Math.Max(0, surface.Width - 8));
            if (width <= 0)
                return Rectangle.Empty;

            if (side == TrackBarValueLabelSide.Right)
                return new Rectangle(surface.Right - width - 6, surface.Top + 2, width, Math.Max(0, surface.Height - 4));

            return new Rectangle(surface.Left + 6, surface.Top + 2, width, Math.Max(0, surface.Height - 4));
        }

        private bool IsInnerLeftTextHit(Point point)
        {
            if (!Enabled)
                return false;

            Rectangle surface = new Rectangle(0, 0, Width - 1, Height - 1);
            Rectangle innerLeftTextRect = GetInnerLeftTextRect(surface);
            return !innerLeftTextRect.IsEmpty && innerLeftTextRect.Contains(point);
        }

        private void DrawInnerLeftText(Graphics g, Rectangle surface, Color textColor)
        {
            Rectangle textRect = GetInnerLeftTextRect(surface);
            if (textRect.IsEmpty)
                return;

            Color drawColor = _innerLeftTextPressed
                ? ControlPaint.Dark(textColor)
                : _innerLeftTextHovered
                    ? ControlPaint.Light(textColor)
                    : textColor;

            TrackBarValueLabelSide side = GetInnerTextSide();
            if (_orientation == Orientation.Vertical
                && (side == TrackBarValueLabelSide.Left || side == TrackBarValueLabelSide.Right))
            {
                DrawTopToBottomText(g, _innerLeftText, textRect, drawColor);
                return;
            }

            TextRenderer.DrawText(
                g,
                _innerLeftText,
                Font,
                textRect,
                drawColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawTopToBottomText(Graphics g, string text, Rectangle bounds, Color textColor)
        {
            List<string> elements = GetTextElements(text);
            if (elements.Count == 0)
                return;

            int cellHeight = Math.Max(1, bounds.Height / elements.Count);
            int y = bounds.Top + Math.Max(0, (bounds.Height - cellHeight * elements.Count) / 2);

            foreach (string element in elements)
            {
                Rectangle cell = new Rectangle(bounds.Left, y, bounds.Width, cellHeight);
                TextRenderer.DrawText(
                    g,
                    element,
                    Font,
                    cell,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                y += cellHeight;
            }
        }

        private static List<string> GetTextElements(string text)
        {
            var elements = new List<string>();
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
                elements.Add(enumerator.GetTextElement());

            return elements;
        }

        private void DrawValueSection(Graphics g, Rectangle surface, Color textColor)
        {
            Rectangle valueRect = GetValueSectionRect(surface);
            if (valueRect.IsEmpty) return;

            if (_showValueSeparator)
            {
                using var linePen = new Pen(_sectionLineColor, 1);
                if (_valueLabelSide == TrackBarValueLabelSide.Top)
                {
                    int y = valueRect.Bottom + _sectionGap / 2;
                    g.DrawLine(linePen, surface.Left + 5, y, surface.Right - 5, y);
                }
                else if (_valueLabelSide == TrackBarValueLabelSide.Bottom)
                {
                    int y = valueRect.Top - _sectionGap / 2;
                    g.DrawLine(linePen, surface.Left + 5, y, surface.Right - 5, y);
                }
                else
                {
                    int x = _valueLabelSide == TrackBarValueLabelSide.Left
                        ? valueRect.Right + _sectionGap / 2
                        : valueRect.Left - _sectionGap / 2;
                    g.DrawLine(linePen, x, surface.Top + 5, x, surface.Bottom - 5);
                }
            }

            string valueText;
            try
            {
                valueText = string.Format(_valueFormat, _value);
            }
            catch (FormatException)
            {
                valueText = _value.ToString();
            }

            TextRenderer.DrawText(
                g,
                valueText,
                Font,
                valueRect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawTrack(Graphics g)
        {
            Rectangle track = GetTrackArea();
            if (track.Width <= 0 || track.Height <= 0) return;

            using GraphicsPath trackPath = GetRoundedRectangle(track, _trackThickness);
            using (var brush = new SolidBrush(Enabled ? _trackColor : _disabledBorderColor))
                g.FillPath(brush, trackPath);

            Rectangle fill;
            if (_orientation == Orientation.Vertical)
            {
                int thumbY = GetThumbCenterY();
                fill = Rectangle.FromLTRB(track.Left, Math.Min(track.Bottom, thumbY), track.Right, track.Bottom);
            }
            else
            {
                int thumbX = GetThumbCenterX();
                fill = Rectangle.FromLTRB(track.Left, track.Top, Math.Max(track.Left, thumbX), track.Bottom);
            }

            if (fill.Width > 0 && fill.Height > 0)
                DrawTrackFill(g, track, fill);
        }

        private void DrawTrackFill(Graphics g, Rectangle track, Rectangle fill)
        {
            if (!Enabled)
            {
                using var disabledBrush = new SolidBrush(_disabledTextColor);
                FillTrackSegment(g, fill, disabledBrush);
                return;
            }

            if (_useTrackFillThresholdColor && _maximum > _minimum)
            {
                int threshold = Math.Clamp(_trackFillThresholdValue, _minimum, _maximum);
                if (_orientation == Orientation.Vertical)
                {
                    int thresholdY = ValueToY(threshold);

                    if (thresholdY > fill.Top && thresholdY < fill.Bottom)
                    {
                        Rectangle thresholdFill = Rectangle.FromLTRB(fill.Left, fill.Top, fill.Right, thresholdY);
                        using Brush thresholdBrush = CreateTrackThresholdFillBrush(track);
                        FillTrackSegment(g, thresholdFill, thresholdBrush);

                        Rectangle normalFill = Rectangle.FromLTRB(fill.Left, thresholdY, fill.Right, fill.Bottom);
                        using Brush normalBrush = CreateTrackFillBrush(track);
                        FillTrackSegment(g, normalFill, normalBrush);
                        return;
                    }

                    if (fill.Top <= thresholdY && fill.Bottom <= thresholdY)
                    {
                        using Brush thresholdBrush = CreateTrackThresholdFillBrush(track);
                        FillTrackSegment(g, fill, thresholdBrush);
                        return;
                    }
                }
                else
                {
                    int thresholdX = ValueToX(threshold);

                    if (thresholdX > fill.Left && thresholdX < fill.Right)
                    {
                        Rectangle normalFill = Rectangle.FromLTRB(fill.Left, fill.Top, thresholdX, fill.Bottom);
                        using Brush normalBrush = CreateTrackFillBrush(track);
                        FillTrackSegment(g, normalFill, normalBrush);

                        Rectangle thresholdFill = Rectangle.FromLTRB(thresholdX, fill.Top, fill.Right, fill.Bottom);
                        using Brush thresholdBrush = CreateTrackThresholdFillBrush(track);
                        FillTrackSegment(g, thresholdFill, thresholdBrush);
                        return;
                    }

                    if (fill.Left >= thresholdX)
                    {
                        using Brush thresholdBrush = CreateTrackThresholdFillBrush(track);
                        FillTrackSegment(g, fill, thresholdBrush);
                        return;
                    }
                }
            }

            using Brush fillBrush = CreateTrackFillBrush(track);
            FillTrackSegment(g, fill, fillBrush);
        }

        private Brush CreateTrackFillBrush(Rectangle track)
        {
            if (!_useTrackFillGradient || track.Width <= 1 || track.Height <= 1)
                return new SolidBrush(_trackFillColor);

            var brush = _orientation == Orientation.Vertical
                ? new LinearGradientBrush(track, _trackFillGradientEndColor, _trackFillGradientStartColor, LinearGradientMode.Vertical)
                : new LinearGradientBrush(track, _trackFillGradientStartColor, _trackFillGradientEndColor, LinearGradientMode.Horizontal);
            if (_useTrackFillGradientMiddleColor)
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, 0.5f, 1f },
                    Colors = _orientation == Orientation.Vertical
                        ? new[] { _trackFillGradientEndColor, _trackFillGradientMiddleColor, _trackFillGradientStartColor }
                        : new[] { _trackFillGradientStartColor, _trackFillGradientMiddleColor, _trackFillGradientEndColor }
                };
            }

            return brush;
        }

        private Brush CreateTrackThresholdFillBrush(Rectangle track)
        {
            if (!_useTrackFillThresholdGradient || track.Width <= 1 || track.Height <= 1)
                return new SolidBrush(_trackFillThresholdColor);

            return _orientation == Orientation.Vertical
                ? new LinearGradientBrush(track, _trackFillThresholdGradientEndColor, _trackFillThresholdGradientStartColor, LinearGradientMode.Vertical)
                : new LinearGradientBrush(track, _trackFillThresholdGradientStartColor, _trackFillThresholdGradientEndColor, LinearGradientMode.Horizontal);
        }

        private void FillTrackSegment(Graphics g, Rectangle segment, Brush brush)
        {
            if (segment.Width <= 0 || segment.Height <= 0) return;
            using GraphicsPath path = GetRoundedRectangle(segment, _trackThickness);
            g.FillPath(brush, path);
        }

        private void DrawTicks(Graphics g)
        {
            if (!_showTicks || _maximum <= _minimum) return;

            Rectangle track = GetTrackArea();

            using var tickPen = new Pen(_tickColor, 1);
            using var majorPen = new Pen(_majorTickColor, 1);

            for (int value = _minimum; value <= _maximum; value += _tickSpacing)
            {
                bool isMajor = _showMajorTicks && ((value - _minimum) % _majorTickSpacing == 0);
                int height = isMajor ? _majorTickHeight : _tickHeight;
                if (_orientation == Orientation.Vertical)
                {
                    int x = track.Left - _tickOffsetFromTrack;
                    int y = ValueToY(value);
                    g.DrawLine(isMajor ? majorPen : tickPen, x, y, x + height, y);
                }
                else
                {
                    int x = ValueToX(value);
                    int y = track.Top - _tickOffsetFromTrack;
                    g.DrawLine(isMajor ? majorPen : tickPen, x, y, x, y + height);
                }
            }
        }

        private void DrawThumb(Graphics g)
        {
            int x = _orientation == Orientation.Vertical ? GetThumbCenterX() : GetThumbCenterX();
            int y = _orientation == Orientation.Vertical ? GetThumbCenterY() : Height / 2;
            int thumbWidth = GetOrientedThumbWidth();
            int thumbHeight = GetOrientedThumbHeight();
            Rectangle rect = new Rectangle(
                x - thumbWidth / 2,
                y - thumbHeight / 2,
                thumbWidth,
                thumbHeight);

            Color thumb = _isDragging
                ? _thumbPressedColor
                : _isHovered
                    ? _thumbHoverColor
                    : _thumbColor;
            using GraphicsPath path = GetRoundedRectangle(rect, Math.Min(_thumbCornerRadius, rect.Height / 2));
            using (var brush = new SolidBrush(Enabled ? thumb : _disabledInnerColor))
                g.FillPath(brush, path);

            if (_thumbBorderThickness > 0)
            {
                using var pen = new Pen(Enabled ? _thumbBorderColor : _disabledBorderColor, _thumbBorderThickness);
                g.DrawPath(pen, path);
            }
        }

        private int GetOrientedThumbWidth()
        {
            return _orientation == Orientation.Vertical && _rotateThumbWithOrientation
                ? _thumbLength
                : _thumbThickness;
        }

        private int GetOrientedThumbHeight()
        {
            return _orientation == Orientation.Vertical && _rotateThumbWithOrientation
                ? _thumbThickness
                : _thumbLength;
        }

        private int GetThumbCenterX()
        {
            Rectangle track = GetTrackArea();
            return _orientation == Orientation.Vertical
                ? track.Left + track.Width / 2
                : ValueToX(_value);
        }

        private int GetThumbCenterY()
        {
            Rectangle track = GetTrackArea();
            return _orientation == Orientation.Vertical
                ? ValueToY(_value)
                : track.Top + track.Height / 2;
        }

        private int ValueToX(int value)
        {
            Rectangle track = GetTrackArea();
            if (_maximum <= _minimum) return track.Left;
            float percent = (value - _minimum) / (float)(_maximum - _minimum);
            return track.Left + (int)Math.Round(track.Width * percent);
        }

        private int ValueToY(int value)
        {
            Rectangle track = GetTrackArea();
            if (_maximum <= _minimum) return track.Bottom;
            float percent = (value - _minimum) / (float)(_maximum - _minimum);
            return track.Bottom - (int)Math.Round(track.Height * percent);
        }

        private void SetValueFromPoint(Point point)
        {
            Rectangle track = GetTrackArea();
            if (track.Width <= 0 || track.Height <= 0 || _maximum <= _minimum) return;

            float percent = _orientation == Orientation.Vertical
                ? Math.Clamp((track.Bottom - point.Y) / (float)track.Height, 0f, 1f)
                : Math.Clamp((point.X - track.Left) / (float)track.Width, 0f, 1f);

            Value = _minimum + (int)Math.Round((_maximum - _minimum) * percent);
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
                return path;

            int diameter = Math.Min(Math.Min(radius * 2, rect.Width), rect.Height);
            if (diameter <= 1)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
