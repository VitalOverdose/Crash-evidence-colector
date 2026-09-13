#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>Vertical placement for a RoundedTextBox's right-hand buttons.</summary>
    public enum RightButtonAlign
    {
        Middle,
        Top,
        Bottom,
    }

    /// <summary>
    /// RoundedTextBox
    /// -------------------------------------------------------------------------
    /// Custom WinForms textbox implemented as a UserControl with:
    /// - Rounded border and background
    /// - Focus + hover visuals
    /// - Optional left / right icons
    /// - Placeholder text
    ///
    /// IMPORTANT (Designer):
    /// ---------------------
    /// This control explicitly exposes overridden base properties (Text, Font,
    /// BackColor) so they appear correctly in the WinForms designer.
    /// </summary>
    [ToolboxItem(true)]
    [DesignerCategory("Code")] // Prevents WinForms designer from hiding properties
    [Designer(typeof(RoundedTextBoxDesigner))]
    public class RoundedTextBox : UserControl, IAdaptiveRowPanelItem
    {
        // ============================================================================
        // CORE COMPOSITION
        // ============================================================================

        // Container panel responsible for rounded drawing
        private readonly RoundedContainerPanel container;

        // Clips the native TextBox if DPI/font sizing makes it taller than the rounded shell.
        private readonly Panel textBoxHost;

        // Actual WinForms TextBox used for input
        private readonly TextBox textBox;

        // Draws placeholder text above the hosted TextBox when native cue banners are hidden.
        private readonly Label placeholderLabel;

        // Optional icon holders
        private PictureBox? _leftIconBox;
        private PictureBox? _rightIconBox;
        private PictureBox? _rightIconBox2;
        private int _rightButtonAOffset;
        private int _rightButtonBOffset;

        // ============================================================================
        // STYLE / STATE
        // ============================================================================

        private int _cornerRadius = 10;
        private Color _borderColor = Color.FromArgb(200, 210, 220);
        private Color _focusColor = Color.FromArgb(150, 180, 255);
        private Color _backColor = Color.White;
        private Color _hoverBackColor = Color.FromArgb(245, 250, 255);
        private bool _flatLabelMode = false;
        private int _rightButtonAHoverGrow = 0;
        private int _rightButtonBHoverGrow = 0;
        private Size _buttonImageSize = Size.Empty;
        private Size _rightButtonAImageSize = Size.Empty;
        private Size _rightButtonBImageSize = Size.Empty;
        private const int IconHoverGrow = 2;

        // Scaled / disabled copies we made ourselves and must dispose. Never the caller's images.
        private readonly Dictionary<PictureBox, Image> _ownedIconImages = new();

        private bool _isHovered = false;

        private Image? _leftIcon;
        private Image? _rightIcon;
        private Image? _rightIconHover;
        private Image? _rightIconPressed;
        private Image? _rightIcon2;
        private Image? _rightIcon2Hover;
        private Image? _rightIcon2Pressed;
        private RightButtonAlign _rightButtonAlignment = RightButtonAlign.Middle;
        private bool _rightButtonAEnabled = true;
        private bool _rightButtonBEnabled = true;
        private Size _iconSize = new Size(24, 24);

        private bool _multiline = false;
        private ScrollBars _scrollBars = ScrollBars.None;
        private bool _wordWrap = true;

        private bool _selectAllOnFocus = false;
        private bool _pendingSelectAll = false;

        private string _placeholderText = "";
        private Color _placeholderColor = Color.Gray;
        private Form? _dpiSourceForm;

        // EM_SETCUEBANNER: sets native placeholder text on the inner TextBox handle
        private const int EM_SETCUEBANNER = 0x1501;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, string lParam);

        private void ApplyNativePlaceholder()
        {
            if (textBox.IsHandleCreated)
                SendMessage(textBox.Handle, EM_SETCUEBANNER, false, _placeholderText ?? "");
        }

        // ============================================================================
        // CONSTRUCTOR
        // ============================================================================

        public RoundedTextBox()
        {
            // Enable high-quality repainting
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            // --------------------------------------------------------------------
            // CONTAINER PANEL
            // --------------------------------------------------------------------
            container = new RoundedContainerPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            container.Paint += Container_Paint;
            container.Resize += (_, _) => RepositionTextBox();

            textBoxHost = new Panel
            {
                BackColor = _backColor,
                Margin = new Padding(0)
            };

            // --------------------------------------------------------------------
            // INNER TEXTBOX
            // --------------------------------------------------------------------
            textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.None,
                Font = Font,
                BackColor = _backColor,
                ForeColor = Color.Black,
                Margin = new Padding(0)
            };

            placeholderLabel = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = _placeholderColor,
                Font = Font,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Visible = false
            };
            placeholderLabel.Click += (_, _) => textBox.Focus();

            ApplyTextCursor();

            // Compose hierarchy
            textBoxHost.Controls.Add(textBox);
            textBoxHost.Controls.Add(placeholderLabel);
            container.Controls.Add(textBoxHost);
            Controls.Add(container);

            // --------------------------------------------------------------------
            // EVENT WIRING
            // --------------------------------------------------------------------
            textBox.HandleCreated += (_, _) => ApplyNativePlaceholder();
            textBox.GotFocus += (_, _) =>
            {
                RefreshVisualState();

                // Address-bar behaviour: taking focus selects the lot, so typing replaces it.
                // The click that GAVE focus lands after this and drops a caret, wiping the
                // selection -- hence the pending flag, consumed by the MouseUp below. Clicks
                // once the box is ALREADY focused don't set it, so the caret can still be
                // placed by hand on the second click.
                if (_selectAllOnFocus)
                {
                    _pendingSelectAll = true;
                    textBox.SelectAll();
                }
            };
            textBox.MouseUp += (_, _) =>
            {
                if (_pendingSelectAll)
                {
                    _pendingSelectAll = false;
                    textBox.SelectAll();
                }
            };
            textBox.LostFocus += (_, _) =>
            {
                _pendingSelectAll = false;
                RefreshVisualState();
            };
            textBox.MouseEnter += (_, _) => RefreshVisualState();
            textBox.MouseLeave += (_, _) => RefreshVisualState();

            // Double-clicks land on whichever inner part was hit; re-raise them as the
            // composite's own so a host can subscribe to MouseDoubleClick as on any control.
            textBox.MouseDoubleClick += (_, e) => OnMouseDoubleClick(
                new MouseEventArgs(e.Button, e.Clicks, e.X + textBoxHost.Left + textBox.Left, e.Y + textBoxHost.Top + textBox.Top, e.Delta));
            textBoxHost.MouseDoubleClick += (_, e) => OnMouseDoubleClick(
                new MouseEventArgs(e.Button, e.Clicks, e.X + textBoxHost.Left, e.Y + textBoxHost.Top, e.Delta));
            container.MouseDoubleClick += (_, e) => OnMouseDoubleClick(e);
            textBox.TextChanged += (_, _) =>
            {
                if (!string.Equals(base.Text, textBox.Text, StringComparison.Ordinal))
                {
                    base.Text = textBox.Text;
                }
                OnTextChanged(EventArgs.Empty);   // surface inner-textbox edits so the public TextChanged actually fires
                UpdatePlaceholderOverlay();
                container.Invalidate();
            };
            textBox.KeyDown += (_, e) => OnKeyDown(e);     // surface inner-textbox keys (e.g. Enter)
            textBox.KeyPress += (_, e) => OnKeyPress(e);
            textBoxHost.MouseEnter += (_, _) => RefreshVisualState();
            textBoxHost.MouseLeave += (_, _) => RefreshVisualState();
            container.MouseEnter += (_, _) => RefreshVisualState();
            container.MouseLeave += (_, _) => RefreshVisualState();
            MouseEnter += (_, _) => RefreshVisualState();
            MouseLeave += (_, _) => RefreshVisualState();

            // Behaviour sync
            textBox.Multiline = _multiline;
            textBox.ScrollBars = _scrollBars;
            textBox.WordWrap = _wordWrap;
            textBox.AcceptsReturn = _multiline;
            textBox.AcceptsTab = false;

            AutoSize = false;
            Size = new Size(200, 35);

            base.BackColor = Color.White;

            // Initial vertical alignment
            UpdateInnerSurfaceColor();
            RepositionTextBox();
            UpdatePlaceholderOverlay();

            Enter += (_, _) => Invalidate();
            Leave += (_, _) => Invalidate();
            TextChanged += (_, _) => Invalidate();
        }

        protected Control InnerContainer => container;
        protected Control InnerTextBoxHost => textBoxHost;
        protected TextBox InnerTextBox => textBox;

        private bool _useTextCursor = true;

        /// <summary>
        /// Shows the I-beam over the whole text zone (host panel, placeholder overlay and the
        /// inner TextBox) so the control reads as "type here". Turn OFF when the control is a
        /// status readout or drag surface rather than an input field — those must not
        /// advertise text input.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool UseTextCursor
        {
            get => _useTextCursor;
            set
            {
                if (_useTextCursor == value) return;
                _useTextCursor = value;
                ApplyTextCursor();
            }
        }

        private void ApplyTextCursor()
        {
            Cursor cursor = _useTextCursor ? Cursors.IBeam : Cursors.Default;
            textBoxHost.Cursor = cursor;
            placeholderLabel.Cursor = cursor;
            textBox.Cursor = cursor;
        }

        // ============================================================================
        // DESIGNER-SAFE OVERRIDDEN BASE PROPERTIES
        // ============================================================================

        /// <summary>
        /// Selects all text inside the inner TextBox.
        /// This mirrors TextBox.SelectAll() so external code
        /// can treat RoundedTextBox like a normal TextBox.
        /// </summary>
        public void SelectAll()
        {
            // Defensive check in case the control is accessed during design-time
            if (textBox != null)
                textBox.SelectAll();
        }

        /// <summary>
        /// Exposes Text property cleanly to the WinForms designer.
        /// </summary>
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        #pragma warning disable CS8765
        public override string Text
        {
            get => textBox.Text;
            set
            {
                string newValue = value ?? string.Empty;
                if (string.Equals(textBox.Text, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                textBox.Text = newValue;
            }
        }
    

        /// <summary>
        /// Keeps font synchronized and visible at design-time.
        /// </summary>
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]

        public override Font Font
        {
            get => base.Font;
            set
            {
                base.Font = value;
                SyncInnerFonts();
            }
        }

        // The inner TextBox snapshots the font at construction, so EVERY later font
        // change must be ferried across manually. The property setter above only
        // covers explicit sets — ambient inheritance changes and the framework's
        // per-monitor DPI font rescaling arrive through base.Font and fire
        // OnFontChanged instead. Missing this hook left inner text at the ambient
        // size while the shell (and its neighbours) moved — the 150% mystery.
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            SyncInnerFonts();
        }

        private void SyncInnerFonts()
        {
            if (textBox == null)
                return;     // construction-time font events arrive before the inner controls exist

            if (!Equals(textBox.Font, Font))
                textBox.Font = Font;

            if (placeholderLabel != null && !Equals(placeholderLabel.Font, Font))
                placeholderLabel.Font = Font;

            RepositionTextBox();
            Invalidate();
            container?.PerformLayout();
        }
        #pragma warning restore CS8765

        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override Color ForeColor
        {
            get => base.ForeColor;
            set
            {
                base.ForeColor = value;

                if (textBox != null)
                    textBox.ForeColor = value;

                Invalidate();
            }
        }

        /// <summary>
        /// BackColor exists only for designer compatibility.
        /// Actual background is custom rendered.
        /// </summary>
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override Color BackColor
        {
            get => base.BackColor;
            set
            {
                base.BackColor = value;
                container.BackColor = Color.Transparent;
                Invalidate();
            }
        }

        /// <summary>
        /// Keeps the embedded TextBox aligned when the parent form DPI changes.
        /// </summary>
        private void AttachToParentFormDpiChanged()
        {
            Form? currentForm = FindForm();
            if (ReferenceEquals(_dpiSourceForm, currentForm)) return;

            if (_dpiSourceForm != null)
                _dpiSourceForm.DpiChanged -= ParentForm_DpiChanged;

            _dpiSourceForm = currentForm;

            if (_dpiSourceForm != null)
                _dpiSourceForm.DpiChanged += ParentForm_DpiChanged;
        }

        private void ParentForm_DpiChanged(object? sender, DpiChangedEventArgs e)
        {
            RepositionTextBox();
            UpdateRoundedRegions();
            Invalidate();

            BeginInvoke(new Action(() =>
            {
                RepositionTextBox();
                Invalidate();
            }));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AttachToParentFormDpiChanged();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            AttachToParentFormDpiChanged();

            // Flat mode borrows the parent's colour, so it must follow the parent around.
            UpdateInnerSurfaceColor();
            container?.Invalidate();
        }

        // ============================================================================
        // HIDDEN TEXT SELECTION PROPERTIES (DESIGNER SAFETY)
        // ============================================================================

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Address-bar behaviour: focusing the box selects all its text, so typing replaces it. Clicking again once focused still places the caret normally.")]
        public bool SelectAllOnFocus
        {
            get => _selectAllOnFocus;
            set => _selectAllOnFocus = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionStart
        {
            get => textBox.SelectionStart;
            set => textBox.SelectionStart = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionLength
        {
            get => textBox.SelectionLength;
            set => textBox.SelectionLength = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TextLength => textBox.TextLength;

        // ============================================================================
        // APPEARANCE / BEHAVIOUR PROPERTIES (DESIGNER VISIBLE)
        // ============================================================================

        [Browsable(true)]
        [Category("Appearance")]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(1, value);
                UpdateRoundedRegions();
                container.Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                container.Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        public Color FocusColor
        {
            get => _focusColor;
            set
            {
                _focusColor = value;
                container.Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        public Color HoverBackColor
        {
            get => _hoverBackColor;
            set
            {
                _hoverBackColor = value;
                UpdateInnerSurfaceColor();
                container.Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        public Color TextBoxBackColor
        {
            // The STORED value, never the live TextBox colour: in flat mode (and while
            // hovered) the surface rule stamps a different colour on the TextBox, and reading
            // that back made the property grid revert every edit the moment it was typed.
            get => _backColor;
            set
            {
                _backColor = value;
                UpdateInnerSurfaceColor();
                container.Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Paints the box as a flat label: the border, focus ring and hover colour all follow TextBoxBackColor, so only that one colour needs setting to blend into a panel. BorderColor, FocusColor and HoverBackColor are ignored while this is on. The text stays selectable and scrollable.")]
        public bool FlatLabelMode
        {
            get => _flatLabelMode;
            set
            {
                if (_flatLabelMode == value)
                    return;

                _flatLabelMode = value;
                UpdateInnerSurfaceColor();
                container.Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Pixels RightButtonA's image grows by while the mouse is over it (0 = no grow). It drops back to normal size while pressed. Growth is skipped if it would not fit inside the icon box, so leave room via RightButtonAImageSize/ButtonImageSize. Does not affect LeftIcon.")]
        public int RightButtonAHoverGrow
        {
            get => _rightButtonAHoverGrow;
            set
            {
                int newValue = Math.Max(0, value);
                if (_rightButtonAHoverGrow == newValue)
                    return;

                _rightButtonAHoverGrow = newValue;
                UpdateIcons();          // redraw at rest size — never leave one stuck grown
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Pixels RightButtonB's image grows by while the mouse is over it (0 = no grow). It drops back to normal size while pressed. Growth is skipped if it would not fit inside the icon box, so leave room via RightButtonBImageSize/ButtonImageSize. Does not affect LeftIcon.")]
        public int RightButtonBHoverGrow
        {
            get => _rightButtonBHoverGrow;
            set
            {
                int newValue = Math.Max(0, value);
                if (_rightButtonBHoverGrow == newValue)
                    return;

                _rightButtonBHoverGrow = newValue;
                UpdateIcons();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Size the RightButtonA/RightButtonB images are drawn at inside their icon box. Leave empty to use each image's own size. Setting this smaller than IconSize is what leaves room for HoverGrowsImage to expand into.")]
        public Size ButtonImageSize
        {
            get => _buttonImageSize;
            set
            {
                if (_buttonImageSize == value)
                    return;

                _buttonImageSize = value;
                UpdateIcons();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Size the RightButtonA image is drawn at, overriding ButtonImageSize for this button only. Leave empty to fall back to ButtonImageSize, then the image's own size.")]
        public Size RightButtonAImageSize
        {
            get => _rightButtonAImageSize;
            set
            {
                if (_rightButtonAImageSize == value)
                    return;

                _rightButtonAImageSize = value;
                UpdateIcons();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Size the RightButtonB image is drawn at, overriding ButtonImageSize for this button only. Leave empty to fall back to ButtonImageSize, then the image's own size.")]
        public Size RightButtonBImageSize
        {
            get => _rightButtonBImageSize;
            set
            {
                if (_rightButtonBImageSize == value)
                    return;

                _rightButtonBImageSize = value;
                UpdateIcons();
            }
        }

        // In label mode the surface follows the PARENT's colour automatically (the whole point
        // of blending — no colour to hand-match, and it tracks theme changes for free). The
        // hover colour is ignored there, so the surface never changes under the cursor.
        // ONE rule for the interior colour. Body fill, inner TextBox, its host panel and the
        // placeholder all read it, so they cannot disagree; a subclass with more states
        // (disabled, selected...) overrides GetSurfaceColor and every surface follows.
        protected Color CurrentSurfaceColor => GetSurfaceColor(_isHovered);

        /// <summary>Whether the pointer is over the control, as the surface colour sees it.</summary>
        protected bool IsHovered => _isHovered;

        /// <summary>
        /// The interior colour for a hover state. Flat mode borrows the nearest opaque parent
        /// colour; otherwise HoverBackColor while hovered, else TextBoxBackColor.
        /// </summary>
        protected virtual Color GetSurfaceColor(bool hovered) =>
            _flatLabelMode
                ? GetEffectiveParentBackColor()
                : hovered ? _hoverBackColor : _backColor;

        /// <summary>
        /// The colour of the editor strip itself. Same as the body by default - a subclass may
        /// give keyboard focus its own colour here without touching the body.
        /// </summary>
        protected virtual Color GetEditorSurfaceColor(bool hovered, bool focused) => GetSurfaceColor(hovered);

        /// <summary>The nearest opaque ancestor BackColor — the solid colour that best stands
        /// in for the surface behind this control (the inner TextBox cannot be transparent).</summary>
        protected Color GetEffectiveParentBackColor()
        {
            for (Control? p = Parent; p != null; p = p.Parent)
            {
                if (p.BackColor.A == 255)
                    return p.BackColor;
            }

            return SystemColors.Control;
        }

        /// <summary>
        /// Re-reads hover, re-stamps the editor colour and repaints. Called on every state
        /// change that can move the surface colour (hover, focus, enabled, subclass states).
        /// The stamp is unconditional: the colour can change without hover changing.
        /// </summary>
        protected void RefreshVisualState()
        {
            if (container == null || container.IsDisposed)
                return;

            _isHovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
            UpdateInnerSurfaceColor();
            container.Invalidate();
        }

        // Flat mode borrows the parent's colour, so it must follow the parent around.
        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);

            // Flat mode borrows the parent's colour, so it must follow colour changes too.
            UpdateInnerSurfaceColor();
            container?.Invalidate();
        }

        protected void UpdateInnerSurfaceColor()
        {
            if (textBox == null || textBox.IsDisposed)
                return;

            Color surfaceColor = GetEditorSurfaceColor(_isHovered, textBox.Focused);

            if (textBoxHost != null && !textBoxHost.IsDisposed && textBoxHost.BackColor != surfaceColor)
                textBoxHost.BackColor = surfaceColor;

            if (textBox.BackColor != surfaceColor)
                textBox.BackColor = surfaceColor;

            if (placeholderLabel != null && !placeholderLabel.IsDisposed && placeholderLabel.BackColor != surfaceColor)
                placeholderLabel.BackColor = surfaceColor;
        }

        [Browsable(true)]
        [Category("Appearance")]
        public string PlaceholderText
        {
            get => _placeholderText;
            set
            {
                _placeholderText = value;
                placeholderLabel.Text = value;
                ApplyNativePlaceholder();
                UpdatePlaceholderOverlay();
                Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        public Image? LeftIcon
        {
            get => _leftIcon;
            set
            {
                _leftIcon = value;
                UpdateIcons();
            }
        }

        // Legacy alias for the outer-right slot. Kept (hidden) so existing designer files
        // that serialize ".RightIcon = null" still compile. New code: use RightButtonA.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image? RightIcon
        {
            get => _rightIcon;
            set { _rightIcon = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Right-side button image (outermost). Clickable -> RightButtonAClicked; disable via RightButtonAEnabled.")]
        public Image? RightButtonA
        {
            get => _rightIcon;
            set { _rightIcon = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("RightButtonA image shown while the mouse is over it. Falls back to RightButtonA if null.")]
        public Image? RightButtonAHover
        {
            get => _rightIconHover;
            set { _rightIconHover = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("RightButtonA image shown while it is pressed. Falls back to RightButtonAHover/RightButtonA if null.")]
        public Image? RightButtonAPressed
        {
            get => _rightIconPressed;
            set { _rightIconPressed = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Second right-side button image, drawn to the LEFT of RightButtonA. Clickable -> RightButtonBClicked.")]
        public Image? RightButtonB
        {
            get => _rightIcon2;
            set { _rightIcon2 = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("RightButtonB image shown while the mouse is over it. Falls back to RightButtonB if null.")]
        public Image? RightButtonBHover
        {
            get => _rightIcon2Hover;
            set { _rightIcon2Hover = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("RightButtonB image shown while it is pressed. Falls back to RightButtonBHover/RightButtonB if null.")]
        public Image? RightButtonBPressed
        {
            get => _rightIcon2Pressed;
            set { _rightIcon2Pressed = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("When false, RightButtonA renders greyscale at 75% opacity and its click event does not fire.")]
        public bool RightButtonAEnabled
        {
            get => _rightButtonAEnabled;
            set { if (_rightButtonAEnabled == value) return; _rightButtonAEnabled = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("When false, RightButtonB renders greyscale at 75% opacity and its click event does not fire.")]
        public bool RightButtonBEnabled
        {
            get => _rightButtonBEnabled;
            set { if (_rightButtonBEnabled == value) return; _rightButtonBEnabled = value; UpdateIcons(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Extra pixels to push RightButtonA (outermost) inward from the right edge. Default 0.")]
        public int RightButtonAOffset
        {
            get => _rightButtonAOffset;
            set { value = Math.Max(0, value); if (_rightButtonAOffset == value) return; _rightButtonAOffset = value; ApplyRightButtonOffsets(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Extra pixels to push RightButtonB (inner, left of A) further inward. Default 0.")]
        public int RightButtonBOffset
        {
            get => _rightButtonBOffset;
            set { value = Math.Max(0, value); if (_rightButtonBOffset == value) return; _rightButtonBOffset = value; ApplyRightButtonOffsets(); }
        }

        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(RightButtonAlign.Middle)]
        [Description("Where RightButtonA/B sit vertically. Middle centres them (the default, and "
            + "the only sensible answer for a single-line box). Top keeps them flush with the top "
            + "edge, which is what a box that grows tall — a multiline log pane — wants: centred "
            + "buttons would drift into the middle of the text as it grows.")]
        public RightButtonAlign RightButtonAlignment
        {
            get => _rightButtonAlignment;
            set
            {
                if (_rightButtonAlignment == value) return;
                _rightButtonAlignment = value;
                ApplyRightButtonOffsets();
            }
        }

        /// <summary>Raised when the left icon is clicked.</summary>
        public event EventHandler? LeftIconClicked;
        /// <summary>Raised when RightButtonA (outermost) is clicked while enabled.</summary>
        public event EventHandler? RightButtonAClicked;
        /// <summary>Raised when RightButtonB (left of A) is clicked while enabled.</summary>
        public event EventHandler? RightButtonBClicked;

        [Browsable(true)]
        [Category("Appearance")]
        public Size IconSize
        {
            get => _iconSize;
            set
            {
                _iconSize = value;
                UpdateIcons();
            }
        }

        [Browsable(true)]
        [Category("Behavior")]
        public bool Multiline
        {
            get => _multiline;
            set
            {
                if (_multiline == value) return;

                _multiline = value;
                textBox.Multiline = value;
                textBox.AcceptsReturn = value;

                RepositionTextBox();
                container.PerformLayout();
                Invalidate();
            }
        }

        [Browsable(true)]
        [Category("Behavior")]
        public ScrollBars ScrollBars
        {
            get => _scrollBars;
            set
            {
                _scrollBars = value;
                textBox.ScrollBars = value;
            }
        }

        [Browsable(true)]
        [Category("Behavior")]
        public bool WordWrap
        {
            get => _wordWrap;
            set
            {
                _wordWrap = value;
                textBox.WordWrap = value;
            }
        }

        [Browsable(true)]
        [Category("Behavior")]
        public bool ReadOnly
        {
            get => textBox.ReadOnly;
            set => textBox.ReadOnly = value;
        }

        // ============================================================================
        // ICON MANAGEMENT
        // ============================================================================

        private PictureBox CreateIconBox(DockStyle dock, EventHandler onClick)
        {
            var box = new PictureBox
            {
                Dock = dock,
                Size = _iconSize,
                MinimumSize = _iconSize,
                Margin = new Padding(4, 0, 4, 0),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            box.Click += onClick;
            return box;
        }

        // Render size = the per-button size when set, else ButtonImageSize, else the image's own
        // size. On hover it adds that button's own grow value, stepping down until it fits — the
        // icon box never moves or resizes, so growth can't disturb the layout or its neighbour.
        private Size ResolveIconRenderSize(Image source, PictureBox box, bool grown)
        {
            bool isA = ReferenceEquals(box, _rightIconBox);
            bool isB = ReferenceEquals(box, _rightIconBox2);

            // Per-button size wins, then the shared ButtonImageSize, then the image's own size.
            Size perButton = isA ? _rightButtonAImageSize
                : isB ? _rightButtonBImageSize
                : Size.Empty;

            Size baseSize = !perButton.IsEmpty ? perButton
                : _buttonImageSize.IsEmpty ? source.Size : _buttonImageSize;

            int grow = isA ? _rightButtonAHoverGrow
                : isB ? _rightButtonBHoverGrow
                : 0;

            if (!grown || grow <= 0)
                return baseSize;

            // Try the requested growth, then smaller steps, so a value that doesn't quite fit
            // still gives some feedback instead of none.
            for (int step = grow; step >= 1; step--)
            {
                Size candidate = new Size(baseSize.Width + step, baseSize.Height + step);
                if (candidate.Width <= box.Width && candidate.Height <= box.Height)
                    return candidate;
            }

            return baseSize;
        }

        private static Image ScaleIconImage(Image source, Size size)
        {
            var bmp = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(Point.Empty, size));
            return bmp;
        }

        // Central place every right-button image goes through, so scaling, the disabled copy and
        // the hover grow can't disagree. Copies we create are tracked and disposed on replacement;
        // the caller's own source images are never disposed.
        private void ApplyIconImage(PictureBox box, Image? source, bool enabled, bool grown)
        {
            if (source == null)
            {
                SetBoxImage(box, null, owned: false);
                return;
            }

            Size target = ResolveIconRenderSize(source, box, grown);

            if (enabled && target == source.Size)
            {
                SetBoxImage(box, source, owned: false);
                return;
            }

            Image render = enabled ? source : MakeDisabledImage(source);
            bool renderOwned = !enabled;

            if (target != render.Size)
            {
                Image scaled = ScaleIconImage(render, target);
                if (renderOwned)
                    render.Dispose();

                render = scaled;
                renderOwned = true;
            }

            SetBoxImage(box, render, renderOwned);
        }

        private void SetBoxImage(PictureBox box, Image? image, bool owned)
        {
            if (_ownedIconImages.TryGetValue(box, out Image? previous))
            {
                _ownedIconImages.Remove(box);
                if (!ReferenceEquals(previous, image))
                    previous.Dispose();
            }

            box.Image = image;

            if (owned && image != null)
                _ownedIconImages[box] = image;
        }

        // Right buttons are positioned manually (not docked) so the per-button offset can
        // actually push them inward — WinForms dock layout ignores Margin.
        private void ApplyRightButtonOffsets()
        {
            RepositionTextBox();
            container.Invalidate();
        }

        // Places RightButtonA (outermost) then RightButtonB (left of A) from the right edge,
        // each pushed inward by its offset, vertically centred. Offset 0 == flush right.
        private void PositionRightIcons()
        {
            if (container == null)
                return;

            Rectangle disp = container.DisplayRectangle;
            int rightEdge = disp.Right;

            if (_rightIconBox != null)
            {
                int x = rightEdge - _rightButtonAOffset - _rightIconBox.Width;
                _rightIconBox.Location = new Point(x, IconTop(disp, _rightIconBox.Height));
                rightEdge = _rightIconBox.Left;
            }

            if (_rightIconBox2 != null)
            {
                int x = rightEdge - _rightButtonBOffset - _rightIconBox2.Width;
                _rightIconBox2.Location = new Point(x, IconTop(disp, _rightIconBox2.Height));
            }
        }

        private int IconTop(Rectangle disp, int iconHeight) => _rightButtonAlignment switch
        {
            RightButtonAlign.Top => disp.Top,
            RightButtonAlign.Bottom => disp.Top + Math.Max(0, disp.Height - iconHeight),
            _ => disp.Top + Math.Max(0, (disp.Height - iconHeight) / 2),
        };

        // Swaps a box's image between normal / hover / pressed states. Hover and pressed
        // fall back to the normal image when not supplied. No swapping while disabled
        // (the disabled greyscale image set in UpdateIcons stays put).
        private void WireIconStates(PictureBox box, Func<Image?> normal, Func<Image?> hover, Func<Image?> pressed, Func<bool> enabled)
        {
            box.MouseEnter += (s, e) => { if (enabled()) ApplyIconImage(box, hover() ?? normal(), true, grown: true); };
            box.MouseLeave += (s, e) => ApplyIconImage(box, normal(), enabled(), grown: false);
            // Pressed drops back to rest size, so the icon reads as being pushed in.
            box.MouseDown  += (s, e) => { if (enabled()) ApplyIconImage(box, pressed() ?? hover() ?? normal(), true, grown: false); };
            box.MouseUp    += (s, e) =>
            {
                if (!enabled()) return;
                bool over = box.ClientRectangle.Contains(box.PointToClient(Cursor.Position));
                ApplyIconImage(box, over ? (hover() ?? normal()) : normal(), true, grown: over);
            };
        }

        private void UpdateIcons()
        {
            if (_leftIcon == null && _leftIconBox != null)
            {
                container.Controls.Remove(_leftIconBox);
                _leftIconBox.Dispose();
                _leftIconBox = null;
            }
            else if (_leftIcon != null)
            {
                _leftIconBox ??= CreateIconBox(DockStyle.Left, (s, e) => LeftIconClicked?.Invoke(this, EventArgs.Empty));
                _leftIconBox.Image = _leftIcon;
                container.Controls.Add(_leftIconBox);
                _leftIconBox.BringToFront();
            }

            // Second right icon sits to the LEFT of RightIcon — handled first, so that
            // RightIcon (brought to front last below) ends up at the right edge.
            if (_rightIcon2 == null && _rightIconBox2 != null)
            {
                container.Controls.Remove(_rightIconBox2);
                _rightIconBox2.Dispose();
                _rightIconBox2 = null;
            }
            else if (_rightIcon2 != null)
            {
                if (_rightIconBox2 == null)
                {
                    _rightIconBox2 = CreateIconBox(DockStyle.None, (s, e) => { if (_rightButtonBEnabled) RightButtonBClicked?.Invoke(this, EventArgs.Empty); });
                    WireIconStates(_rightIconBox2, () => _rightIcon2, () => _rightIcon2Hover, () => _rightIcon2Pressed, () => _rightButtonBEnabled);
                }
                ApplyIconImage(_rightIconBox2, _rightIcon2, _rightButtonBEnabled, grown: false);
                _rightIconBox2.Cursor = _rightButtonBEnabled ? Cursors.Hand : Cursors.Default;
                container.Controls.Add(_rightIconBox2);
                _rightIconBox2.BringToFront();
            }

            if (_rightIcon == null && _rightIconBox != null)
            {
                container.Controls.Remove(_rightIconBox);
                _rightIconBox.Dispose();
                _rightIconBox = null;
            }
            else if (_rightIcon != null)
            {
                if (_rightIconBox == null)
                {
                    _rightIconBox = CreateIconBox(DockStyle.None, (s, e) => { if (_rightButtonAEnabled) RightButtonAClicked?.Invoke(this, EventArgs.Empty); });
                    WireIconStates(_rightIconBox, () => _rightIcon, () => _rightIconHover, () => _rightIconPressed, () => _rightButtonAEnabled);
                }
                ApplyIconImage(_rightIconBox, _rightIcon, _rightButtonAEnabled, grown: false);
                _rightIconBox.Cursor = _rightButtonAEnabled ? Cursors.Hand : Cursors.Default;
                container.Controls.Add(_rightIconBox);
                _rightIconBox.BringToFront();
            }

            ApplyRightButtonOffsets();
        }

        // Greyscale + 75% opacity copy of an icon, shown when its right button is disabled.
        private static Image MakeDisabledImage(Image src)
        {
            var bmp = new Bitmap(src.Width, src.Height);
            using var g = Graphics.FromImage(bmp);
            var matrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.299f, 0.299f, 0.299f, 0,     0 },
                new float[] { 0.587f, 0.587f, 0.587f, 0,     0 },
                new float[] { 0.114f, 0.114f, 0.114f, 0,     0 },
                new float[] { 0,      0,      0,      0.75f, 0 },
                new float[] { 0,      0,      0,      0,     1 }
            });
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(matrix);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            return bmp;
        }

        // ============================================================================
        // RENDERING
        // ============================================================================

        protected virtual void Container_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, container.Width - 1, container.Height - 1);

            bool isHovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (_isHovered != isHovered)
            {
                _isHovered = isHovered;
                UpdateInnerSurfaceColor();
            }

            Color bgColor = CurrentSurfaceColor;   // the body follows the SAME rule as the editor

            PaintParentSurfaceBehind(container, e.Graphics);

            // Label mode: nothing else to paint — the parent's replayed surface (gradients
            // included) IS the background, which is what blending actually means. Only the
            // inner TextBox needs a solid colour, and CurrentSurfaceColor gives it the
            // nearest opaque ancestor colour.
            if (_flatLabelMode)
                return;

            // Border width. Hardcoded rather than a property, as it always has been - but named
            // so the half-pen inset below reads as arithmetic instead of a magic number.
            const float BorderThickness = 2f;

            using GraphicsPath path = GetRoundedRect(rect, _cornerRadius);
            using SolidBrush brush = new SolidBrush(bgColor);
            using Pen pen = new Pen(textBox.Focused ? _focusColor : _borderColor, BorderThickness);

            e.Graphics.FillPath(brush, path);

            // Stroke a path inset by HALF the pen width, not the fill path itself. A pen
            // straddles its path, so stroking the fill's own edge leaves the fill's AA fringe
            // and the stroke's AA fringe each blending to the parent surface, with the backdrop
            // surviving between them as a pale ring just inside the border. At 2px - which this
            // control always is - that ring is at its most visible.
            const float inset = BorderThickness / 2f;
            RectangleF strokeBounds = RectangleF.Inflate(rect, -inset, -inset);
            float strokeRadius = Math.Max(0f, _cornerRadius - inset);

            using GraphicsPath borderPath = GetRoundedRectF(strokeBounds, strokeRadius);
            e.Graphics.DrawPath(pen, borderPath);
        }

        /// <summary>Float-precision twin of GetRoundedRect, for strokes that sit between pixels.</summary>
        protected static GraphicsPath GetRoundedRectF(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            float d = Math.Min(Math.Max(1f, radius * 2f), Math.Min(bounds.Width, bounds.Height));

            path.StartFigure();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        protected void PaintParentSurfaceBehind(Control surface, Graphics g)
        {
            Color fallbackBack = SystemColors.Control;
            Control? p = Parent;
            while (p != null)
            {
                if (p.BackColor.A == 255)
                {
                    fallbackBack = p.BackColor;
                    break;
                }

                p = p.Parent;
            }

            using (var fallbackBrush = new SolidBrush(fallbackBack))
                g.FillRectangle(fallbackBrush, 0, 0, surface.Width, surface.Height);

            if (Parent == null || Parent.IsDisposed)
                return;

            try
            {
                int parentX = Left + surface.Left;
                int parentY = Top + surface.Top;
                GraphicsState state = g.Save();
                g.TranslateTransform(-parentX, -parentY);

                using var pe = new PaintEventArgs(
                    g,
                    new Rectangle(parentX, parentY, surface.Width, surface.Height));

                InvokePaintBackground(Parent, pe);
                InvokePaint(Parent, pe);
                g.Restore(state);
            }
            catch
            {
                // The fallback fill above is enough for design-time or unusual parent surfaces.
            }
        }

        // ============================================================================
        // VERTICAL TEXT ALIGNMENT
        // ============================================================================

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RepositionTextBox();
            container.Invalidate();
        }

        protected virtual void RepositionTextBox()
        {
            if (textBox == null || textBox.IsDisposed || container == null)
                return;

            PositionRightIcons();

            int horizontalPadding = ScaleValue(8);
            int iconGap = ScaleValue(4);

            int left = horizontalPadding;
            if (_leftIconBox != null)
                left += _leftIconBox.Width + iconGap;

            int right = horizontalPadding;
            if (_rightIconBox != null)
                right += _rightIconBox.Width + iconGap;

            int width = Math.Max(0, container.ClientSize.Width - left - right);
            int borderInset = GetTextBoxHostBorderInset();
            int availableHeight = Math.Max(1, container.ClientSize.Height - (borderInset * 2));
            int height = _multiline
                ? Math.Max(1, availableHeight - (ScaleValue(6) * 2))
                : Math.Max(1, Math.Min(textBox.PreferredHeight, availableHeight));
            int top = _multiline
                ? borderInset + ScaleValue(6)
                : borderInset + Math.Max(0, (availableHeight - height) / 2);

            SetInnerTextBoxBounds(left, top, width, height);
            UpdatePlaceholderOverlay();
            container.Invalidate();
        }

        protected void SetInnerTextBoxBounds(int left, int top, int width, int height)
        {
            textBoxHost.SetBounds(left, top, width, height);
            textBox.SetBounds(0, 0, width, height);

            textBox.Top = (textBoxHost.ClientSize.Height - textBox.Height) / 2;
            placeholderLabel.SetBounds(
                textBox.Left + ScaleValue(1),
                textBox.Top,
                Math.Max(0, textBox.Width - ScaleValue(2)),
                textBox.Height);
            placeholderLabel.BringToFront();
        }

        private void UpdatePlaceholderOverlay()
        {
            if (placeholderLabel == null || placeholderLabel.IsDisposed)
                return;

            placeholderLabel.Text = _placeholderText;
            placeholderLabel.ForeColor = _placeholderColor;
            placeholderLabel.BackColor = GetEditorSurfaceColor(_isHovered, textBox.Focused);
            placeholderLabel.Visible =
                string.IsNullOrEmpty(textBox.Text) &&
                !string.IsNullOrEmpty(_placeholderText) &&
                !textBox.Focused;
        }

        protected virtual int GetTextBoxHostBorderInset()
        {
            return ScaleValue(2);
        }

        protected int ScaleValue(int value)
        {
            int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            return (int)Math.Round(value * (dpi / 96.0));
        }

        protected void UpdateRoundedRegions()
        {
            // No-op: corner fill approach replaces Region clipping.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _dpiSourceForm != null)
            {
                _dpiSourceForm.DpiChanged -= ParentForm_DpiChanged;
                _dpiSourceForm = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class RoundedContainerPanel : Panel
    {
        public RoundedContainerPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // RoundedTextBox paints the full rounded background on the container.
        }
    }
}
