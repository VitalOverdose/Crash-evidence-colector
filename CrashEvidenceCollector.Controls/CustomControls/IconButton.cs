using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// Where the text sits relative to the icon. Center draws the text over the icon;
    /// None draws no text at all (Text stays assigned, so tooltips/automation still see it)
    /// and the button sizes to the icon alone.
    /// </summary>
    public enum IconButtonTextPosition
    {
        Right,
        Left,
        Up,
        Down,
        Center,
        None
    }

    /// <summary>
    /// How the second image family (ButtonImageB/BHover/BPressed) is used.
    /// None: B family unused — a plain momentary button on the A family.
    /// Toggle: toggle button — the B family is the on-state, a click flips it.
    /// Hover/pressed swaps now come from the per-state slots (AHover/APressed, BHover/BPressed),
    /// so they work in both modes. Size/brightness feedback applies in every mode.
    /// </summary>
    public enum IconButtonImageBUse
    {
        None,
        Toggle
    }

    /// <summary>
    /// An image-first button. Unlike <see cref="ModernButton"/> it has no border, no rounded
    /// corners and no corner-clearing padding, so its width is just the image (plus optional
    /// text) — an AdaptiveRowPanel never floors it above its content, it simply clips if the
    /// row makes it narrower than the icon.
    ///
    /// Two image FAMILIES: A (off/normal) and B (toggled-on when <see cref="ImageBUse"/> is
    /// Toggle), each with optional Hover and Pressed slots that fall back to the family's
    /// normal image when unset. Press/hover feedback is also numeric — a size delta and a
    /// brightness shift — on top of any image swap.
    /// </summary>
    [ToolboxItem(true)]
    public class IconButton : Control
    {
        private Image? _imageA;
        private Image? _imageAHover;
        private Image? _imageAPressed;
        private Image? _imageB;
        private Image? _imageBHover;
        private Image? _imageBPressed;
        private Image? _imageDisabled;
        private Size _imageSize = new Size(40, 40);
        private IconButtonImageBUse _imageBUse = IconButtonImageBUse.None;

        private bool _toggled;
        private string _toggledText = string.Empty;

        private int _hoveredSize;
        private int _hoveredBrightness;
        private int _pressedSize;
        private int _pressedBrightness;

        private HorizontalAlignment _contentAlignment = HorizontalAlignment.Center;
        private Color _textColor = Color.FromArgb(60, 60, 60);
        private Font? _hoveredFont;
        private IconButtonTextPosition _textPosition = IconButtonTextPosition.Right;
        private int _textOffsetX;
        private int _textOffsetY;
        private int _imageOffsetX;
        private int _imageOffsetY;

        private bool _isHovered;
        private bool _isPressed;

        private const int ImageTextGap = 4;

        /// <summary>Raised whenever <see cref="Toggled"/> changes, by click or in code.</summary>
        public event EventHandler? ToggledChanged;

        public IconButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(44, 44);
        }

        // ── Images ──────────────────────────────────────────────────────────────────────────

        [Category("Appearance")]
        [Description("The primary image, shown by default.")]
        [DefaultValue(null)]
        public Image? ButtonImageA
        {
            get => _imageA;
            set { _imageA = value; OnContentChanged(); }
        }

        [Category("Appearance")]
        [Description("Shown while hovered (untoggled). Falls back to ButtonImageA when unset.")]
        [DefaultValue(null)]
        public Image? ButtonImageAHover
        {
            get => _imageAHover;
            set { _imageAHover = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Shown while pressed (untoggled). Falls back to ButtonImageAHover, then ButtonImageA.")]
        [DefaultValue(null)]
        public Image? ButtonImageAPressed
        {
            get => _imageAPressed;
            set { _imageAPressed = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("The toggled-on image (ImageBUse = Toggle). Falls back to ButtonImageA when unset.")]
        [DefaultValue(null)]
        public Image? ButtonImageB
        {
            get => _imageB;
            set { _imageB = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Shown while hovered in the toggled-on state. Falls back to ButtonImageB.")]
        [DefaultValue(null)]
        public Image? ButtonImageBHover
        {
            get => _imageBHover;
            set { _imageBHover = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Shown while pressed in the toggled-on state. Falls back to ButtonImageBHover, then ButtonImageB.")]
        [DefaultValue(null)]
        public Image? ButtonImageBPressed
        {
            get => _imageBPressed;
            set { _imageBPressed = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Shown as-is while the button is disabled. When unset, the current image is drawn greyed out instead.")]
        [DefaultValue(null)]
        public Image? ButtonImageDisabled
        {
            get => _imageDisabled;
            set { _imageDisabled = value; Invalidate(); }
        }

        [Category("Behavior")]
        [Description("None: plain button on the A images. Toggle: a click flips Toggled and the B images become the on-state.")]
        [DefaultValue(IconButtonImageBUse.None)]
        public IconButtonImageBUse ImageBUse
        {
            get => _imageBUse;
            set { _imageBUse = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Size the images are drawn at.")]
        public Size ImageSize
        {
            get => _imageSize;
            set { _imageSize = value; OnContentChanged(); }
        }

        // ── Toggle state ────────────────────────────────────────────────────────────────────

        [Category("Behavior")]
        [Description("On-state of a toggle button (ImageBUse = Toggle): false shows A, true shows B. Settable in code; a click flips it when ImageBUse is Toggle.")]
        [DefaultValue(false)]
        public bool Toggled
        {
            get => _toggled;
            set
            {
                if (_toggled == value) return;
                _toggled = value;
                Invalidate();
                ToggledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("Appearance")]
        [Description("Label shown while toggled on (ImageBUse = Toggle), e.g. Play/Pause. Empty = keep Text in both states.")]
        [DefaultValue("")]
        public string ToggledText
        {
            get => _toggledText;
            set { _toggledText = value ?? string.Empty; OnContentChanged(); }
        }

        // ── Feedback ────────────────────────────────────────────────────────────────────────

        [Category("Appearance")]
        [Description("Pixels added to the image while hovered (grown from the centre, capped so it never exceeds the button).")]
        [DefaultValue(0)]
        public int HoveredSize
        {
            get => _hoveredSize;
            set { _hoveredSize = ClampSizeDelta(value); if (_isHovered) Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Image brightness while hovered, -100 to 100 (0 = unchanged).")]
        [DefaultValue(0)]
        public int HoveredBrightness
        {
            get => _hoveredBrightness;
            set { _hoveredBrightness = Math.Clamp(value, -100, 100); if (_isHovered) Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Pixels added to the image while pressed (negative shrinks it, reading as pushed in).")]
        [DefaultValue(0)]
        public int PressedSize
        {
            get => _pressedSize;
            set { _pressedSize = ClampSizeDelta(value); if (_isPressed) Invalidate(); }
        }

        // Keeps a size delta to what the button can actually show: a positive grow can't exceed the
        // free space around the image, a negative shrink can't collapse it below 1px. Runs only
        // once the control has a real size (a live designer edit or runtime) — during code-gen load
        // the geometry isn't set yet, so the value passes through and the paint-time cap covers it.
        private int ClampSizeDelta(int value)
        {
            if (!IsHandleCreated)
            {
                return value;
            }

            if (value <= 0)
            {
                int maxShrink = Math.Max(0, Math.Min(_imageSize.Width, _imageSize.Height) - 1);
                return -Math.Min(-value, maxShrink);
            }

            int slack = Math.Max(0, Math.Min(Width - _imageSize.Width, Height - _imageSize.Height));
            return Math.Min(value, slack);
        }

        [Category("Appearance")]
        [Description("Image brightness while pressed, -100 to 100 (0 = unchanged).")]
        [DefaultValue(0)]
        public int PressedBrightness
        {
            get => _pressedBrightness;
            set { _pressedBrightness = Math.Clamp(value, -100, 100); if (_isPressed) Invalidate(); }
        }

        // ── Text ────────────────────────────────────────────────────────────────────────────

        [Category("Appearance")]
        [Description("Colour of the optional label.")]
        public Color TextColor
        {
            get => _textColor;
            set { _textColor = value; ForeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Horizontal placement of the content. Left/Right PLANT THE ICON at that edge (text takes the inboard side) so a label change (e.g. ToggledText) can never shift it; Center keeps the icon+text pair centred as before. Padding insets the anchored edge.")]
        [DefaultValue(HorizontalAlignment.Center)]
        public HorizontalAlignment ContentAlignment
        {
            get => _contentAlignment;
            set { _contentAlignment = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Where the text sits relative to the icon.")]
        [DefaultValue(IconButtonTextPosition.Right)]
        public IconButtonTextPosition TextPosition
        {
            get => _textPosition;
            set { _textPosition = value; OnContentChanged(); }
        }

        [Category("Appearance")]
        [Description("Nudges the text horizontally from its computed position.")]
        [DefaultValue(0)]
        public int TextOffsetX
        {
            get => _textOffsetX;
            set { _textOffsetX = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Nudges the text vertically from its computed position.")]
        [DefaultValue(0)]
        public int TextOffsetY
        {
            get => _textOffsetY;
            set { _textOffsetY = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Nudges the image horizontally from its computed position (handy in Up/Down layouts to make room for the hover grow).")]
        [DefaultValue(0)]
        public int ImageOffsetX
        {
            get => _imageOffsetX;
            set { _imageOffsetX = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Nudges the image vertically from its computed position (handy in Up/Down layouts to make room for the hover grow).")]
        [DefaultValue(0)]
        public int ImageOffsetY
        {
            get => _imageOffsetY;
            set { _imageOffsetY = value; Invalidate(); }
        }

        // Superseded by TextPosition.None — an icon-only button isn't a separate switch, it's
        // the "nowhere" case of where the text goes. Hidden and non-serialized so any old
        // designer file still loads; nothing in the solution actually set it.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool bIconOnly
        {
            get => _textPosition == IconButtonTextPosition.None;
            set
            {
                if (value)
                    TextPosition = IconButtonTextPosition.None;
                else if (_textPosition == IconButtonTextPosition.None)
                    TextPosition = IconButtonTextPosition.Right;
            }
        }

        [Category("Appearance")]
        [Description("Font used for the label while the button is hovered. When null, the normal Font is used in every state. The button's auto-size footprint stays based on the normal Font, so hover doesn't resize the control.")]
        public Font? FontHovered
        {
            get => _hoveredFont;
            set { _hoveredFont = value; if (_isHovered) Invalidate(); }
        }

        // Font is a reference type, so [DefaultValue(null)] won't drive designer serialization/reset —
        // these do (only serialize when set; Reset clears it).
        private bool ShouldSerializeFontHovered() => _hoveredFont != null;
        private void ResetFontHovered() => FontHovered = null;

        // ── State plumbing ──────────────────────────────────────────────────────────────────

        // The label for the current state: ToggledText while toggled on (when set), else Text.
        private string CurrentText =>
            _imageBUse == IconButtonImageBUse.Toggle && _toggled && _toggledText.Length > 0
                ? _toggledText
                : base.Text;

        private bool HasText =>
            _textPosition != IconButtonTextPosition.None && !string.IsNullOrEmpty(CurrentText);

        // The image for the current family (A, or B when toggled on) and state, with per-state
        // fallback inside the family: pressed → hover → normal. The B family's normal falls back
        // to A so a half-configured toggle still shows something.
        private Image? CurrentImage
        {
            get
            {
                // A dedicated disabled face wins over every state - the button can't be hot or
                // pressed while disabled anyway.
                if (!Enabled && _imageDisabled != null) return _imageDisabled;

                if (_imageBUse == IconButtonImageBUse.Toggle && _toggled)
                {
                    Image? normal = _imageB ?? _imageA;
                    if (_isPressed) return _imageBPressed ?? _imageBHover ?? normal;
                    if (_isHovered) return _imageBHover ?? normal;
                    return normal;
                }

                if (_isPressed) return _imageAPressed ?? _imageAHover ?? _imageA;
                if (_isHovered) return _imageAHover ?? _imageA;
                return _imageA;
            }
        }

        // The label font for the current state: FontHovered while hovered (and enabled), else Font.
        private Font CurrentFont =>
            Enabled && _isHovered && _hoveredFont != null ? _hoveredFont : Font;

        private void OnContentChanged()
        {
            if (AutoSize)
            {
                PerformLayout(this, nameof(Text));
            }

            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            OnContentChanged();
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
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        // The toggle flips in OnClick — BEFORE subscribers run, and before OnMouseUp (WinForms
        // raises Click first) — so a Click handler sees the new state and can even overrule it
        // by assigning Toggled, CheckBox-style. Flipping in OnMouseUp instead would run AFTER
        // the handler and silently invert whatever state it set.
        protected override void OnClick(EventArgs e)
        {
            if (_imageBUse == IconButtonImageBUse.Toggle)
            {
                Toggled = !_toggled;   // raises ToggledChanged
            }

            base.OnClick(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        // ── Sizing ──────────────────────────────────────────────────────────────────────────

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size image = _imageSize;

            if (!HasText)
            {
                return image;
            }

            // Footprint fits the WIDER of the two toggle labels, so flipping Play/Pause never
            // resizes the control.
            Size text = TextRenderer.MeasureText(base.Text, Font);
            if (_imageBUse == IconButtonImageBUse.Toggle && _toggledText.Length > 0)
            {
                Size alt = TextRenderer.MeasureText(_toggledText, Font);
                text = new Size(Math.Max(text.Width, alt.Width), Math.Max(text.Height, alt.Height));
            }

            return _textPosition switch
            {
                IconButtonTextPosition.Left or IconButtonTextPosition.Right =>
                    new Size(image.Width + ImageTextGap + text.Width, Math.Max(image.Height, text.Height)),
                IconButtonTextPosition.Up or IconButtonTextPosition.Down =>
                    new Size(Math.Max(image.Width, text.Width), image.Height + ImageTextGap + text.Height),
                _ => new Size(Math.Max(image.Width, text.Width), Math.Max(image.Height, text.Height))
            };
        }

        // ── Painting ────────────────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            LayoutContent(out Rectangle imageRect, out Rectangle textRect);

            Image? image = CurrentImage;
            if (image != null)
            {
                imageRect.Offset(_imageOffsetX, _imageOffsetY);
                Rectangle drawRect = ApplyFeedbackSize(imageRect);
                DrawImage(g, image, drawRect);
            }

            if (HasText)
            {
                Color colour = Enabled ? _textColor : Color.FromArgb(160, 160, 160);
                textRect.Offset(_textOffsetX, _textOffsetY);
                TextRenderer.DrawText(g, CurrentText, CurrentFont, textRect, colour,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }

            // Nothing configured yet (no image, no text) → a faint stacked "Icon / Button" so the
            // control is visible and selectable. It only ever shows on an unconfigured button.
            if (image == null && !HasText)
            {
                DrawPlaceholder(g);
            }
        }

        private void DrawPlaceholder(Graphics g)
        {
            using var font = new Font("Segoe UI", 7f, FontStyle.Regular, GraphicsUnit.Point);
            Color colour = Color.FromArgb(150, 150, 150);
            int lineHeight = TextRenderer.MeasureText(g, "Button", font).Height;

            Rectangle top = new Rectangle(0, (Height / 2) - lineHeight, Width, lineHeight);
            Rectangle bottom = new Rectangle(0, Height / 2, Width, lineHeight);

            const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, "Icon", font, top, colour, flags);
            TextRenderer.DrawText(g, "Button", font, bottom, colour, flags);
        }

        // Places the icon and text according to TextPosition; ContentAlignment sets where the
        // pair (or each element, in the stacked/overlaid layouts) sits horizontally.
        private void LayoutContent(out Rectangle imageRect, out Rectangle textRect)
        {
            Rectangle bounds = ClientRectangle;
            Size image = _imageSize;

            if (!HasText)
            {
                imageRect = new Rectangle(AlignX(bounds, image.Width), bounds.Y + (bounds.Height - image.Height) / 2, image.Width, image.Height);
                textRect = Rectangle.Empty;
                return;
            }

            Size text = TextRenderer.MeasureText(CurrentText, CurrentFont);

            switch (_textPosition)
            {
                case IconButtonTextPosition.Center:
                    imageRect = new Rectangle(AlignX(bounds, image.Width), bounds.Y + (bounds.Height - image.Height) / 2, image.Width, image.Height);
                    textRect = new Rectangle(AlignX(bounds, text.Width), bounds.Y + (bounds.Height - text.Height) / 2, text.Width, text.Height);
                    break;

                // Horizontal pair layouts. Anchored (Left/Right) plants the ICON at that edge and
                // puts the text inboard — overriding TextPosition's side if they conflict — so the
                // icon can never move when the label changes. Center keeps TextPosition's order.
                case IconButtonTextPosition.Left:
                case IconButtonTextPosition.Right:
                {
                    int imageY = bounds.Y + (bounds.Height - image.Height) / 2;
                    int textY = bounds.Y + (bounds.Height - text.Height) / 2;

                    if (_contentAlignment == HorizontalAlignment.Left)
                    {
                        imageRect = new Rectangle(AlignX(bounds, image.Width), imageY, image.Width, image.Height);
                        textRect = new Rectangle(imageRect.Right + ImageTextGap, textY, text.Width, text.Height);
                    }
                    else if (_contentAlignment == HorizontalAlignment.Right)
                    {
                        imageRect = new Rectangle(AlignX(bounds, image.Width), imageY, image.Width, image.Height);
                        textRect = new Rectangle(imageRect.Left - ImageTextGap - text.Width, textY, text.Width, text.Height);
                    }
                    else if (_textPosition == IconButtonTextPosition.Left)
                    {
                        int left = bounds.X + (bounds.Width - (text.Width + ImageTextGap + image.Width)) / 2;
                        textRect = new Rectangle(left, textY, text.Width, text.Height);
                        imageRect = new Rectangle(textRect.Right + ImageTextGap, imageY, image.Width, image.Height);
                    }
                    else
                    {
                        int left = bounds.X + (bounds.Width - (image.Width + ImageTextGap + text.Width)) / 2;
                        imageRect = new Rectangle(left, imageY, image.Width, image.Height);
                        textRect = new Rectangle(imageRect.Right + ImageTextGap, textY, text.Width, text.Height);
                    }

                    break;
                }

                case IconButtonTextPosition.Up:
                {
                    int totalH = text.Height + ImageTextGap + image.Height;
                    int top = bounds.Y + (bounds.Height - totalH) / 2;
                    textRect = new Rectangle(AlignX(bounds, text.Width), top, text.Width, text.Height);
                    imageRect = new Rectangle(AlignX(bounds, image.Width), textRect.Bottom + ImageTextGap, image.Width, image.Height);
                    break;
                }

                default: // Down
                {
                    int totalH = image.Height + ImageTextGap + text.Height;
                    int top = bounds.Y + (bounds.Height - totalH) / 2;
                    imageRect = new Rectangle(AlignX(bounds, image.Width), top, image.Width, image.Height);
                    textRect = new Rectangle(AlignX(bounds, text.Width), imageRect.Bottom + ImageTextGap, text.Width, text.Height);
                    break;
                }
            }
        }

        // X for a run of the given width under the current ContentAlignment. Padding insets the
        // Left/Right anchored edges (centred content ignores it, as before). Anchored placement
        // also stands off by the feedback grow + 1px — ApplyFeedbackSize caps growth by the
        // smallest gap to the control edge, so a flush-planted icon would never grow at all.
        private int AlignX(Rectangle bounds, int width) => _contentAlignment switch
        {
            HorizontalAlignment.Left => bounds.X + Padding.Left + GrowInset,
            HorizontalAlignment.Right => bounds.Right - Padding.Right - width - GrowInset,
            _ => bounds.X + (bounds.Width - width) / 2
        };

        // Room the anchored edge reserves for the largest positive grow (hover or pressed).
        private int GrowInset => Math.Max(0, Math.Max(_hoveredSize, _pressedSize)) + 1;

        // Applies the hover/pressed size delta, grown/shrunk from the centre. A positive delta is
        // capped so the image never spills past the control; a negative delta just shrinks.
        private Rectangle ApplyFeedbackSize(Rectangle imageRect)
        {
            int delta = _isPressed ? _pressedSize : _isHovered ? _hoveredSize : 0;
            if (delta == 0 || !Enabled)
            {
                return imageRect;
            }

            if (delta > 0)
            {
                // Inflate is symmetric (delta/2 grows on EVERY side), so the binding limit is the
                // smallest gap between the image's DISPLAYED rect (offset included) and the control
                // edge — not the total (control − image) slack, which only held for a centred image
                // and let Up/Down layouts (or an ImageOffset) clip on the tight side.
                Rectangle c = ClientRectangle;
                int gapLeft = imageRect.Left - c.Left;
                int gapRight = c.Right - imageRect.Right;
                int gapTop = imageRect.Top - c.Top;
                int gapBottom = c.Bottom - imageRect.Bottom;
                int minGap = Math.Max(0, Math.Min(Math.Min(gapLeft, gapRight), Math.Min(gapTop, gapBottom)));
                delta = Math.Min(delta, minGap * 2);
            }
            else
            {
                // Don't let a shrink collapse the image below 1px.
                int maxShrink = Math.Max(0, Math.Min(imageRect.Width, imageRect.Height) - 1);
                delta = -Math.Min(-delta, maxShrink);
            }

            Rectangle grown = imageRect;
            grown.Inflate(delta / 2, delta / 2);
            return grown;
        }

        private void DrawImage(Graphics g, Image image, Rectangle dest)
        {
            // The authored disabled image is drawn untouched; only a borrowed face gets greyed.
            if (!Enabled && _imageDisabled == null)
            {
                DrawDisabledImage(g, image, dest);
                return;
            }

            int brightness = _isPressed ? _pressedBrightness : _isHovered ? _hoveredBrightness : 0;

            if (brightness == 0)
            {
                g.DrawImage(image, dest);
                return;
            }

            float shift = brightness / 100f;
            var matrix = new ColorMatrix(new[]
            {
                new[] { 1f, 0, 0, 0, 0 },
                new[] { 0, 1f, 0, 0, 0 },
                new[] { 0, 0, 1f, 0, 0 },
                new[] { 0, 0, 0, 1f, 0 },
                new[] { shift, shift, shift, 0, 1f }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }

        // Greyed-out + translucent look for the disabled state: luminance desaturation to
        // drain the colour, a small lift so dark outlines don't read as black, and reduced
        // alpha for the faded feel. (The old path applied a negative brightness shift, which
        // just darkened the icon instead of fading it.)
        // Internal: the ARP v2 virtual renderer draws through this so a virtualized icon button
        // is pixel-identical to a real one when disabled (luminance grayscale, not a darken).
        internal static void DrawDisabledImage(Graphics g, Image image, Rectangle dest)
        {
            var matrix = new ColorMatrix(new[]
            {
                new[] { 0.21f, 0.21f, 0.21f, 0, 0 },
                new[] { 0.72f, 0.72f, 0.72f, 0, 0 },
                new[] { 0.07f, 0.07f, 0.07f, 0, 0 },
                new[] { 0, 0, 0, 0.5f, 0 },
                new[] { 0.15f, 0.15f, 0.15f, 0, 1f }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
    }
}
