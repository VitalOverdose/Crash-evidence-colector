// ======================================================================================================
// ROUNDED PANEL FASTER — TRUE TRANSPARENT VERSION with SEPARATOR LINES
// ------------------------------------------------------------------------------------------------------
// Features:
// - Organized properties into categories (Top Bar, Bottom Bar, Appearance)
// - Optional separator lines at bar boundaries (independent of bar visibility)
// - Optimized rendering with caching
// - Fixed resize redraw behavior
// - Hover and Selection border effects
// ------------------------------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ProfessorSnowsVideoDownloader.FormHelperClasses;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public class RoundedPanelFaster : Panel
    {
        public enum BarTextVerticalPosition
        {
            Top,
            Middle,
            Bottom
        }

        private string TruncateText(string text, int maxChars)
        {
            if (maxChars <= 0 || string.IsNullOrEmpty(text))
                return text;

            if (text.Length <= maxChars)
                return text;

            // Ensure we have room for "..."
            int takeChars = Math.Max(0, maxChars - 3);
            return text.Substring(0, takeChars) + "...";
        }

        // ==============================================================================================
        // CONFIGURABLE PROPERTIES 
        // ==============================================================================================

        #region Panel properties

        private int cornerRadius = 20;
        private int borderThickness = 1;
        private Color innerColor = Color.White;
        private Color borderColor = Color.FromArgb(80, 80, 80);
        private Color _selectionEffectColor = Color.FromArgb(128, 128, 255);
        private bool _isSelected = false;


        [Category("Appearance")]
        [Description("Toggle Selection effect")]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [Description("The color of the Selection effect (glow below separator)")]
        public Color SelectionEffectColor
        {
            get { return _selectionEffectColor; }
            set
            {
                if (value != _selectionEffectColor)
                {
                    _selectionEffectColor = value;
                    Invalidate();
                }
            }
        }


        [Category("Appearance")]
        [Description("Radius of rounded corners")]
        public int CornerRadius
        {
            get { return cornerRadius; }
            set
            {
                if (value != cornerRadius && value >= 0)
                {
                    cornerRadius = value;
                    InvalidateGeometryCache();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [Description("Thickness of the border")]
        public int BorderThickness
        {
            get { return borderThickness; }
            set
            {
                if (value != borderThickness && value >= 0)
                {
                    borderThickness = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [Description("Background color of the panel")]
        public Color InnerColor
        {
            get { return innerColor; }
            set
            {
                if (value != innerColor)
                {
                    innerColor = value;
                    base.BackColor = value; // keep BackColor in sync so child controls see the right parent color
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [Description("Color of the border")]
        public Color BorderColor
        {
            get { return borderColor; }
            set
            {
                if (value != borderColor)
                {
                    borderColor = value;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Hover and Selection Effects

        private bool _isHovered = false;
        private bool _enableHoverEffects = false;
        private Color _hoverBorderColor = Color.FromArgb(100, 149, 237); // Cornflower blue
        private int _hoverBorderThickness = 2;
        private Color _selectedBorderColor = Color.Blue;
        private int _selectedBorderThickness = 2;

        [Category("Hover Effects")]
        [Description("Enable hover effects on this panel")]
        [DefaultValue(false)]
        public bool EnableHoverEffects
        {
            get => _enableHoverEffects;
            set
            {
                _enableHoverEffects = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [Description("Border color when mouse hovers over the panel")]
        public Color HoverBorderColor
        {
            get => _hoverBorderColor;
            set
            {
                _hoverBorderColor = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [Description("Border thickness when mouse hovers over the panel")]
        [DefaultValue(2)]
        public int HoverBorderThickness
        {
            get => _hoverBorderThickness;
            set
            {
                _hoverBorderThickness = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [Description("Border color when panel is selected")]
        public Color SelectedBorderColor
        {
            get => _selectedBorderColor;
            set
            {
                _selectedBorderColor = value;
                Invalidate();
            }
        }

        [Category("Hover Effects")]
        [Description("Border thickness when panel is selected")]
        [DefaultValue(2)]
        public int SelectedBorderThickness
        {
            get => _selectedBorderThickness;
            set
            {
                _selectedBorderThickness = value;
                Invalidate();
            }
        }

        #endregion

        #region Properties Top  

        private int topBarHeight = 36;
        private int topTextCharLimit = 0;
        private int topTextOffsetX = 0;
        private int topTextRightMargin = 0;
        private bool topSeparatorVisible = false;
        private bool topBarVisible = false;
        private Font topFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        private Color topTextColorDisabled = Color.FromArgb(160, 160, 160);
        private Color topSeparatorColor = Color.DarkGray;
        private Color topTextColor = Color.FromArgb(31, 31, 31);
        private Color topBarColor = Color.FromArgb(230, 235, 240);
        private string topText = string.Empty;
        private int topTextOffsetY = 8;


        [Category("Bars Top ")]
        [Description("Height of the top bar in pixels")]
        public int TopBarHeight
        {
            get { return topBarHeight; }
            set
            {
                if (value != topBarHeight && value >= 0)
                {
                    topBarHeight = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Show or hide the top bar")]
        public bool TopBarVisible
        {
            get { return topBarVisible; }
            set
            {
                if (value != topBarVisible)
                {
                    topBarVisible = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Background color of the top bar")]
        public Color TopBarColor
        {
            get { return topBarColor; }
            set
            {
                if (value != topBarColor)
                {
                    topBarColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Text displayed in the top bar")]
        public string TopText
        {
            get { return topText; }
            set
            {
                if (value != topText)
                {
                    topText = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Horizontal offset for top bar text")]
        public int TopTextOffsetX
        {
            get { return topTextOffsetX; }
            set
            {
                if (value != topTextOffsetX)
                {
                    topTextOffsetX = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Reserves space on the right of the top bar so the text can't run under a corner button (e.g. a close X). Text ellipsizes to fit. 0 = no limit (unchanged behaviour).")]
        [DefaultValue(0)]
        public int TopTextRightMargin
        {
            get { return topTextRightMargin; }
            set
            {
                if (value != topTextRightMargin)
                {
                    topTextRightMargin = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Vertical offset (px) of the top text, measured from the top of the panel.")]
        [DefaultValue(8)]
        public int TopTextOffsetY
        {
            get { return topTextOffsetY; }
            set
            {
                if (value != topTextOffsetY)
                {
                    topTextOffsetY = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Font for top bar text")]
        public Font TopFont
        {
            get { return topFont; }
            set
            {
                if (value != topFont)
                {
                    topFont = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Color of top bar text")]
        public Color TopTextColor
        {
            get { return topTextColor; }
            set
            {
                if (value != topTextColor)
                {
                    topTextColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Show separator line at bottom of top bar")]
        public bool TopSeparatorVisible
        {
            get { return topSeparatorVisible; }
            set
            {
                if (value != topSeparatorVisible)
                {
                    topSeparatorVisible = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Color of top bar separator line")]
        public Color TopSeparatorColor
        {
            get { return topSeparatorColor; }
            set
            {
                if (value != topSeparatorColor)
                {
                    topSeparatorColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Maximum characters for top text (0 = no limit). Text will be truncated with '...'")]
        public int TopTextCharLimit
        {
            get { return topTextCharLimit; }
            set
            {
                if (value != topTextCharLimit && value >= 0)
                {
                    topTextCharLimit = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        #endregion Properties Top  

        #region Properties Bottom

        private int bottomTextCharLimit = 0;
        private int bottomTextOffsetX = 0;
        private int bottomTextRightMargin = 0;
        private int bottomBarHeight = 36;
        private bool bottomSeparatorVisible = false;
        private bool bottomBarVisible = false;
        private Font bottomFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        private Color bottomTextColor = Color.FromArgb(31, 31, 31);
        private Color bottomBarColor = Color.FromArgb(230, 235, 240);
        private Color bottomSeparatorColor = Color.DarkGray;
        private Color bottomTextColorDisabled = Color.FromArgb(160, 160, 160);
        private string bottomText = string.Empty;
        private int bottomTextOffsetY = 8;


        [Category("Bars Bottom ")]
        [Description("Maximum characters for bottom text (0 = no limit). Text will be truncated with '...'")]
        public int BottomTextCharLimit
        {
            get { return bottomTextCharLimit; }
            set
            {
                if (value != bottomTextCharLimit && value >= 0)
                {
                    bottomTextCharLimit = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Top ")]
        [Description("Text color used when the control is disabled (Top Bar)")]
        public Color TopTextColorDisabled
        {
            get { return topTextColorDisabled; }
            set
            {
                if (value != topTextColorDisabled)
                {
                    topTextColorDisabled = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Text color used when the control is disabled (Bottom Bar)")]
        public Color BottomTextColorDisabled
        {
            get { return bottomTextColorDisabled; }
            set
            {
                if (value != bottomTextColorDisabled)
                {
                    bottomTextColorDisabled = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Height of the bottom bar in pixels")]
        public int BottomBarHeight
        {
            get { return bottomBarHeight; }
            set
            {
                if (value != bottomBarHeight && value >= 0)
                {
                    bottomBarHeight = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Show or hide the bottom bar")]
        public bool BottomBarVisible
        {
            get { return bottomBarVisible; }
            set
            {
                if (value != bottomBarVisible)
                {
                    bottomBarVisible = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Background color of the bottom bar")]
        public Color BottomBarColor
        {
            get { return bottomBarColor; }
            set
            {
                if (value != bottomBarColor)
                {
                    bottomBarColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Text displayed in the bottom bar")]
        public string BottomText
        {
            get { return bottomText; }
            set
            {
                if (value != bottomText)
                {
                    bottomText = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Horizontal offset for bottom bar text")]
        public int BottomTextOffsetX
        {
            get { return bottomTextOffsetX; }
            set
            {
                if (value != bottomTextOffsetX)
                {
                    bottomTextOffsetX = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Reserves space on the right of the bottom bar so the text can't run under a corner button. Text ellipsizes to fit. 0 = no limit (unchanged behaviour).")]
        [DefaultValue(0)]
        public int BottomTextRightMargin
        {
            get { return bottomTextRightMargin; }
            set
            {
                if (value != bottomTextRightMargin)
                {
                    bottomTextRightMargin = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Vertical offset (px) of the bottom text, measured up from the bottom of the panel.")]
        [DefaultValue(8)]
        public int BottomTextOffsetY
        {
            get { return bottomTextOffsetY; }
            set
            {
                if (value != bottomTextOffsetY)
                {
                    bottomTextOffsetY = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Font for bottom bar text")]
        public Font BottomFont
        {
            get { return bottomFont; }
            set
            {
                if (value != bottomFont)
                {
                    bottomFont = value;
                    InvalidateTextCache();
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Color of bottom bar text")]
        public Color BottomTextColor
        {
            get { return bottomTextColor; }
            set
            {
                if (value != bottomTextColor)
                {
                    bottomTextColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Show separator line at top of bottom bar")]
        public bool BottomSeparatorVisible
        {
            get { return bottomSeparatorVisible; }
            set
            {
                if (value != bottomSeparatorVisible)
                {
                    bottomSeparatorVisible = value;
                    Invalidate();
                }
            }
        }

        [Category("Bars Bottom ")]
        [Description("Color of bottom bar separator line")]
        public Color BottomSeparatorColor
        {
            get { return bottomSeparatorColor; }
            set
            {
                if (value != bottomSeparatorColor)
                {
                    bottomSeparatorColor = value;
                    Invalidate();
                }
            }
        }

        #endregion Properties Bottom

        #region PRIVATE CACHED OBJECTS

        private GraphicsPath cachedPath = null!;
        private Rectangle cachedRect;
        private Size lastSize = Size.Empty;
        private int lastRadius;

        // Cached text positions and sizes
        private Point topTextPos = Point.Empty;
        private Size topTextSize = Size.Empty;
        private Point bottomTextPos = Point.Empty;
        private Size bottomTextSize = Size.Empty;

        // Track last known values to detect when recalculation is needed
        private string lastTopText = string.Empty;
        private string lastBottomText = string.Empty;
        private int lastTopOffset = 0;
        private int lastBottomOffset = 0;
        private Font? lastTopFont = null;
        private Font? lastBottomFont = null;
        private int lastTopOffsetY = -1;
        private int lastBottomOffsetY = -1;

        #endregion

        // ==============================================================================================
        // CONSTRUCTOR
        // ==============================================================================================

        public RoundedPanelFaster()
        {
            BackColor = Color.Transparent;

            // Performance flags
            // EXPERIMENT: force all painting through this control's buffered OnPaint path and
            // suppress separate background erase painting. The layout logs show OnPaint can be
            // called 100+ times during resize/DPI while OnInvalidated stays low, so the problem is
            // repeated OS-driven painting rather than our cached geometry being rebuilt.
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;

            // EXPERIMENT: this used to be true, but RoundedPanelFaster already invalidates itself
            // in OnSizeChanged after refreshing the cached rounded geometry. Leaving ResizeRedraw
            // enabled gives WinForms a second automatic repaint path during live resize / DPI moves,
            // which is exactly where the file browser BasePanel is currently over-painting.
            // this.ResizeRedraw = true;
            this.ResizeRedraw = false;

            TopFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            BottomFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        }

        // ==============================================================================================
        // MOUSE HOVER EVENTS
        // ==============================================================================================

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (_enableHoverEffects && !_isSelected)
            {
                _isHovered = true;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_enableHoverEffects)
            {
                _isHovered = false;
                Invalidate();
            }
        }

        /// <summary>
        /// EVENT: OnSizeChanged (with immediate geometry rebuild)
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutStormDiagnostics.Hit(DiagnosticName, "OnSizeChanged");

            if (this.Size != lastSize)
            {
                InvalidateGeometryCache();  // rebuild rounded edges
                InvalidateTextCache();      // force text to recalc on next paint
                lastSize = this.Size;

                Invalidate();
            }
        }

        /// <summary>
        /// OnPaint
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            LayoutStormDiagnostics.Hit(DiagnosticName, "OnPaint");

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Without this the arcs rasterise half a pixel from where the geometry puts them,
            // which is half of why the corners looked soft.
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (cachedPath == null || CornerRadius != lastRadius)
            {
                BuildCachedGeometry();
                lastRadius = CornerRadius;
            }

            // Fill corners with parent background to fake transparency without cascade
            Color parentBack = Parent?.BackColor ?? SystemColors.Control;
            using (SolidBrush cornerBrush = new SolidBrush(parentBack))
                g.FillRectangle(cornerBrush, 0, 0, Width, Height);

            // --- Fill rounded shape ---
            using (SolidBrush innerBrush = new SolidBrush(InnerColor))
                g.FillPath(innerBrush, cachedPath!);

            // Bar FILLS are drawn as their own rounded paths and must stay OUTSIDE the clip:
            // a GDI+ clip region is a 1-bit mask, so filling an anti-aliased path inside one
            // just re-cuts it with a hard edge — the very artefact this replaces.
            DrawBarFills(g);

            // Bar CONTENT (text, separators, selection glow) keeps the clip exactly as before.
            // It is drawn with GDI text and straight lines, where a hard edge costs nothing and
            // the clip still usefully stops a long label escaping the rounded shape.
            Region oldClip = g.Clip;
            g.SetClip(cachedPath!);
            DrawBars(g);
            g.Clip = oldClip;

            // --- Draw border on top with hover/selection effects ---
            Color actualBorderColor = BorderColor;
            int actualBorderThickness = BorderThickness;

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

            // Stroke a path inset by HALF the pen width, not the fill path itself. A pen
            // straddles its path: stroking the fill's own edge leaves the fill's AA fringe and
            // the stroke's AA fringe each blending to the PARENT colour, and the backdrop
            // survives between them as a pale ring just inside the border — the halo. Inset by
            // half, the pen's outer half covers that fringe and its inner half sits on solid
            // fill, so there is no uncovered ring left.
            if (actualBorderThickness > 0)
            {
                float inset = actualBorderThickness / 2f;
                RectangleF strokeBounds = RectangleF.Inflate(cachedRect, -inset, -inset);
                float strokeRadius = Math.Max(0f, Math.Max(1, CornerRadius) - inset);

                using (GraphicsPath borderPath = RoundedPathF(strokeBounds, strokeRadius))
                using (Pen borderPen = new Pen(actualBorderColor, actualBorderThickness))
                    g.DrawPath(borderPen, borderPath);
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutStormDiagnostics.Hit(DiagnosticName, $"OnLayout:{levent.AffectedProperty ?? "unknown"}");
        }

        private static readonly System.Diagnostics.Stopwatch _stackThrottle = System.Diagnostics.Stopwatch.StartNew();

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            LayoutStormDiagnostics.Hit(DiagnosticName, "OnInvalidated");

            if (_stackThrottle.ElapsedMilliseconds > 2000)
            {
                _stackThrottle.Restart();
                var stack = new System.Diagnostics.StackTrace();
                var frames = Enumerable.Range(1, Math.Min(8, stack.FrameCount - 1))
                                       .Select(i => stack.GetFrame(i)?.GetMethod()?.Name ?? "?");
                System.Diagnostics.Debug.WriteLine($"[Invalidated] {DiagnosticName} <- {string.Join(" <- ", frames)}");
            }
        }

        private string DiagnosticName => LayoutStormDiagnostics.NameOf(this);


        /// <summary>
        /// DRAW BARS, TEXT, AND SEPARATORS (UPDATED - Separators now independent of bar visibility)
        /// </summary>
        /// <param name="g"></param>
        private void DrawBars(Graphics g)
        {
            // Compute inner drawing area (inside the border)
            Rectangle inner = Rectangle.Inflate(cachedRect, -BorderThickness, -BorderThickness);
            // --- Update text position cache (only recalculates when needed) ---
            UpdateTextCache(inner);

            // (Bar FILLS moved to DrawBarFills — see OnPaint.)

            // Draw top separator line (independent of bar visibility)
            if (TopSeparatorVisible && TopBarHeight > 0)
            {
                int sepY = inner.Y + TopBarHeight - 1;
                using (Pen sepPen = new Pen(TopSeparatorColor, 1))
                {
                    g.DrawLine(sepPen, inner.X, sepY, inner.Right, sepY);
                }

                // Draw selection glow effect below separator
                if (IsSelected)
                {
                    using (Pen glowPen1 = new Pen(_selectionEffectColor, 1))
                    using (Pen glowPen2 = new Pen(Color.FromArgb(_selectionEffectColor.A / 2, _selectionEffectColor), 1))
                    {
                        g.DrawLine(glowPen1, inner.X, sepY + 1, inner.Right, sepY + 1);
                        g.DrawLine(glowPen2, inner.X, sepY + 2, inner.Right, sepY + 2);
                    }
                }
            }

            // ✅ Draw top text (regardless of bar visibility)
            if (!string.IsNullOrEmpty(TopText))
            {
                string displayText = TruncateText(TopText, TopTextCharLimit);
                Color drawTopColor = this.Enabled ? TopTextColor : TopTextColorDisabled;
                if (topTextRightMargin > 0)
                {
                    // Reserve space on the right (e.g. for a corner close button) and ellipsize to fit.
                    int availWidth = Math.Max(0, inner.Right - topTextRightMargin - topTextPos.X);
                    Rectangle topTextRect = new Rectangle(topTextPos.X, topTextPos.Y, availWidth, topTextSize.Height + 2);
                    TextRenderer.DrawText(
                        g,
                        displayText,
                        TopFont,
                        topTextRect,
                        drawTopColor,
                        TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    TextRenderer.DrawText(
                        g,
                        displayText,
                        TopFont,
                        topTextPos,
                        drawTopColor,
                        TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                }
            }

            // Draw bottom separator line (independent of bar visibility)
            if (BottomSeparatorVisible && BottomBarHeight > 0)
            {
                int sepY = inner.Bottom - BottomBarHeight;
                using (Pen sepPen = new Pen(BottomSeparatorColor, 1))
                {
                    g.DrawLine(sepPen, inner.X, sepY, inner.Right, sepY);
                }
            }

            // Draw bottom text.
            if (!string.IsNullOrEmpty(BottomText))
            {
                string displayText = TruncateText(BottomText, BottomTextCharLimit);
                Color drawBottomColor = this.Enabled ? BottomTextColor : BottomTextColorDisabled;
                if (bottomTextRightMargin > 0)
                {
                    int availWidth = Math.Max(0, inner.Right - bottomTextRightMargin - bottomTextPos.X);
                    Rectangle bottomTextRect = new Rectangle(bottomTextPos.X, bottomTextPos.Y, availWidth, bottomTextSize.Height + 2);
                    TextRenderer.DrawText(g, displayText, BottomFont, bottomTextRect, drawBottomColor,
                        TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    TextRenderer.DrawText(g, displayText, BottomFont, bottomTextPos, drawBottomColor,
                        TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                }
            }
        }

        /// <summary>
        /// A lot of maths are chached so we dont have to recalculate on every redraw.
        /// </summary>
        /// <summary>
        /// Bar backgrounds, drawn as their own paths: rounded on the outside edge, square where
        /// they meet the panel face. Previously these were square rectangles trimmed by a clip
        /// region, which is a hard 1-bit mask — so a smooth panel body met visibly stair-stepped
        /// bar corners. Identical geometry to RoundedBarPanelTest, which is where this was
        /// proven side by side.
        /// </summary>
        private void DrawBarFills(Graphics g)
        {
            Rectangle inner = Rectangle.Inflate(cachedRect, -BorderThickness, -BorderThickness);
            if (inner.Width <= 0 || inner.Height <= 0)
                return;

            // The bars sit INSIDE the border, so their curve is tighter than the panel's by
            // exactly the border thickness — matching what the eye expects from concentric
            // rounded shapes.
            float barRadius = Math.Max(0f, Math.Max(1, CornerRadius) - BorderThickness);

            if (TopBarVisible && TopBarHeight > 0)
            {
                float h = Math.Min(TopBarHeight, inner.Height);
                using (GraphicsPath bar = HalfRoundedPathF(
                           new RectangleF(inner.X, inner.Y, inner.Width, h), barRadius, topRounded: true))
                using (SolidBrush barBrush = new SolidBrush(TopBarColor))
                    g.FillPath(barBrush, bar);
            }

            if (BottomBarVisible && BottomBarHeight > 0)
            {
                float h = Math.Min(BottomBarHeight, inner.Height);
                using (GraphicsPath bar = HalfRoundedPathF(
                           new RectangleF(inner.X, inner.Bottom - h, inner.Width, h), barRadius, topRounded: false))
                using (SolidBrush barBrush = new SolidBrush(BottomBarColor))
                    g.FillPath(barBrush, bar);
            }
        }

        /// <summary>Float-precision rounded rect. The cached int path stays as it was.</summary>
        private static GraphicsPath RoundedPathF(RectangleF bounds, float radius)
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

        /// <summary>Rounded at one end, square at the other — the shape a bar actually is.</summary>
        private static GraphicsPath HalfRoundedPathF(RectangleF bounds, float radius, bool topRounded)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            float diameter = Math.Min(Math.Max(1f, radius * 2f), Math.Min(bounds.Width, bounds.Height));

            path.StartFigure();
            if (topRounded)
            {
                path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                path.AddLine(bounds.Right, bounds.Bottom, bounds.X, bounds.Bottom);
            }
            else
            {
                path.AddLine(bounds.X, bounds.Y, bounds.Right, bounds.Y);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            }
            path.CloseFigure();
            return path;
        }

        private void BuildCachedGeometry()
        {
            InvalidateGeometryCache();
            cachedRect = new Rectangle(0, 0, Width - 1, Height - 1);

            int radius = CornerRadius;
            if (radius < 1) radius = 1;

            cachedPath = new GraphicsPath();
            int d = radius * 2;

            // Rounded rectangle path
            cachedPath.AddArc(cachedRect.X, cachedRect.Y, d, d, 180, 90);
            cachedPath.AddArc(cachedRect.Right - d, cachedRect.Y, d, d, 270, 90);
            cachedPath.AddArc(cachedRect.Right - d, cachedRect.Bottom - d, d, d, 0, 90);
            cachedPath.AddArc(cachedRect.X, cachedRect.Bottom - d, d, d, 90, 90);
            cachedPath.CloseFigure();
        }

        private void UpdateTextCache(Rectangle inner)
        {
            // TOP TEXT CACHE ---------------------------------------------------------
            if (!string.IsNullOrEmpty(TopText))
            {
                bool needsUpdate = lastTopText != TopText ||
                                   lastTopOffset != TopTextOffsetX ||
                                   lastTopFont != TopFont ||
                                   lastTopOffsetY != TopTextOffsetY;

                if (needsUpdate)
                {
                    // ✅ Measure with truncated text
                    string displayText = TruncateText(TopText, TopTextCharLimit);
                    topTextSize = TextRenderer.MeasureText(displayText, TopFont);

                    int textY = inner.Y + TopTextOffsetY;

                    int textX = inner.X + TopTextOffsetX + BorderThickness + 8;
                    topTextPos = new Point(textX, textY);

                    lastTopText = TopText;
                    lastTopOffset = TopTextOffsetX;
                    lastTopFont = TopFont;
                    lastTopOffsetY = TopTextOffsetY;
                }
            }

            // BOTTOM TEXT CACHE ------------------------------------------------------
            if (!string.IsNullOrEmpty(BottomText))
            {
                bool needsUpdate =
                    lastBottomText != BottomText ||
                    lastBottomOffset != BottomTextOffsetX ||
                    lastBottomFont != BottomFont ||
                    lastBottomOffsetY != BottomTextOffsetY;

                if (needsUpdate)
                {
                    // ✅ Measure with truncated text
                    string displayText = TruncateText(BottomText, BottomTextCharLimit);
                    bottomTextSize = TextRenderer.MeasureText(displayText, BottomFont);

                    int textY = inner.Bottom - bottomTextSize.Height - BottomTextOffsetY;

                    int textX = inner.X + BottomTextOffsetX + BorderThickness + 8;
                    bottomTextPos = new Point(textX, textY);

                    lastBottomText = BottomText;
                    lastBottomOffset = BottomTextOffsetX;
                    lastBottomFont = BottomFont;
                    lastBottomOffsetY = BottomTextOffsetY;
                }
            }
        }

        #region INVALIDATE CACHE / CLEANUP
        private void InvalidateTextCache()
        {
            topTextPos = Point.Empty;
            bottomTextPos = Point.Empty;
            topTextSize = Size.Empty;
            bottomTextSize = Size.Empty;
            lastTopText = string.Empty;
            lastBottomText = string.Empty;
            lastTopFont = null;
            lastBottomFont = null;
            lastTopOffsetY = -1;
            lastBottomOffsetY = -1;
        }

        private void InvalidateGeometryCache()
        {
            if (cachedPath != null)
            {
                cachedPath.Dispose();
                cachedPath = null!;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                InvalidateGeometryCache();

            base.Dispose(disposing);
        }

        #endregion
    }
}
