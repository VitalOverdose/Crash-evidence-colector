using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Drawing.Imaging;
using ProfessorSnowsVideoDownloader.FormHelperClasses;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// ModernButton — a custom rounded WinForms button control.
    /// Now includes HoverBorderColor, PressedBorderColor, HoverBorderSize, and PressedBorderSize.
    /// </summary>
    public class ModernButton : Button, IAdaptiveRowPanelItem
    {
        // ========================================================================================
        // FIELDS
        // ========================================================================================
        private int borderSize = 1;
        private int borderRadius = 10;
        private Color borderColor = Color.Gray;

        // NEW — optional state-specific border colors
        private Color? hoverBorderColor = null;
        private Color? pressedBorderColor = null;

        // NEW — optional state-specific border thickness
        private int? hoverBorderSize = null;
        private int? pressedBorderSize = null;

        private Color backgroundColor = Color.White;
        private Color textColor = Color.FromArgb(60, 60, 60);
        private Color hoverColor = Color.WhiteSmoke;
        private Color pressedColor = Color.Gainsboro;

        private bool isHovered = false;
        private bool isPressed = false;

        private Image? buttonImage = null;
        private Image? buttonImageToggled = null;
        private Image? buttonImageHovered = null;
        private Image? buttonImagePressed = null;
        private Font? hoverFont = null;
        private bool toggled = false;
        private bool hoverGrowsImage = false;
        private bool hoverColorFx = true;
        private bool pressedColorFx = true;
        private bool allowContentClipping = false;
        private ContentAlignment imageAlign = ContentAlignment.MiddleLeft;
        private Size imageSize = new Size(24, 24);

        private int textXOffset = 0;
        private bool multiline = false;
        private string line2 = "";
        private ContentAlignment line2Align = ContentAlignment.MiddleCenter;
        private string textIcon = "";
        private bool iconOnlyMode = false;
        private bool useAppIconOnlyMode = true;
        private bool settingsEventSubscribed = false;
        private const int ContentHorizontalPadding = 8;
        private const int ContentVerticalPadding = 10;
        private const int ImageTextSpacing = 4;
        private const int HoverImageGrow = 2;

        // ========================================================================================
        // PROPERTIES
        // ========================================================================================
        [Category("Appearance")]
        [Description("Width of the button border in pixels.")]
        public int BorderSize
        {
            get => borderSize;
            set { borderSize = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Corner radius for rounded edges.")]
        public int BorderRadius
        {
            get => borderRadius;
            set { borderRadius = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Base color used for the button border.")]
        public Color BorderColor
        {
            get => borderColor;
            set { borderColor = value; Invalidate(); }
        }

        // ========================================================================================
        // NEW PROPERTIES — Hover & Pressed Border Colors and Sizes
        // ========================================================================================
        [Category("Appearance")]
        [Description("Border color when the button is hovered over. If unset, BorderColor is used.")]
        public Color HoverBorderColor
        {
            get => hoverBorderColor ?? borderColor;
            set { hoverBorderColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Border color when the button is pressed. If unset, BorderColor is used.")]
        public Color PressedBorderColor
        {
            get => pressedBorderColor ?? borderColor;
            set { pressedBorderColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Border width (in pixels) when hovered. If unset, BorderSize is used.")]
        public int HoverBorderSize
        {
            get => hoverBorderSize ?? borderSize;
            set { hoverBorderSize = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Border width (in pixels) when pressed. If unset, BorderSize is used.")]
        public int PressedBorderSize
        {
            get => pressedBorderSize ?? borderSize;
            set { pressedBorderSize = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Base fill colour of the button FACE (inside the border radius). The area outside the radius is BackColor.")]
        public Color BackgroundColor
        {
            get => backgroundColor;
            set
            {
                backgroundColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Text color used for button label.")]
        public Color TextColor
        {
            get => textColor;
            set { textColor = value; ForeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Background color when the button is hovered over.")]
        public Color HoverColor
        {
            get => hoverColor;
            set { hoverColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Background color when the button is pressed.")]
        public Color PressedColor
        {
            get => pressedColor;
            set { pressedColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Image displayed on the button.")]
        public Image? ButtonImage
        {
            get => buttonImage;
            set
            {
                buttonImage = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Alternate image painted when Toggled is true (e.g. an up-arrow to ButtonImage's down-arrow). Falls back to ButtonImage if unset.")]
        [DefaultValue(null)]
        public Image? ButtonImageToggled
        {
            get => buttonImageToggled;
            set
            {
                buttonImageToggled = value;
                if (toggled) InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Image painted while the mouse is over the button (a highlight variant of ButtonImage). Falls back to the normal image when unset. Pressed and Toggled take priority over it.")]
        [DefaultValue(null)]
        public Image? ButtonImageHovered
        {
            get => buttonImageHovered;
            set
            {
                buttonImageHovered = value;
                if (isHovered) InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("When true, paints ButtonImageToggled instead of ButtonImage (no-op if ButtonImageToggled is unset).")]
        [DefaultValue(false)]
        public bool Toggled
        {
            get => toggled;
            set
            {
                if (toggled == value) return;
                toggled = value;
                if (buttonImageToggled != null) InvalidateContentLayout();
                ToggledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raised when Toggled changes. Exists so a VIRTUALIZED button can be repainted: a
        /// virtual child has no HWND, so InvalidateContentLayout() above reaches an Invalidate()
        /// that does nothing, and the new toggle state only appeared on the next unrelated
        /// repaint. VirtualizingAdaptiveRowPanel hooks this the way it hooks TextChanged.
        /// Fires regardless of whether a toggled image is set, since the renderer may express
        /// the state some other way.
        /// </summary>
        [Category("Property Changed")]
        [Description("Raised when the Toggled property changes.")]
        public event EventHandler? ToggledChanged;

        [Category("Appearance")]
        [Description("Grows the image by 2px on hover, but only when the button has room for it. Off by default.")]
        [DefaultValue(false)]
        public bool HoverGrowsImage
        {
            get => hoverGrowsImage;
            set
            {
                if (hoverGrowsImage == value) return;
                hoverGrowsImage = value;
                Invalidate();
            }
        }

        [Category("Layout")]
        [DefaultValue(false)]
        [Description("Lets a layout size this button narrower than its image + padding, clipping the image instead of forcing the button wider. AdaptiveRowPanel floors each child at its content width, so a button meant to collapse to a sliver needs this on.")]
        public bool AllowContentClipping
        {
            get => allowContentClipping;
            set
            {
                if (allowContentClipping == value) return;
                allowContentClipping = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("When off, hovering leaves the background alone and HoverColor is ignored — no need to set HoverColor to match BackgroundColor just to suppress it. Border hover properties are unaffected.")]
        [DefaultValue(true)]
        public bool HoverColorFx
        {
            get => hoverColorFx;
            set
            {
                if (hoverColorFx == value) return;
                hoverColorFx = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("When off, pressing leaves the background alone and PressedColor is ignored. Border pressed properties are unaffected.")]
        [DefaultValue(true)]
        public bool PressedColorFx
        {
            get => pressedColorFx;
            set
            {
                if (pressedColorFx == value) return;
                pressedColorFx = value;
                Invalidate();
            }
        }

        // Image actually painted/measured. Priority: the toggled alt when active, then the hover
        // highlight while the cursor is over the button, then the primary. All fall back to the
        // primary if their own image is unset.
        private Image? CurrentImage =>
            (isPressed && buttonImagePressed != null) ? buttonImagePressed
            : (toggled && buttonImageToggled != null) ? buttonImageToggled
            : (isHovered && buttonImageHovered != null) ? buttonImageHovered
            : buttonImage;

        [Category("Appearance")]
        [Description("Image painted while the button is held down. Falls back through Hovered to the primary when unset.")]
        [DefaultValue(null)]
        public Image? ButtonImagePressed
        {
            get => buttonImagePressed;
            set
            {
                buttonImagePressed = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Font used for the button text while hovered. When null, Font is used in every state. Layout/auto-size stays based on the normal Font, so hover doesn't resize the button.")]
        public Font? FontHovered
        {
            get => hoverFont;
            set { hoverFont = value; if (isHovered) Invalidate(); }
        }

        // Font is a reference type, so [DefaultValue(null)] won't drive designer serialization —
        // these do (only serialize when set; Reset clears it).
        private bool ShouldSerializeFontHovered() => hoverFont != null;
        private void ResetFontHovered() => FontHovered = null;

        // Text font for the current state: FontHovered while hovered (and enabled), else Font. Used
        // for painting only — measurement/layout stays on Font so the button footprint is stable.
        private Font CurrentFont =>
            isHovered && Enabled && hoverFont != null ? hoverFont : Font;

        // Opt-in hover nudge. Grows from the centre so the image stays put, and only when the
        // grown rect still fits the content area — otherwise it would clip or crowd the text.
        // Pressing drops it back to normal size, so the image reads as being pushed in.
        // Layout/measurement deliberately ignore this: it is a paint-time effect only, so the
        // button never resizes or reflows its text just because the cursor is over it.
        private Rectangle ApplyHoverGrow(Rectangle imageRect, Rectangle bounds)
        {
            if (!hoverGrowsImage || !isHovered || isPressed || !Enabled)
            {
                return imageRect;
            }

            Rectangle grown = imageRect;
            grown.Inflate(HoverImageGrow / 2, HoverImageGrow / 2);

            return bounds.Contains(grown) ? grown : imageRect;
        }

        [Category("Appearance")]
        [Description("Alignment of the image on the button.")]
        [DefaultValue(ContentAlignment.MiddleLeft)]
        public new ContentAlignment ImageAlign
        {
            get => imageAlign;
            set
            {
                imageAlign = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Size of the image.")]
        public Size ImageSize
        {
            get => imageSize;
            set
            {
                imageSize = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Text-based icon used when the button is in icon-only mode and no ButtonImage is set. Useful for emoji icons.")]
        [DefaultValue("")]
        public string TextIcon
        {
            get => textIcon;
            set
            {
                textIcon = value ?? "";
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Forces this button to render as icon-only, using ButtonImage first or TextIcon if no image is set.")]
        [DefaultValue(false)]
        public bool IconOnlyMode
        {
            get => iconOnlyMode;
            set
            {
                iconOnlyMode = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("When enabled, this button follows the app-wide ModernButton icon-only setting.")]
        [DefaultValue(true)]
        public bool UseAppIconOnlyMode
        {
            get => useAppIconOnlyMode;
            set
            {
                useAppIconOnlyMode = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Allow the button text to wrap across multiple lines.")]
        [DefaultValue(false)]
        public bool Multiline
        {
            get => multiline;
            set
            {
                multiline = value;
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Optional second line of text drawn below the main Text label.")]
        [DefaultValue("")]
        public string TextLine2
        {
            get => line2;
            set
            {
                line2 = value ?? "";
                InvalidateContentLayout();
            }
        }

        [Category("Appearance")]
        [Description("Alignment of the TextLine2 text within its half of the button.")]
        [DefaultValue(ContentAlignment.MiddleCenter)]
        public ContentAlignment TextLine2Align
        {
            get => line2Align;
            set { line2Align = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Horizontal offset (in pixels) applied to button text. Negative values move left; positive move right.")]
        [DefaultValue(0)]
        public int TextXOffset
        {
            get => textXOffset;
            set
            {
                if (textXOffset != value)
                {
                    textXOffset = value;
                    Invalidate();
                }
            }
        }

        // ========================================================================================
        // CONSTRUCTOR
        // ========================================================================================
        public ModernButton()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Opaque, false);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.MouseOverBackColor = Color.Transparent;

            Size = new Size(150, 40);
            // Default backdrop stays transparent so nothing starts inheriting a parent colour
            // ambiently — set BackColor explicitly (or use the ARP's Apply Surface Color) to
            // give a button an opaque backdrop and skip the parent-repaint cost.
            BackColor = Color.Transparent;
            ForeColor = textColor;

            MouseEnter += (s, e) => { isHovered = true; Invalidate(); };
            MouseLeave += (s, e) => { isHovered = false; isPressed = false; Invalidate(); };
            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } };
            MouseUp += (s, e) => { isPressed = false; Invalidate(); };

            Resize += (s, e) => Invalidate();
        }

        // BackColor means what it says: the backdrop BEHIND the rounded face, i.e. what shows
        // at the corners. It used to be hijacked — any non-transparent value was copied into
        // the FACE colour and BackColor forced back to Transparent — which made it impossible
        // to give a button an opaque backdrop matching its surface, and so every button paid
        // the transparency tax (a transparent child forces its parent to repaint the region).
        // The face colour lives in BackgroundColor, and only there. (2026-08-04)
        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            InvalidateContentLayout();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            InvalidateContentLayout();
        }

        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);
            Invalidate();
        }

        // ========================================================================================
        // DISABLED FADE SUPPORT
        // ========================================================================================
        private int _disabledFadePercent = 60;

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
                    Invalidate();
                }
            }
        }

        // Internal: shared with the ARP v2 virtual renderer so virtualized buttons disable
        // exactly like real ones (fade toward the parent surface by DisabledFadePercent).
        internal static Color FadeToward(Color source, Color background, int fadePercent)
        {
            float t = Math.Clamp(fadePercent / 100f, 0f, 1f);
            int r = (int)Math.Round(source.R * (1f - t) + background.R * t);
            int g = (int)Math.Round(source.G * (1f - t) + background.G * t);
            int b = (int)Math.Round(source.B * (1f - t) + background.B * t);
            int a = (int)Math.Round(source.A * (1f - t) + background.A * t);
            return Color.FromArgb(a, r, g, b);
        }

        internal static void DrawImageFaded(Graphics g, Image img, Rectangle dest, int fadePercent)
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

            g.DrawImage(img, dest, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        // ========================================================================================
        // DRAW HELPERS
        // ========================================================================================
        private GraphicsPath GetFigurePath(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float curveSize = Math.Min(Math.Max(1F, radius * 2F), Math.Min(rect.Width, rect.Height));

            path.StartFigure();
            path.AddArc(rect.X, rect.Y, curveSize, curveSize, 180, 90);
            path.AddArc(rect.Right - curveSize, rect.Y, curveSize, curveSize, 270, 90);
            path.AddArc(rect.Right - curveSize, rect.Bottom - curveSize, curveSize, curveSize, 0, 90);
            path.AddArc(rect.X, rect.Bottom - curveSize, curveSize, curveSize, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InvalidateContentLayout()
        {
            Invalidate();
            Parent?.PerformLayout(this, nameof(PreferredSize));
        }

        internal int GetPreferredContentWidth(float textScale = 1f)
        {
            // A layout that deliberately collapses this button wants it clipped, not widened.
            // AdaptiveRowPanel floors every child at its content width, so reporting the real
            // image+padding here would silently veto any narrower width it was given.
            if (allowContentClipping)
            {
                return 1;
            }

            int textWidth = GetMeasuredTextWidth(Text, Font);
            if (!string.IsNullOrEmpty(line2))
            {
                textWidth = Math.Max(textWidth, GetMeasuredTextWidth(line2, Font));
            }

            int scaledTextWidth = (int)Math.Ceiling(textWidth * Math.Max(0f, textScale));
            int imageWidth = CurrentImage == null ? 0 : Math.Max(0, imageSize.Width);

            if (IsIconOnlyActive() && HasIconOnlyContent())
            {
                int iconWidth = imageWidth > 0
                    ? imageWidth
                    : GetMeasuredTextWidth(textIcon, Font);

                return Math.Max(1, iconWidth + ContentHorizontalPadding * 2);
            }

            bool hasText = scaledTextWidth > 0;
            bool hasImage = imageWidth > 0;

            if (hasImage && hasText && IsHorizontalEdgeAligned(imageAlign))
            {
                return Math.Max(1, ContentHorizontalPadding + imageWidth + ImageTextSpacing + scaledTextWidth + ContentHorizontalPadding);
            }

            int contentWidth = Math.Max(scaledTextWidth, imageWidth);
            return contentWidth > 0
                ? Math.Max(1, contentWidth + ContentHorizontalPadding * 2)
                : Math.Max(1, Width);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size basePreferred = base.GetPreferredSize(proposedSize);
            int preferredWidth = Math.Max(basePreferred.Width, GetPreferredContentWidth());
            int preferredHeight = Math.Max(basePreferred.Height, GetPreferredContentHeight());

            return new Size(
                Math.Max(MinimumSize.Width, preferredWidth),
                Math.Max(MinimumSize.Height, preferredHeight));
        }

        private int GetPreferredContentHeight()
        {
            int textHeight = GetMeasuredTextHeight(Text, Font);
            if (!string.IsNullOrEmpty(line2))
            {
                textHeight += GetMeasuredTextHeight(line2, Font);
            }

            int imageHeight = CurrentImage == null ? 0 : Math.Max(0, imageSize.Height);
            int contentHeight = Math.Max(textHeight, imageHeight);

            return contentHeight > 0
                ? contentHeight + ContentVerticalPadding
                : Math.Max(1, Height);
        }

        private static int GetMeasuredTextWidth(string? text, Font? font)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            return TextRenderer.MeasureText(text, font ?? Control.DefaultFont, new Size(int.MaxValue, int.MaxValue), flags).Width;
        }

        private static int GetMeasuredTextHeight(string? text, Font? font)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            return TextRenderer.MeasureText(text, font ?? Control.DefaultFont, new Size(int.MaxValue, int.MaxValue), flags).Height;
        }

        private static bool IsLeftAligned(ContentAlignment alignment)
        {
            return alignment is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft;
        }

        private static bool IsRightAligned(ContentAlignment alignment)
        {
            return alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight;
        }

        private static bool IsCenterAligned(ContentAlignment alignment)
        {
            return alignment is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter;
        }

        private static bool IsBottomAligned(ContentAlignment alignment)
        {
            return alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight;
        }

        private static bool IsMiddleAligned(ContentAlignment alignment)
        {
            return alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight;
        }

        private static bool IsHorizontalEdgeAligned(ContentAlignment alignment)
        {
            return IsLeftAligned(alignment) || IsRightAligned(alignment);
        }

        private Rectangle CalcImageRect(Rectangle bounds)
        {
            int x = IsRightAligned(imageAlign) ? bounds.Right - imageSize.Width - ContentHorizontalPadding
                  : IsCenterAligned(imageAlign) ? bounds.X + (bounds.Width - imageSize.Width) / 2
                  : bounds.X + ContentHorizontalPadding;

            int y = IsBottomAligned(imageAlign) ? bounds.Bottom - imageSize.Height - 5
                  : IsMiddleAligned(imageAlign) ? bounds.Y + (bounds.Height - imageSize.Height) / 2
                  : bounds.Y + 5;

            return new Rectangle(x, y, imageSize.Width, imageSize.Height);
        }

        // ========================================================================================
        // ONPAINT — Main drawing logic (now includes hover/pressed border size)
        // ========================================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // --- Determine base states ---
            int currentBorderSize = borderSize;
            Color baseBorder = borderColor;
            Color baseBack = backgroundColor;

            if (isPressed)
            {
                currentBorderSize = pressedBorderSize ?? borderSize;
                baseBorder = pressedBorderColor ?? borderColor;
                if (pressedColorFx)
                    baseBack = pressedColor;
            }
            else if (isHovered)
            {
                currentBorderSize = hoverBorderSize ?? borderSize;
                baseBorder = hoverBorderColor ?? borderColor;
                if (hoverColorFx)
                    baseBack = hoverColor;
            }

            Rectangle rectSurface = new Rectangle(0, 0, Width - 1, Height - 1);
            int safeBorderInset = Math.Min(Math.Max(0, currentBorderSize), Math.Max(0, Math.Min(Width, Height) / 2));
            Rectangle rectBorder = Rectangle.Inflate(rectSurface, -safeBorderInset, -safeBorderInset);
            bool isDisabled = !Enabled;
            int fade = isDisabled ? _disabledFadePercent : 0;
            Color parentBack = this.Parent?.BackColor ?? SystemColors.Control;

            Color backPaint = FadeToward(baseBack, parentBack, fade);
            Color borderPaint = FadeToward(baseBorder, parentBack, fade);
            Color textPaint = FadeToward(textColor, parentBack, fade);
            bool drawIconOnly = IsIconOnlyActive() && HasIconOnlyContent();

            // --- Draw rounded or flat button ---
            int paintRadius = Math.Min(borderRadius, Math.Max(0, Math.Min(Width, Height) / 2));
            if (paintRadius > 2)
            {
                using (GraphicsPath pathSurface = GetFigurePath(rectSurface, paintRadius))
                using (GraphicsPath pathBorder = GetFigurePath(rectBorder, Math.Max(1, paintRadius - 1f)))
                using (Pen penBorder = new Pen(borderPaint, currentBorderSize))
                using (SolidBrush fillBrush = new SolidBrush(backPaint))
                {
                    this.Region = new Region(pathSurface);
                    g.FillPath(fillBrush, pathSurface);

                    if (currentBorderSize >= 1)
                        g.DrawPath(penBorder, pathBorder);
                }
            }
            else
            {
                this.Region = new Region(rectSurface);
                using (SolidBrush fillBrush = new SolidBrush(backPaint))
                    g.FillRectangle(fillBrush, rectSurface);

                if (currentBorderSize >= 1)
                {
                    using (Pen penBorder = new Pen(borderPaint, currentBorderSize))
                    {
                        penBorder.Alignment = PenAlignment.Inset;
                        g.DrawRectangle(penBorder, 0, 0, Width - 1, Height - 1);
                    }
                }
            }

            if (drawIconOnly)
            {
                DrawIconOnlyContent(g, rectSurface, textPaint, isDisabled);
                return;
            }

            // --- Draw image and text ---
            Image? img = CurrentImage;
            if (img != null)
            {
                Rectangle imgRect = ApplyHoverGrow(CalcImageRect(rectSurface), rectSurface);
                if (isDisabled)
                    DrawImageFaded(g, img, imgRect, _disabledFadePercent);
                else
                    g.DrawImage(img, imgRect);
            }

            Rectangle textRect = rectSurface;
            if (img != null && IsLeftAligned(imageAlign))
            {
                Rectangle imgRect = CalcImageRect(rectSurface);
                textRect = new Rectangle(imgRect.Right + ImageTextSpacing, rectSurface.Y,
                    rectSurface.Width - imgRect.Width - ContentHorizontalPadding - ImageTextSpacing, rectSurface.Height);
            }
            else if (img != null && IsRightAligned(imageAlign))
            {
                Rectangle imgRect = CalcImageRect(rectSurface);
                textRect = new Rectangle(rectSurface.X, rectSurface.Y,
                    rectSurface.Width - imgRect.Width - ContentHorizontalPadding - ImageTextSpacing, rectSurface.Height);
            }

            textRect.Offset(textXOffset, 0);

            bool hasTextLine2 = !string.IsNullOrEmpty(line2);
            if (hasTextLine2)
            {
                int half = textRect.Height / 2;
                var r1 = new Rectangle(textRect.X, textRect.Y + 2, textRect.Width, half);
                var r2 = new Rectangle(textRect.X, textRect.Y + half, textRect.Width, textRect.Height - half);
                TextRenderer.DrawText(g, Text,  CurrentFont, r1, textPaint, ToTextFormatFlags(TextAlign,  false));
                TextRenderer.DrawText(g, line2, CurrentFont, r2, textPaint, ToTextFormatFlags(line2Align, false));
            }
            else
            {
                TextRenderer.DrawText(g, Text, CurrentFont, textRect, textPaint, ToTextFormatFlags(TextAlign, multiline));
            }
        }

        // ========================================================================================
        // ONHANDLECREATED
        // ========================================================================================
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Parent != null)
            {
                Parent.BackColorChanged += (s, ev) => Invalidate();
            }

            if (!settingsEventSubscribed && !IsInDesignMode())
            {
                ControlSettings.AppIconOnlyModeChanged += SettingsManager_AppIconOnlyModeChanged;
                settingsEventSubscribed = true;
            }
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing && settingsEventSubscribed)
            {
                ControlSettings.AppIconOnlyModeChanged -= SettingsManager_AppIconOnlyModeChanged;
                settingsEventSubscribed = false;
            }

            base.Dispose(disposing);
        }

        private void SettingsManager_AppIconOnlyModeChanged()
        {
            if (!IsDisposed && IsHandleCreated)
                Invalidate();
        }

        private bool IsIconOnlyActive()
        {
            if (iconOnlyMode)
                return true;

            return useAppIconOnlyMode &&
                   !IsInDesignMode() &&
                   ControlSettings.AppIconOnlyMode;
        }

        private bool HasIconOnlyContent()
        {
            return CurrentImage != null || !string.IsNullOrWhiteSpace(textIcon);
        }

        private void DrawIconOnlyContent(Graphics g, Rectangle bounds, Color textPaint, bool isDisabled)
        {
            Image? img = CurrentImage;
            if (img != null)
            {
                int w = Math.Min(imageSize.Width, Math.Max(0, bounds.Width - 8));
                int h = Math.Min(imageSize.Height, Math.Max(0, bounds.Height - 8));
                var imgRect = ApplyHoverGrow(new Rectangle(
                    bounds.X + (bounds.Width - w) / 2,
                    bounds.Y + (bounds.Height - h) / 2,
                    w,
                    h), bounds);

                if (isDisabled)
                    DrawImageFaded(g, img, imgRect, _disabledFadePercent);
                else
                    g.DrawImage(img, imgRect);

                return;
            }

            TextRenderer.DrawText(g, textIcon, CurrentFont, bounds, textPaint,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime ||
                   Site?.DesignMode == true ||
                   Parent?.Site?.DesignMode == true;
        }

        // ========================================================================================
        // TEXT ALIGNMENT HELPER
        // ========================================================================================
        private static TextFormatFlags ToTextFormatFlags(ContentAlignment align, bool multiline = false)
        {
            var flags = align switch
            {
                ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
                ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
                ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
                ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter | TextFormatFlags.Left,
                ContentAlignment.MiddleCenter => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
                ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
                ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
                ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
                ContentAlignment.BottomRight => TextFormatFlags.Bottom | TextFormatFlags.Right,
                _ => TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            };

            if (multiline)
                flags |= TextFormatFlags.WordBreak;

            return flags;
        }
    }
}
