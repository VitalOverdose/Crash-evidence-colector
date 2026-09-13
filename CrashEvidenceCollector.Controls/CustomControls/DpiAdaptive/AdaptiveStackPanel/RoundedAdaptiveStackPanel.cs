using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.DotNet.DesignTools.Editors;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    [Designer(typeof(AdaptiveStackPanelDesigner))]
    public class RoundedAdaptiveStackPanel : AdaptiveStackPanel
    {
        private int _cornerRadius = 15;
        private int _borderThickness = 1;
        private Color _innerColor = Color.White;
        private Color _borderColor = Color.FromArgb(80, 80, 80);
        private bool _enableHoverEffects;
        private bool _isHovered;
        private bool _isSelected;
        private Color _hoverBorderColor = Color.FromArgb(100, 149, 237);
        private int _hoverBorderThickness = 2;
        private Color _selectedBorderColor = Color.Blue;
        private int _selectedBorderThickness = 2;
        private Color _selectionEffectColor = Color.FromArgb(128, 128, 255);

        private int _topBarHeight = 36;
        private int _topTextCharLimit;
        private int _topTextOffsetX;
        private bool _topSeparatorVisible;
        private bool _topBarVisible;
        private Font _topFont = new("Segoe UI", 11f, FontStyle.Regular);
        private Color _topTextColorDisabled = Color.FromArgb(160, 160, 160);
        private Color _topSeparatorColor = Color.DarkGray;
        private Color _topTextColor = Color.FromArgb(31, 31, 31);
        private Color _topBarColor = Color.FromArgb(230, 235, 240);
        private string _topText = string.Empty;
        private RoundedPanelFaster.BarTextVerticalPosition _topTextVerticalPosition = RoundedPanelFaster.BarTextVerticalPosition.Middle;

        private int _bottomTextCharLimit;
        private int _bottomTextOffsetX;
        private int _bottomBarHeight = 36;
        private bool _bottomSeparatorVisible;
        private bool _bottomBarVisible;
        private Font _bottomFont = new("Segoe UI", 11f, FontStyle.Regular);
        private Color _bottomTextColor = Color.FromArgb(31, 31, 31);
        private Color _bottomBarColor = Color.FromArgb(230, 235, 240);
        private Color _bottomSeparatorColor = Color.DarkGray;
        private Color _bottomTextColorDisabled = Color.FromArgb(160, 160, 160);
        private string _bottomText = string.Empty;
        private RoundedPanelFaster.BarTextVerticalPosition _bottomTextVerticalPosition = RoundedPanelFaster.BarTextVerticalPosition.Middle;

        private GraphicsPath? _cachedPath;
        private Rectangle _cachedRect;
        private Size _cachedSize = Size.Empty;
        private int _cachedRadius = -1;
        private bool _syncingBackColor;
        private int _stackHeight = 108;
        private int _collapsedHeight = 36;
        private int _gapTop;
        private int _gapBottom = 4;
        private bool _startsCollapsed;
        private AdaptiveStackRowSpringMode _stackSpringMode = AdaptiveStackRowSpringMode.False;

        public RoundedAdaptiveStackPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);

            DoubleBuffered = true;
            ResizeRedraw = false;
            Padding = new Padding(7);
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            SyncBackColorFromInnerColor();
        }

        [Category("Adaptive Stack")]
        [Description("Plain-data item rules for this rounded adaptive stack. RowType chooses ARP or nested RAS.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(RoundedAdaptiveStackRowRuleCollectionEditor), typeof(CollectionEditor))]
        public new AdaptiveStackRowRuleCollection RowRules
        {
            get
            {
                EnsureRoundedRowRules();
                return base.RowRules;
            }
        }

        internal override AdaptiveStackRowRule NormalizeRowRuleForCollection(AdaptiveStackRowRule rule)
        {
            return rule is RoundedAdaptiveStackRowRule
                ? rule
                : CreateRoundedRowRule(rule);
        }

        [Category("Appearance")]
        [DefaultValue(15)]
        [Description("Radius of rounded corners.")]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                int newValue = Math.Max(0, value);
                if (_cornerRadius == newValue)
                    return;

                _cornerRadius = newValue;
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(1)]
        [Description("Thickness of the border.")]
        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                int newValue = Math.Max(0, value);
                if (_borderThickness == newValue)
                    return;

                _borderThickness = newValue;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Background color inside the rounded panel.")]
        public Color InnerColor
        {
            get => _innerColor;
            set
            {
                if (_innerColor == value)
                    return;

                _innerColor = value;
                SyncBackColorFromInnerColor();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Color of the border.")]
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                if (_borderColor == value)
                    return;

                _borderColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Toggle selection border effect.")]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("The color of the selection glow below the top separator.")]
        public Color SelectionEffectColor
        {
            get => _selectionEffectColor;
            set
            {
                if (_selectionEffectColor == value)
                    return;

                _selectionEffectColor = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [DefaultValue(false)]
        [Description("Enable hover effects on this panel.")]
        public bool EnableHoverEffects
        {
            get => _enableHoverEffects;
            set
            {
                if (_enableHoverEffects == value)
                    return;

                _enableHoverEffects = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [Description("Border color when mouse hovers over the panel.")]
        public Color HoverBorderColor
        {
            get => _hoverBorderColor;
            set
            {
                if (_hoverBorderColor == value)
                    return;

                _hoverBorderColor = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [DefaultValue(2)]
        [Description("Border thickness when mouse hovers over the panel.")]
        public int HoverBorderThickness
        {
            get => _hoverBorderThickness;
            set
            {
                int newValue = Math.Max(0, value);
                if (_hoverBorderThickness == newValue)
                    return;

                _hoverBorderThickness = newValue;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [Description("Border color when panel is selected.")]
        public Color SelectedBorderColor
        {
            get => _selectedBorderColor;
            set
            {
                if (_selectedBorderColor == value)
                    return;

                _selectedBorderColor = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [DefaultValue(2)]
        [Description("Border thickness when panel is selected.")]
        public int SelectedBorderThickness
        {
            get => _selectedBorderThickness;
            set
            {
                int newValue = Math.Max(0, value);
                if (_selectedBorderThickness == newValue)
                    return;

                _selectedBorderThickness = newValue;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue(36)]
        [Description("Height of the top bar in pixels.")]
        public int TopBarHeight
        {
            get => _topBarHeight;
            set
            {
                int newValue = Math.Max(0, value);
                if (_topBarHeight == newValue)
                    return;

                _topBarHeight = newValue;
                PerformStackLayout();
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue(false)]
        [Description("Show or hide the top bar.")]
        public bool TopBarVisible
        {
            get => _topBarVisible;
            set
            {
                if (_topBarVisible == value)
                    return;

                _topBarVisible = value;
                PerformStackLayout();
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [Description("Background color of the top bar.")]
        public Color TopBarColor
        {
            get => _topBarColor;
            set
            {
                if (_topBarColor == value)
                    return;

                _topBarColor = value;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue("")]
        [Description("Text displayed in the top bar.")]
        public string TopText
        {
            get => _topText;
            set
            {
                string newValue = value ?? string.Empty;
                if (_topText == newValue)
                    return;

                bool chromeChanged = string.IsNullOrEmpty(_topText) != string.IsNullOrEmpty(newValue);
                _topText = newValue;
                if (chromeChanged)
                    PerformStackLayout();

                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue(0)]
        [Description("Horizontal offset for top bar text.")]
        public int TopTextOffsetX
        {
            get => _topTextOffsetX;
            set
            {
                if (_topTextOffsetX == value)
                    return;

                _topTextOffsetX = value;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue(0)]
        [Description("Maximum top text characters before truncation. Use 0 for no truncation.")]
        public int TopTextCharLimit
        {
            get => _topTextCharLimit;
            set
            {
                int newValue = Math.Max(0, value);
                if (_topTextCharLimit == newValue)
                    return;

                _topTextCharLimit = newValue;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue(RoundedPanelFaster.BarTextVerticalPosition.Middle)]
        [Description("Vertical position of the top text inside the top bar.")]
        public RoundedPanelFaster.BarTextVerticalPosition TopTextVerticalPosition
        {
            get => _topTextVerticalPosition;
            set
            {
                if (_topTextVerticalPosition == value)
                    return;

                _topTextVerticalPosition = value;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [Description("Font for top bar text.")]
        public Font TopFont
        {
            get => _topFont;
            set
            {
                if (_topFont == value)
                    return;

                _topFont = value ?? Font;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [Description("Color of top bar text.")]
        public Color TopTextColor
        {
            get => _topTextColor;
            set
            {
                if (_topTextColor == value)
                    return;

                _topTextColor = value;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [Description("Color of disabled top bar text.")]
        public Color TopTextColorDisabled
        {
            get => _topTextColorDisabled;
            set
            {
                if (_topTextColorDisabled == value)
                    return;

                _topTextColorDisabled = value;
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [DefaultValue(false)]
        [Description("Show separator line at bottom of the top bar.")]
        public bool TopSeparatorVisible
        {
            get => _topSeparatorVisible;
            set
            {
                if (_topSeparatorVisible == value)
                    return;

                _topSeparatorVisible = value;
                PerformStackLayout();
                Invalidate();
            }
        }

        [Category("Bars Top ")]
        [Description("Color of top bar separator line.")]
        public Color TopSeparatorColor
        {
            get => _topSeparatorColor;
            set
            {
                if (_topSeparatorColor == value)
                    return;

                _topSeparatorColor = value;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue(36)]
        [Description("Height of the bottom bar in pixels.")]
        public int BottomBarHeight
        {
            get => _bottomBarHeight;
            set
            {
                int newValue = Math.Max(0, value);
                if (_bottomBarHeight == newValue)
                    return;

                _bottomBarHeight = newValue;
                PerformStackLayout();
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue(false)]
        [Description("Show or hide the bottom bar.")]
        public bool BottomBarVisible
        {
            get => _bottomBarVisible;
            set
            {
                if (_bottomBarVisible == value)
                    return;

                _bottomBarVisible = value;
                PerformStackLayout();
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [Description("Background color of the bottom bar.")]
        public Color BottomBarColor
        {
            get => _bottomBarColor;
            set
            {
                if (_bottomBarColor == value)
                    return;

                _bottomBarColor = value;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue("")]
        [Description("Text displayed in the bottom bar.")]
        public string BottomText
        {
            get => _bottomText;
            set
            {
                string newValue = value ?? string.Empty;
                if (_bottomText == newValue)
                    return;

                bool chromeChanged = string.IsNullOrEmpty(_bottomText) != string.IsNullOrEmpty(newValue);
                _bottomText = newValue;
                if (chromeChanged)
                    PerformStackLayout();

                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue(0)]
        [Description("Horizontal offset for bottom bar text.")]
        public int BottomTextOffsetX
        {
            get => _bottomTextOffsetX;
            set
            {
                if (_bottomTextOffsetX == value)
                    return;

                _bottomTextOffsetX = value;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue(0)]
        [Description("Maximum bottom text characters before truncation. Use 0 for no truncation.")]
        public int BottomTextCharLimit
        {
            get => _bottomTextCharLimit;
            set
            {
                int newValue = Math.Max(0, value);
                if (_bottomTextCharLimit == newValue)
                    return;

                _bottomTextCharLimit = newValue;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue(RoundedPanelFaster.BarTextVerticalPosition.Middle)]
        [Description("Vertical position of the bottom text inside the bottom bar.")]
        public RoundedPanelFaster.BarTextVerticalPosition BottomTextVerticalPosition
        {
            get => _bottomTextVerticalPosition;
            set
            {
                if (_bottomTextVerticalPosition == value)
                    return;

                _bottomTextVerticalPosition = value;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [Description("Font for bottom bar text.")]
        public Font BottomFont
        {
            get => _bottomFont;
            set
            {
                if (_bottomFont == value)
                    return;

                _bottomFont = value ?? Font;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [Description("Color of bottom bar text.")]
        public Color BottomTextColor
        {
            get => _bottomTextColor;
            set
            {
                if (_bottomTextColor == value)
                    return;

                _bottomTextColor = value;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [Description("Color of disabled bottom bar text.")]
        public Color BottomTextColorDisabled
        {
            get => _bottomTextColorDisabled;
            set
            {
                if (_bottomTextColorDisabled == value)
                    return;

                _bottomTextColorDisabled = value;
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [DefaultValue(false)]
        [Description("Show separator line at top of bottom bar.")]
        public bool BottomSeparatorVisible
        {
            get => _bottomSeparatorVisible;
            set
            {
                if (_bottomSeparatorVisible == value)
                    return;

                _bottomSeparatorVisible = value;
                PerformStackLayout();
                Invalidate();
            }
        }

        [Category("Bars Bottom ")]
        [Description("Color of bottom bar separator line.")]
        public Color BottomSeparatorColor
        {
            get => _bottomSeparatorColor;
            set
            {
                if (_bottomSeparatorColor == value)
                    return;

                _bottomSeparatorColor = value;
                Invalidate();
            }
        }

        [Category("Adaptive Stack Item")]
        [DefaultValue(108)]
        [Description("Authored height when this rounded stack is hosted directly by another rounded adaptive stack.")]
        public int StackHeight
        {
            get => _stackHeight;
            set
            {
                int newValue = Math.Max(1, value);
                if (_stackHeight == newValue)
                    return;

                _stackHeight = newValue;
                NotifyParentStackLayout();
            }
        }

        [Category("Adaptive Stack Item")]
        [DefaultValue(36)]
        [Description("Height used when this rounded stack is collapsed inside another rounded adaptive stack.")]
        public int CollapsedHeight
        {
            get => _collapsedHeight;
            set
            {
                int newValue = Math.Max(0, value);
                if (_collapsedHeight == newValue)
                    return;

                _collapsedHeight = newValue;
                NotifyParentStackLayout();
            }
        }

        [Category("Adaptive Stack Item")]
        [DefaultValue(0)]
        [Description("Space above this rounded stack when hosted directly by another rounded adaptive stack.")]
        public int GapTop
        {
            get => _gapTop;
            set
            {
                int newValue = Math.Max(0, value);
                if (_gapTop == newValue)
                    return;

                _gapTop = newValue;
                NotifyParentStackLayout();
            }
        }

        [Category("Adaptive Stack Item")]
        [DefaultValue(4)]
        [Description("Space below this rounded stack when hosted directly by another rounded adaptive stack.")]
        public int GapBottom
        {
            get => _gapBottom;
            set
            {
                int newValue = Math.Max(0, value);
                if (_gapBottom == newValue)
                    return;

                _gapBottom = newValue;
                NotifyParentStackLayout();
            }
        }

        [Category("Adaptive Stack Item")]
        [DefaultValue(false)]
        [Description("When true, this rounded stack starts collapsed inside another rounded adaptive stack.")]
        public bool StartsCollapsed
        {
            get => _startsCollapsed;
            set
            {
                if (_startsCollapsed == value)
                    return;

                _startsCollapsed = value;
                NotifyParentStackLayout();
            }
        }

        [Category("Adaptive Stack Item")]
        [DefaultValue(AdaptiveStackRowSpringMode.False)]
        [Description("Controls whether this rounded stack absorbs available vertical space in its parent rounded adaptive stack.")]
        public AdaptiveStackRowSpringMode StackSpringMode
        {
            get => _stackSpringMode;
            set
            {
                if (_stackSpringMode == value)
                    return;

                _stackSpringMode = value;
                NotifyParentStackLayout();
            }
        }

        protected override bool IsStackLayoutItem(Control control)
        {
            return control is AdaptiveStackRowPanel
                || (control is RoundedAdaptiveStackPanel stack && !ReferenceEquals(stack, this));
        }

        protected override Padding GetEffectiveLayoutPadding()
        {
            Padding padding = Padding;
            return new Padding(
                padding.Left,
                padding.Top + GetTopChromeHeight(),
                padding.Right,
                padding.Bottom + GetBottomChromeHeight());
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);

            if (_syncingBackColor || BackColor == Color.Transparent || _innerColor == BackColor)
                return;

            _innerColor = BackColor;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!_enableHoverEffects || _isSelected)
                return;

            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_enableHoverEffects)
                return;

            _isHovered = false;
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateGeometryCache();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The full background is painted in OnPaint so the rounded corners can show the parent color.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0)
                return;

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            EnsureGeometry();
            if (_cachedPath == null)
                return;

            Color parentBackColor = Parent?.BackColor ?? SystemColors.Control;
            using (var cornerBrush = new SolidBrush(parentBackColor))
            {
                graphics.FillRectangle(cornerBrush, ClientRectangle);
            }

            using (var innerBrush = new SolidBrush(_innerColor))
            {
                graphics.FillPath(innerBrush, _cachedPath);
            }

            using (Region oldClip = graphics.Clip.Clone())
            {
                graphics.SetClip(_cachedPath);
                DrawBars(graphics);
                graphics.SetClip(oldClip, CombineMode.Replace);
            }

            Color actualBorderColor = _borderColor;
            int actualBorderThickness = _borderThickness;

            if (_isSelected)
            {
                actualBorderColor = _selectedBorderColor;
                actualBorderThickness = _selectedBorderThickness;
            }
            else if (_isHovered && _enableHoverEffects)
            {
                actualBorderColor = _hoverBorderColor;
                actualBorderThickness = _hoverBorderThickness;
            }

            if (actualBorderThickness > 0)
            {
                using GraphicsPath borderPath = CreateBorderPath(actualBorderThickness);
                using var borderPen = new Pen(actualBorderColor, actualBorderThickness)
                {
                    Alignment = PenAlignment.Center,
                    LineJoin = LineJoin.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                graphics.DrawPath(borderPen, borderPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cachedPath?.Dispose();
                _topFont.Dispose();
                _bottomFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private int GetTopChromeHeight()
        {
            return _topBarHeight > 0 && (_topBarVisible || _topSeparatorVisible || !string.IsNullOrEmpty(_topText))
                ? _topBarHeight
                : 0;
        }

        private int GetBottomChromeHeight()
        {
            return _bottomBarHeight > 0 && (_bottomBarVisible || _bottomSeparatorVisible || !string.IsNullOrEmpty(_bottomText))
                ? _bottomBarHeight
                : 0;
        }

        private void DrawBars(Graphics graphics)
        {
            Rectangle inner = Rectangle.Inflate(_cachedRect, -_borderThickness, -_borderThickness);
            int topChromeHeight = GetTopChromeHeight();
            int bottomChromeHeight = GetBottomChromeHeight();

            if (topChromeHeight > 0)
            {
                Rectangle topRect = new(inner.X, inner.Y, inner.Width, Math.Min(topChromeHeight, inner.Height));
                if (_topBarVisible)
                {
                    using var topBrush = new SolidBrush(_topBarColor);
                    graphics.FillRectangle(topBrush, topRect);
                }

                if (_topSeparatorVisible)
                {
                    int separatorY = topRect.Bottom - 1;
                    using var separatorPen = new Pen(_topSeparatorColor, 1);
                    graphics.DrawLine(separatorPen, topRect.Left, separatorY, topRect.Right, separatorY);

                    if (_isSelected)
                    {
                        using var glowPen1 = new Pen(_selectionEffectColor, 1);
                        using var glowPen2 = new Pen(Color.FromArgb(_selectionEffectColor.A / 2, _selectionEffectColor), 1);
                        graphics.DrawLine(glowPen1, topRect.Left, separatorY + 1, topRect.Right, separatorY + 1);
                        graphics.DrawLine(glowPen2, topRect.Left, separatorY + 2, topRect.Right, separatorY + 2);
                    }
                }

                DrawBarText(
                    graphics,
                    _topText,
                    _topTextCharLimit,
                    _topFont,
                    Enabled ? _topTextColor : _topTextColorDisabled,
                    topRect,
                    _topTextOffsetX,
                    _topTextVerticalPosition);
            }

            if (bottomChromeHeight > 0)
            {
                Rectangle bottomRect = new(
                    inner.X,
                    inner.Bottom - Math.Min(bottomChromeHeight, inner.Height),
                    inner.Width,
                    Math.Min(bottomChromeHeight, inner.Height));

                if (_bottomBarVisible)
                {
                    using var bottomBrush = new SolidBrush(_bottomBarColor);
                    graphics.FillRectangle(bottomBrush, bottomRect);
                }

                if (_bottomSeparatorVisible)
                {
                    using var separatorPen = new Pen(_bottomSeparatorColor, 1);
                    graphics.DrawLine(separatorPen, bottomRect.Left, bottomRect.Top, bottomRect.Right, bottomRect.Top);
                }

                DrawBarText(
                    graphics,
                    _bottomText,
                    _bottomTextCharLimit,
                    _bottomFont,
                    Enabled ? _bottomTextColor : _bottomTextColorDisabled,
                    bottomRect,
                    _bottomTextOffsetX,
                    _bottomTextVerticalPosition);
            }
        }

        private static void DrawBarText(
            Graphics graphics,
            string text,
            int maxChars,
            Font font,
            Color color,
            Rectangle barBounds,
            int offsetX,
            RoundedPanelFaster.BarTextVerticalPosition verticalPosition)
        {
            if (string.IsNullOrEmpty(text) || barBounds.Width <= 0 || barBounds.Height <= 0)
                return;

            string displayText = TruncateText(text, maxChars);
            Rectangle textBounds = new(
                barBounds.Left + offsetX + 9,
                GetTextY(barBounds, font, verticalPosition),
                Math.Max(0, barBounds.Width - offsetX - 18),
                TextRenderer.MeasureText(displayText, font).Height);

            TextRenderer.DrawText(
                graphics,
                displayText,
                font,
                textBounds,
                color,
                TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private static int GetTextY(Rectangle barBounds, Font font, RoundedPanelFaster.BarTextVerticalPosition verticalPosition)
        {
            int textHeight = TextRenderer.MeasureText("Ag", font).Height;
            const int inset = 2;

            if (barBounds.Height <= textHeight)
                return barBounds.Top + Math.Max(0, (barBounds.Height - textHeight) / 2);

            return verticalPosition switch
            {
                RoundedPanelFaster.BarTextVerticalPosition.Top => barBounds.Top + inset,
                RoundedPanelFaster.BarTextVerticalPosition.Bottom => barBounds.Bottom - textHeight - inset,
                _ => barBounds.Top + (barBounds.Height - textHeight) / 2
            };
        }

        private static string TruncateText(string text, int maxChars)
        {
            if (maxChars <= 0 || string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text;

            int takeChars = Math.Max(0, maxChars - 3);
            return text[..takeChars] + "...";
        }

        private void EnsureGeometry()
        {
            if (_cachedPath != null && _cachedSize == Size && _cachedRadius == _cornerRadius)
                return;

            InvalidateGeometryCache();
            _cachedSize = Size;
            _cachedRadius = _cornerRadius;
            _cachedRect = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            _cachedPath = CreateRoundedPath(_cachedRect, _cornerRadius);
        }

        private GraphicsPath CreateBorderPath(int borderThickness)
        {
            float inset = Math.Max(0.5f, borderThickness / 2f);
            RectangleF rect = new(
                inset,
                inset,
                Math.Max(0, Width - borderThickness),
                Math.Max(0, Height - borderThickness));

            return CreateRoundedPath(rect, Math.Max(0, _cornerRadius - (int)Math.Ceiling(inset / 2f)));
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            return CreateRoundedPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        private static GraphicsPath CreateRoundedPath(RectangleF rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            float diameter = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
            if (diameter <= 0f)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InvalidateGeometryCache()
        {
            _cachedPath?.Dispose();
            _cachedPath = null;
        }

        private void SyncBackColorFromInnerColor()
        {
            _syncingBackColor = true;
            try
            {
                base.BackColor = _innerColor;
            }
            finally
            {
                _syncingBackColor = false;
            }
        }

        private void NotifyParentStackLayout()
        {
            if (Parent is AdaptiveStackPanel parentStack && !ReferenceEquals(parentStack, this))
            {
                parentStack.PerformStackLayout();
            }
        }

        private void EnsureRoundedRowRules()
        {
            AdaptiveStackRowRuleCollection rules = base.RowRules;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] is RoundedAdaptiveStackRowRule)
                    continue;

                rules[i] = CreateRoundedRowRule(rules[i]);
            }
        }

        private RoundedAdaptiveStackRowRule CreateRoundedRowRule(AdaptiveStackRowRule source)
        {
            return new RoundedAdaptiveStackRowRule
            {
                RowType = GetHostedRuleType(source),
                RowName = source.RowName,
                RowHeight = source.RowHeight,
                CollapsedHeight = source.CollapsedHeight,
                GapTop = source.GapTop,
                GapBottom = source.GapBottom,
                StartsCollapsed = source.StartsCollapsed,
                StretchChildHeight = source.StretchChildHeight,
                SpringMode = source.SpringMode
            };
        }

        private RoundedAdaptiveStackRowType GetHostedRuleType(AdaptiveStackRowRule rule)
        {
            foreach (Control control in Controls)
            {
                if (control is RoundedAdaptiveStackPanel
                    && !ReferenceEquals(control, this)
                    && string.Equals(control.Name, rule.RowName, System.StringComparison.Ordinal))
                {
                    return RoundedAdaptiveStackRowType.RAS;
                }
            }

            return RoundedAdaptiveStackRowType.ARP;
        }
    }
}
