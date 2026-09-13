using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// Direction a two-colour fill gradient runs. Public and generically named so the rest of
    /// the control set can adopt it rather than each control inventing its own.
    /// </summary>
    public enum RoundedGradientDirection
    {
        /// <summary>Runs left to right.</summary>
        Left,

        /// <summary>Runs bottom to top.</summary>
        Up,

        /// <summary>Runs right to left.</summary>
        Right,

        /// <summary>Runs top to bottom.</summary>
        Down
    }

    [Designer(typeof(RoundedComboBoxDesigner))]
    public class RoundedComboBox : Control, IAdaptiveRowPanelItem
    {
        private static readonly Font DefaultComboFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        // =============================================================================================
        // PRIVATE FIELDS
        // =============================================================================================
        private readonly BindingList<RoundedComboBoxItem> _items = new BindingList<RoundedComboBoxItem>();
        private int _selectedIndex = -1;
        private int _defaultSelectedIndex = -1;
        private bool _isDropDownOpen = false;
        private bool _isHovered = false;

        // Geometry Cache
        private GraphicsPath? _cachedPath;
        private Rectangle _cachedRect;
        private Size _lastSize = Size.Empty;
        private int _lastRadius;

        // Popup Form Reference
        private DropDownForm? _dropDown;
        private DropDownMessageFilter? _messageFilter;
        private int _maxVisibleRows = 10;

        // Settings
        private int _textPaddingHorizontal = 10;
        private int _itemHeight = 26;
        private int _itemImageSize = 24;
        private int _itemImageHoverGrow = 0;
        private Font _itemFont;
        private Color _separatorColor = Color.FromArgb(220, 220, 220);
        private int _dropShadowSize = 6;

        // Appearance - UPDATED DEFAULTS
        private int _cornerRadius = 10;
        private int _borderThickness = 1;
        private Color _innerColor = Color.White;  // ⭐ Changed from gray to white
        private Color _borderColor = Color.FromArgb(180, 180, 180);  // ⭐ Changed to lighter gray

        private bool _hoverPropertiesEnabled = true;
        private bool _useInnerGradient;
        private Color _innerGradientEndColor = Color.FromArgb(225, 232, 240);
        private RoundedGradientDirection _innerGradientDirection = RoundedGradientDirection.Down;
        private Color _hoverInnerColor = Color.FromArgb(245, 245, 245);
        private Color _hoverBorderColor = Color.FromArgb(0, 120, 215);  // ⭐ Blue for hover
        private int _hoverBorderThickness = 1;

        private bool _isSelected = false;
        private Color _selectedInnerColor = Color.FromArgb(230, 240, 250);
        private Color _selectedBorderColor = Color.FromArgb(0, 120, 215);
        private int _selectedBorderThickness = 2;
     

        // ⭐ NEW: Dropdown item hover colors
        private Color _itemHoverColor = Color.FromArgb(240, 244, 252);

        // Disabled color effects 
        private Color _disabledInnerColor = Color.FromArgb(250, 250, 250);
        private Color _disabledBorderColor = Color.FromArgb(220, 220, 220);
        private Color _disabledTextColor = Color.FromArgb(160, 160, 160);

        // Label support mirrors RoundedToggleSwitch: inherited Text is drawn beside the combo.
        private HorizontalAlignment _labelSide = HorizontalAlignment.Left;
        private int _textGap = 8;
        private int _comboBoxWidth = 140;
        private bool _isAdjustingDesignerComboWidth;
        private Font? _labelFont;

        // =============================================================================================
        // PUBLIC PROPERTIES
        // =============================================================================================

        [Category("Appearance")]
        [Description("Optional label text drawn beside the combo box.")]
        [DefaultValue("")]
        [AllowNull]
        public override string Text
        {
            get => base.Text;
            set => base.Text = value ?? string.Empty;
        }

        [Category("Appearance - Dropdown")]
        [Description("When true, dropdown shows all items without scrolling (overrides MaxVisibleRows)")]
        public bool ShowAllDropDownRows { get; set; } = false;

        [Category("Appearance - Dropdown")]
        [DefaultValue(false)]
        [Description("Draws a checkmark beside the selected item in the dropdown list.")]
        public bool ShowSelectedCheckmark { get; set; } = false;

        [Category("Appearance")]
        [Description("Side of the combo box on which the text label is drawn.")]
        [DefaultValue(HorizontalAlignment.Left)]
        public HorizontalAlignment LabelSide
        {
            get => _labelSide;
            set
            {
                if (_labelSide == value) return;
                _labelSide = value;
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        [Category("Layout")]
        [Description("Horizontal space between the combo box and the text label.")]
        [DefaultValue(8)]
        public int TextGap
        {
            get => _textGap;
            set
            {
                int newValue = Math.Max(0, value);
                if (_textGap == newValue) return;
                _textGap = newValue;
                EnsureMinimumControlSize();
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        [Category("Layout")]
        [Description("Width of the rounded combo box portion when Text is used as an outer label.")]
        [DefaultValue(140)]
        public int ComboBoxWidth
        {
            get => _comboBoxWidth;
            set
            {
                int newValue = Math.Max(40, value);
                if (_comboBoxWidth == newValue) return;
                _comboBoxWidth = newValue;
                EnsureMinimumControlSize();
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        [Category("Layout")]
        [Description("Horizontal padding inside the control, between the border and the content on each side.")]
        [DefaultValue(10)]
        public int TextPaddingHorizontal
        {
            get => _textPaddingHorizontal;
            set
            {
                int newValue = Math.Max(0, value);
                if (_textPaddingHorizontal == newValue) return;
                _textPaddingHorizontal = newValue;
                EnsureMinimumControlSize();
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        [Category("Layout")]
        [Description("Width of the clickable dropdown-arrow zone on the right of the control.")]
        [DefaultValue(24)]
        public int ArrowZoneWidth
        {
            get => _arrowZoneWidth;
            set
            {
                int newValue = Math.Max(0, value);
                if (_arrowZoneWidth == newValue) return;
                _arrowZoneWidth = newValue;
                EnsureMinimumControlSize();
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Font used for the text label beside the combo. Falls back to the control Font if not set.")]
        [DefaultValue(null)]
        public Font? LabelFont
        {
            get => _labelFont;
            set
            {
                if (_labelFont == value) return;
                _labelFont?.Dispose();
                _labelFont = value;
                EnsureMinimumControlSize();
                InvalidateGeometryCache();
                Invalidate();
            }
        }

        // Hover font wins while the mouse is over an enabled control, then LabelFont, then Font.
        private Font ActiveLabelFont =>
            (_isHovered && Enabled && _fontHovered is not null) ? _fontHovered
            : _labelFont ?? Font;

        private Font? _fontHovered;

        /// <summary>
        /// The font for the FACE - the selected item, or a split button's own Text. Hover was
        /// already honoured for the label and by the virtual renderer, but not here, so a menu
        /// title in a real (handle-backed) control ignored FontHovered while its virtualised twin
        /// obeyed it.
        /// </summary>
        private Font ActiveFaceFont =>
            (_isHovered && Enabled && _fontHovered is not null) ? _fontHovered : Font;

        [Category("Appearance")]
        [Description("Font used for the label while the mouse is over the control. Defaults to Font. Matches IconButton/ModernButton's FontHovered.")]
        public Font FontHovered
        {
            get => _fontHovered ?? ActiveLabelFont;
            set { _fontHovered = value; Invalidate(); }
        }
        private bool ShouldSerializeFontHovered() => _fontHovered is not null;
        private void ResetFontHovered() { _fontHovered = null; Invalidate(); }

        // Add these public properties (around line 180):
        [Category("Appearance - Disabled")]
        public Color DisabledInnerColor
        {
            get => _disabledInnerColor;
            set { if (value != _disabledInnerColor) { _disabledInnerColor = value; Invalidate(); } }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledBorderColor
        {
            get => _disabledBorderColor;
            set { if (value != _disabledBorderColor) { _disabledBorderColor = value; Invalidate(); } }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledTextColor
        {
            get => _disabledTextColor;
            set { if (value != _disabledTextColor) { _disabledTextColor = value; Invalidate(); } }
        }

        [Category("Appearance - Dropdown")]
        public Color ItemHoverColor
        {
            get => _itemHoverColor;
            set { if (value != _itemHoverColor) { _itemHoverColor = value; InvalidateDropDown(); } }
        }

        [Category("Appearance - Dropdown")]
        public int MaxVisibleRows
        {
            get => _maxVisibleRows;
            set { if (value > 0 && value != _maxVisibleRows) { _maxVisibleRows = value; InvalidateDropDown(); } }
        }

        [Category("Appearance")]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { if (value != _cornerRadius && value >= 0) { _cornerRadius = value; InvalidateGeometryCache(); Invalidate(); } }
        }

        [Category("Appearance")]
        public int BorderThickness
        {
            get => _borderThickness;
            set { if (value != _borderThickness && value >= 0) { _borderThickness = value; Invalidate(); } }
        }

        [Category("Appearance")]
        public Color InnerColor
        {
            get => _innerColor;
            set { if (value != _innerColor) { _innerColor = value; Invalidate(); } }
        }

        // ── Inner gradient ──────────────────────────────────────────────────────────────────
        // Opt-in, so every existing combo/split button keeps its flat fill until asked.
        // The gradient's START is deliberately whichever inner colour the current STATE picks
        // (Inner / Hover / Selected / Disabled) — only the END colour is authored here. That
        // way hover and open states still visibly change the control instead of being painted
        // over by a fixed gradient, and one extra colour covers every state.

        [DefaultValue(false)]
        [Category("Appearance")]
        [Description("Fill the body with a gradient running from the current state's inner colour to InnerGradientEndColor. Off = flat InnerColor.")]
        public bool UseInnerGradient
        {
            get => _useInnerGradient;
            set { if (value != _useInnerGradient) { _useInnerGradient = value; Invalidate(); } }
        }

        [Category("Appearance")]
        [Description("Far end of the inner gradient. The near end is the active state's inner colour (Inner/Hover/Selected/Disabled).")]
        public Color InnerGradientEndColor
        {
            get => _innerGradientEndColor;
            set { if (value != _innerGradientEndColor) { _innerGradientEndColor = value; Invalidate(); } }
        }

        [DefaultValue(RoundedGradientDirection.Down)]
        [Category("Appearance")]
        [Description("Direction the inner gradient runs.")]
        public RoundedGradientDirection InnerGradientDirection
        {
            get => _innerGradientDirection;
            set { if (value != _innerGradientDirection) { _innerGradientDirection = value; Invalidate(); } }
        }

        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set { if (value != _borderColor) { _borderColor = value; Invalidate(); } }
        }

        /// <summary>
        /// Body fill for the current state: a plain brush normally, a two-colour gradient when
        /// <see cref="UseInnerGradient"/> is on. Caller owns the returned brush.
        /// </summary>
        private Brush CreateInnerBrush(Color stateColor)
        {
            Rectangle bounds = ClientRectangle;
            if (!_useInnerGradient || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return new SolidBrush(stateColor);
            }

            // Inflated by one pixel: a LinearGradientBrush sized exactly to the fill area
            // renders its first row/column with the wrap-around colour, which shows up as a
            // hairline of the wrong shade along the top/left edge.
            Rectangle gradientBounds = Rectangle.Inflate(bounds, 1, 1);

            // GDI+ angles run clockwise from the positive X axis.
            float angle = _innerGradientDirection switch
            {
                RoundedGradientDirection.Left => 0f,     // left → right
                RoundedGradientDirection.Up => 270f,     // bottom → top
                RoundedGradientDirection.Right => 180f,  // right → left
                _ => 90f                                 // Down: top → bottom
            };

            return new LinearGradientBrush(gradientBounds, stateColor, _innerGradientEndColor, angle);
        }

        [Category("Appearance - Hover")]
        public Color HoverInnerColor
        {
            get => _hoverInnerColor;
            set { if (value != _hoverInnerColor) { _hoverInnerColor = value; Invalidate(); } }
        }

        [Category("Appearance - Hover")]
        public Color HoverBorderColor
        {
            get => _hoverBorderColor;
            set { if (value != _hoverBorderColor) { _hoverBorderColor = value; Invalidate(); } }
        }

        [Category("Appearance - Hover")]
        public int HoverBorderThickness
        {
            get => _hoverBorderThickness;
            set { if (value != _hoverBorderThickness && value >= 0) { _hoverBorderThickness = value; Invalidate(); } }
        }

        [Category("Appearance - Hover")]
        public bool HoverPropertiesEnabled
        {
            get => _hoverPropertiesEnabled;
            set { if (value != _hoverPropertiesEnabled) { _hoverPropertiesEnabled = value; Invalidate(); } }
        }

        


        [Category("Appearance - Selected")]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; Invalidate(); } }
        }

        [Category("Appearance - Selected")]
        public Color SelectedInnerColor
        {
            get => _selectedInnerColor;
            set { if (value != _selectedInnerColor) { _selectedInnerColor = value; Invalidate(); } }
        }

        [Category("Appearance - Selected")]
        public Color SelectedBorderColor
        {
            get => _selectedBorderColor;
            set { if (value != _selectedBorderColor) { _selectedBorderColor = value; Invalidate(); } }
        }

        [Category("Appearance - Selected")]
        public int SelectedBorderThickness
        {
            get => _selectedBorderThickness;
            set { if (value != _selectedBorderThickness && value >= 0) { _selectedBorderThickness = value; Invalidate(); } }
        }

        [Category("Appearance - Dropdown")]
        public Color SeparatorColor
        {
            get => _separatorColor;
            set { if (value != _separatorColor) { _separatorColor = value; InvalidateDropDown(); } }
        }

        [Category("Appearance - Dropdown")]
        public int ItemHeight
        {
            get => _itemHeight;
            set { if (value > 12 && value != _itemHeight) { _itemHeight = value; InvalidateDropDown(); } }
        }

        [DefaultValue(24)]
        [Category("Appearance")]
        [Description("Size an item's Image is drawn at, in the list and on the button face. Raise ItemHeight to match, or taller icons will be clipped by the row.")]
        public int ItemImageSize
        {
            get => _itemImageSize;
            set { if (value > 0 && value != _itemImageSize) { _itemImageSize = value; InvalidateDropDown(); Invalidate(); } }
        }

        [DefaultValue(0)]
        [Category("Appearance")]
        [Description("Pixels an item's image grows by while its row is hovered, from the centre. The row's layout keeps the ungrown size, so nothing beside it moves.")]
        public int ItemImageHoverGrow
        {
            get => _itemImageHoverGrow;
            set { if (value >= 0 && value != _itemImageHoverGrow) { _itemImageHoverGrow = value; InvalidateDropDown(); } }
        }

        [Category("Appearance - Dropdown")]
        [DefaultValue(0)]
        [Description("Horizontal nudge for the dropdown list, in pixels. Negative moves it left. The list is right-aligned with the control by default; use this to fine-tune against the button's visual edge.")]
        public int DropDownOffsetX
        {
            get => _dropDownOffsetX;
            set { if (value != _dropDownOffsetX) { _dropDownOffsetX = value; InvalidateDropDown(); } }
        }
        private int _dropDownOffsetX = 0;

        [Category("Appearance - Dropdown")]
        public int DropShadowSize
        {
            get => _dropShadowSize;
            set { if (value != _dropShadowSize && value >= 0) { _dropShadowSize = value; InvalidateDropDown(); } }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Category("Data")]
        public BindingList<RoundedComboBoxItem> Items => _items;

        public bool ShouldSerializeItems() => false;

        [Category("Behavior")]
        [Description("Item index selected automatically when the combo has items and no current selection.")]
        [DefaultValue(-1)]
        public int DefaultSelectedIndex
        {
            get => _defaultSelectedIndex;
            set
            {
                int newValue = Math.Max(-1, value);
                if (_defaultSelectedIndex == newValue)
                {
                    return;
                }

                _defaultSelectedIndex = newValue;
                ApplyDefaultSelectedIndex();
            }
        }

        [Browsable(false)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value != _selectedIndex)
                {
                    _selectedIndex = value;
                    // ⭐ REMOVED: Don't auto-set IsSelected when item is chosen
                    // _isSelected = _selectedIndex >= 0;
                    Invalidate();
                    OnSelectedIndexChanged(EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        public RoundedComboBoxItem? SelectedItem
        {
            get => (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;
            set
            {
                if (value == null) SelectedIndex = -1;
                else SelectedIndex = _items.IndexOf(value);
            }
        }

        public event EventHandler? SelectedIndexChanged;
        protected virtual void OnSelectedIndexChanged(EventArgs e) => SelectedIndexChanged?.Invoke(this, e);

        /// <summary>
        /// When false, the built-in item dropdown does NOT open on click — instead
        /// <see cref="DropDownButtonClicked"/> fires so the host can show its own popup.
        /// Lets the combo double as a "button with a dropdown arrow" (split-button style).
        /// </summary>
        [DefaultValue(true)]
        [Category("Behavior")]
        public bool DropDownEnabled { get; set; } = true;

        /// <summary>
        /// Raised when the RIGHT arrow zone is clicked while <see cref="DropDownEnabled"/> is
        /// false. The Rectangle is the control's bounds in SCREEN coordinates — anchor a custom
        /// popup to its right edge (rect.Right) and below it (rect.Bottom).
        /// </summary>
        public event EventHandler<Rectangle>? DropDownButtonClicked;

        /// <summary>
        /// Raised just before the list opens, so a menu that depends on the world - recent
        /// projects, autosaves on disk - can be rebuilt against what is true NOW rather than
        /// what was true when the control was created.
        /// </summary>
        public event EventHandler? DropDownOpening;

        private int _arrowZoneWidth = 24;

        /// <summary>
        /// Split-button mode: the control is divided into a LEFT button area (raises
        /// <see cref="ButtonClick"/>) and a RIGHT arrow zone, with a divider drawn between.
        /// The arrow zone opens the item list when <see cref="DropDownEnabled"/> is true
        /// (mode 1: combo + left button), or raises <see cref="DropDownButtonClicked"/> when
        /// it's false (mode 2: button + custom dropdown). Off = a plain combo (unchanged).
        /// </summary>
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool SplitButtonMode { get; set; } = false;

        /// <summary>
        /// Which end the dropdown arrow and its click zone live on. Left turns the control into
        /// a menu title - arrow first, then the word - which is what a menu bar wants; a combo
        /// keeps the arrow on the right where a combo's arrow belongs.
        ///
        /// The arrow, its hit zone, the split divider and the text inset all follow this, so the
        /// painted arrow and the thing you can click cannot end up on opposite ends.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(RoundedComboBoxSide.Right)]
        [Description("Which end the dropdown arrow and its click zone sit on.")]
        public RoundedComboBoxSide ArrowSide
        {
            get => _arrowSide;
            set
            {
                if (_arrowSide == value) return;
                _arrowSide = value;
                Invalidate();
            }
        }
        private RoundedComboBoxSide _arrowSide = RoundedComboBoxSide.Right;

        /// <summary>
        /// Which of the control's edges the open list lines up with. Separate from
        /// <see cref="ArrowSide"/> on purpose: a menu title wants its list flush under its LEFT
        /// edge growing rightwards, whichever end its arrow is on.
        ///
        /// Right is the default because <see cref="ComboBoxWidth"/> can be wider than a narrow
        /// split button, and a left-aligned list would then hang off to the right.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(RoundedComboBoxSide.Right)]
        [Description("Which edge of the control the open list lines up with.")]
        public RoundedComboBoxSide DropDownAlign
        {
            get => _dropDownAlign;
            set
            {
                if (_dropDownAlign == value) return;
                _dropDownAlign = value;
                InvalidateDropDown();
            }
        }
        private RoundedComboBoxSide _dropDownAlign = RoundedComboBoxSide.Right;

        /// <summary>
        /// Whether choosing an item rewrites the button face. True is combo behaviour - the face
        /// reports the current selection. False is MENU behaviour: "File" stays "File" after you
        /// pick Open, because a menu title names a menu, it does not report a state.
        ///
        /// The selection is still recorded either way; only what is painted changes.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("False keeps the button's own Text on the face after an item is chosen.")]
        public bool TextFollowsSelection
        {
            get => _textFollowsSelection;
            set
            {
                if (_textFollowsSelection == value) return;
                _textFollowsSelection = value;
                Invalidate();
            }
        }
        private bool _textFollowsSelection = true;

        /// <summary>Raised when the LEFT (button) area is clicked in <see cref="SplitButtonMode"/>.</summary>
        public event EventHandler? ButtonClick;

        private Image? _buttonImage = null;
        private Image? _buttonImageHover = null;
        private Image? _buttonImagePressed = null;
        private Image? _buttonImageB = null;
        private Image? _buttonImageBHover = null;
        private Image? _buttonImageBPressed = null;
        private IconButtonImageBUse _imageBUse = IconButtonImageBUse.None;
        private bool _toggled = false;
        private bool _isPressed = false;
        private int _buttonImageHoverGrow = 0;
        private Size _buttonImageSize = Size.Empty;
        private const int ButtonImageTextGap = 6;

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Image drawn on the button face, on the same side as the text and outboard of it (between the text and the control edge). The text is pushed in to clear it.")]
        public Image? ButtonImage
        {
            get => _buttonImage;
            set { _buttonImage = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Size ButtonImage is drawn at. Leave empty to use the image's own size. Setting it smaller than the button's inner height is what leaves room for HoverGrowsImage.")]
        public Size ButtonImageSize
        {
            get => _buttonImageSize;
            set { _buttonImageSize = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Pixels ButtonImage grows by while the mouse is over the control (0 = no grow). Steps down until it fits, and is paint-time only — the text never moves.")]
        public int ButtonImageHoverGrow
        {
            get => _buttonImageHoverGrow;
            set { _buttonImageHoverGrow = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Shown while hovered (untoggled). Falls back to ButtonImage when unset. Matches IconButton's image-family model.")]
        public Image? ButtonImageHover
        {
            get => _buttonImageHover;
            set { _buttonImageHover = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Shown while pressed (untoggled). Falls back to ButtonImageHover, then ButtonImage.")]
        public Image? ButtonImagePressed
        {
            get => _buttonImagePressed;
            set { _buttonImagePressed = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("The toggled-on image (ImageBUse = Toggle). Falls back to ButtonImage when unset. Matches IconButton's image-family model.")]
        public Image? ButtonImageB
        {
            get => _buttonImageB;
            set { _buttonImageB = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Shown while hovered in the toggled-on state. Falls back to ButtonImageB.")]
        public Image? ButtonImageBHover
        {
            get => _buttonImageBHover;
            set { _buttonImageBHover = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Shown while pressed in the toggled-on state. Falls back to ButtonImageBHover, then ButtonImageB.")]
        public Image? ButtonImageBPressed
        {
            get => _buttonImageBPressed;
            set { _buttonImageBPressed = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(IconButtonImageBUse.None)]
        [Description("None: plain button on the ButtonImage family. Toggle: a click flips Toggled and the B family becomes the on-state.")]
        public IconButtonImageBUse ImageBUse
        {
            get => _imageBUse;
            set { _imageBUse = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("On-state of a toggle button (ImageBUse = Toggle): false shows ButtonImage, true shows ButtonImageB.")]
        public bool Toggled
        {
            get => _toggled;
            set { if (_toggled == value) return; _toggled = value; Invalidate(); }
        }

        // The image for the current family and state — mirrors IconButton.CurrentImage:
        // family by toggle, per-state fallback pressed → hover → normal within the family.
        private Image? CurrentButtonImage
        {
            get
            {
                if (_imageBUse == IconButtonImageBUse.Toggle && _toggled)
                {
                    Image? normal = _buttonImageB ?? _buttonImage;
                    if (_isPressed) return _buttonImageBPressed ?? _buttonImageBHover ?? normal;
                    if (_isHovered) return _buttonImageBHover ?? normal;
                    return normal;
                }

                if (_isPressed) return _buttonImagePressed ?? _buttonImageHover ?? _buttonImage;
                if (_isHovered) return _buttonImageHover ?? _buttonImage;
                return _buttonImage;
            }
        }

        // =============================================================================================
        // CONSTRUCTOR
        // =============================================================================================
        public RoundedComboBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                BackColor = SystemColors.Control;
            else
            {
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            Height = 36;
            Font = DefaultComboFont;
            Text = string.Empty;
            _itemFont = this.Font;
            _items.ListChanged += Items_ListChanged;
        }

        private void Items_ListChanged(object? sender, ListChangedEventArgs e)
        {
            if (_selectedIndex >= _items.Count)
            {
                SelectedIndex = -1;
            }

            ApplyDefaultSelectedIndex();
            InvalidateDropDown();
            Invalidate();
        }

        private void ApplyDefaultSelectedIndex()
        {
            if (_selectedIndex >= 0
                || _defaultSelectedIndex < 0
                || _defaultSelectedIndex >= _items.Count)
            {
                return;
            }

            SelectedIndex = _defaultSelectedIndex;
        }

        // =============================================================================================
        // OVERRIDES (Main Control)
        // =============================================================================================
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _itemFont = this.Font;
            EnsureMinimumControlSize();
            Invalidate();
            InvalidateDropDown();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            EnsureMinimumControlSize();
            InvalidateGeometryCache();
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
            {
                CloseDropDown();
            }

            InvalidateDropDown();
            Invalidate();
        }

        private void EnsureMinimumControlSize()
        {
            Size preferred = GetPreferredSize(Size.Empty);
            int minimumWidth = string.IsNullOrWhiteSpace(Text)
                ? preferred.Width
                : Math.Min(Width, preferred.Width);
            int newWidth = Math.Max(Width, minimumWidth);
            int newHeight = Math.Max(Height, preferred.Height);
            if (newWidth != Width || newHeight != Height)
                Size = new Size(newWidth, newHeight);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = _comboBoxWidth;
            int height = Height;

            if (!string.IsNullOrWhiteSpace(Text))
            {
                Size textSize = TextRenderer.MeasureText(
                    Text,
                    ActiveLabelFont,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                width += _textGap + textSize.Width;
                height = Math.Max(height, textSize.Height);
            }

            return new Size(width, height);
        }

        public int GetWidestItemComboBoxWidth()
        {
            int maxTextWidth = 0;
            foreach (RoundedComboBoxItem item in _items)
            {
                if (string.IsNullOrEmpty(item.Text))
                {
                    continue;
                }

                Size itemSize = TextRenderer.MeasureText(
                    item.Text,
                    Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                maxTextWidth = Math.Max(maxTextWidth, itemSize.Width);
            }

            int arrowWidth = 18;
            int imageWidth = _buttonImage == null
                ? 0
                : (_buttonImageSize.IsEmpty ? _buttonImage.Width : _buttonImageSize.Width) + ButtonImageTextGap;
            int width = maxTextWidth + imageWidth + (_textPaddingHorizontal * 2) + arrowWidth + (_borderThickness * 2) + 4;
            return Math.Max(40, width);
        }

        public void ExpandLabelToFitText()
        {
            if (string.IsNullOrWhiteSpace(Text))
                return;

            Size textSize = TextRenderer.MeasureText(
                Text, Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            int newWidth = _comboBoxWidth + _textGap + textSize.Width;
            if (newWidth == Width)
                return;

            _isAdjustingDesignerComboWidth = true;
            try { Width = newWidth; }
            finally { _isAdjustingDesignerComboWidth = false; }
        }

        public void ExpandComboBoxToWidestItem()
        {
            int newComboWidth = GetWidestItemComboBoxWidth();
            int labelSpace = string.IsNullOrWhiteSpace(Text)
                ? 0
                : Math.Max(0, Width - _comboBoxWidth - _textGap);
            int newWidth = string.IsNullOrWhiteSpace(Text)
                ? newComboWidth
                : newComboWidth + _textGap + labelSpace;

            _isAdjustingDesignerComboWidth = true;
            try
            {
                ComboBoxWidth = newComboWidth;
                Width = Math.Max(40, newWidth);
            }
            finally
            {
                _isAdjustingDesignerComboWidth = false;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Size != _lastSize)
            {
                InvalidateGeometryCache();
                _lastSize = Size;
                Invalidate();
            }
        }

        protected override void SetBoundsCore(
            int x,
            int y,
            int width,
            int height,
            BoundsSpecified specified)
        {
            Rectangle oldBounds = Bounds;
            bool widthChanged = width != oldBounds.Width;

            if (!_isAdjustingDesignerComboWidth &&
                widthChanged &&
                IsInDesignMode() &&
                IsHandleCreated &&
                !IsDesignerLoading() &&
                oldBounds.Width > 0 &&
                oldBounds.Height > 0 &&
                specified.HasFlag(BoundsSpecified.Width))
            {
                AdjustComboWidthForDesignerResize(
                    oldBounds,
                    new Rectangle(x, y, width, height),
                    specified);
            }

            base.SetBoundsCore(x, y, width, height, specified);
        }

        private bool IsDesignerLoading()
        {
            var host = Site?.GetService(typeof(IDesignerHost)) as IDesignerHost;
            return host?.Loading == true;
        }

        private void AdjustComboWidthForDesignerResize(
            Rectangle oldBounds,
            Rectangle newBounds,
            BoundsSpecified specified)
        {
            int widthDelta = newBounds.Width - oldBounds.Width;
            if (widthDelta == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Text))
            {
                SetComboBoxWidthFromDesignerResize(newBounds.Width);
                return;
            }

            bool draggedLeftEdge = newBounds.Left != oldBounds.Left;
            bool draggedRightEdge = !draggedLeftEdge && newBounds.Width != oldBounds.Width;

            bool comboIsOnLeft = _labelSide == HorizontalAlignment.Right;
            bool comboEdgeDragged = comboIsOnLeft ? draggedLeftEdge : draggedRightEdge;
            if (!comboEdgeDragged)
            {
                return;
            }

            SetComboBoxWidthFromDesignerResize(_comboBoxWidth + widthDelta);
        }

        private void SetComboBoxWidthFromDesignerResize(int width)
        {
            int newValue = Math.Max(40, width);
            if (_comboBoxWidth == newValue)
            {
                return;
            }

            int oldValue = _comboBoxWidth;
            var cs = Site?.GetService(typeof(IComponentChangeService)) as IComponentChangeService;
            PropertyDescriptor? prop = TypeDescriptor.GetProperties(this)["ComboBoxWidth"];

            cs?.OnComponentChanging(this, prop);

            _isAdjustingDesignerComboWidth = true;
            try
            {
                _comboBoxWidth = newValue;
                InvalidateGeometryCache();
                Invalidate();
            }
            finally
            {
                _isAdjustingDesignerComboWidth = false;
            }

            cs?.OnComponentChanged(this, prop, oldValue, newValue);
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
            _isPressed = false;      // never leave the pressed image stuck on
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            _isPressed = true;
            if (_imageBUse == IconButtonImageBUse.Toggle)
            {
                _toggled = !_toggled;
            }
            Invalidate();

            if (SplitButtonMode)
            {
                bool onArrow = _arrowSide == RoundedComboBoxSide.Left
                    ? e.X <= ArrowZoneWidth
                    : e.X >= ClientSize.Width - ArrowZoneWidth;
                if (onArrow)
                {
                    if (DropDownEnabled) ToggleDropDown();
                    else DropDownButtonClicked?.Invoke(this, RectangleToScreen(ClientRectangle));
                }
                else
                {
                    ButtonClick?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            if (!DropDownEnabled)
            {
                // Whole-control custom-popup mode (no split): let the host handle it.
                DropDownButtonClicked?.Invoke(this, RectangleToScreen(ClientRectangle));
                return;
            }

            ToggleDropDown();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            // Only close if the focus actually left the control AND the dropdown
            if (_dropDown != null && _dropDown.Focused)
            {
                return; // The dropdown took focus, so don't close it!
            }

            base.OnLostFocus(e);
            CloseDropDown();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Without this the arcs rasterise half a pixel from where the geometry puts them.
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (_cachedPath == null || CornerRadius != _lastRadius)
            {
                BuildCachedGeometry();
                _lastRadius = CornerRadius;
            }

            if (_cachedPath == null) return;

            Color inner;
            if (!Enabled)  // ⭐ Check disabled first
                inner = DisabledInnerColor;
            else if (_isDropDownOpen)
                inner = SelectedInnerColor;
            else if (_isHovered && HoverPropertiesEnabled)
                inner = HoverInnerColor;
            else
                inner = InnerColor;

            // FillPath already confines the brush to the path, so the SetClip that used to wrap
            // this was redundant - and harmful: a GDI+ clip region is a 1-bit mask, so clipping
            // an anti-aliased fill re-cuts its own smooth edge with a hard one. That was the
            // stair-stepping on the corners.
            using (Brush brush = CreateInnerBrush(inner))
                g.FillPath(brush, _cachedPath);

            Color border;
            int thickness;
            if (!Enabled)  // ⭐ Check disabled first
            {
                border = DisabledBorderColor;
                thickness = BorderThickness;
            }
            else if (_isDropDownOpen)
            {
                border = SelectedBorderColor;
                thickness = SelectedBorderThickness;
            }
            else if (_isHovered && HoverPropertiesEnabled)
            {
                border = HoverBorderColor;
                thickness = HoverBorderThickness;
            }
            else
            {
                border = BorderColor;
                thickness = BorderThickness;
            }

            // GDI+ treats a width-0 pen as a HAIRLINE (1 device pixel), not "no line" —
            // BorderThickness = 0 must genuinely mean borderless.
            if (thickness > 0)
            {
                // Stroke a path inset by HALF the pen width rather than the fill path itself.
                // A pen straddles its path: stroking the fill's own edge leaves the fill's AA
                // fringe and the stroke's AA fringe each blending to whatever is behind, and the
                // backdrop survives between them as a pale ring just inside the border. Inset by
                // half, the pen covers that fringe and sits on solid fill.
                float inset = thickness / 2f;
                RectangleF strokeBounds = RectangleF.Inflate(_cachedRect, -inset, -inset);
                float strokeRadius = Math.Max(0f, Math.Max(1, CornerRadius) - inset);

                using (GraphicsPath borderPath = RoundedPathF(strokeBounds, strokeRadius))
                using (Pen pen = new Pen(border, thickness))
                    g.DrawPath(pen, borderPath);
            }

            DrawSelectedText(g);
            DrawDropArrow(g);
            if (SplitButtonMode) DrawSplitDivider(g);
            DrawLabel(g);
        }

        private void DrawSplitDivider(Graphics g)
        {
            int x = _arrowSide == RoundedComboBoxSide.Left
                ? _cachedRect.Left + ArrowZoneWidth
                : _cachedRect.Right - ArrowZoneWidth;
            int top = _cachedRect.Top + 5;
            int bottom = _cachedRect.Bottom - 5;
            using var pen = new Pen(Color.FromArgb(195, 195, 195));
            g.DrawLine(pen, x, top, x, bottom);
        }

        private void DrawSelectedText(Graphics g)
        {
            Rectangle rect = GetTextRectangle();
            Color textColor = Enabled ? ForeColor : DisabledTextColor;

            if (_buttonImage != null)
                rect = DrawButtonImage(g, rect);

            if (SelectedItem != null && _textFollowsSelection)
            {
                DrawItemContent(g, SelectedItem, ActiveFaceFont, rect, textColor, _itemImageSize);
                return;
            }

            // Nothing selected, or a menu title that ignores the selection: the control's own
            // Text is the face. Split-button mode has always shown Text here; a menu title shows
            // it whether or not it is split, which is what makes "File" stay "File".
            string text = SplitButtonMode || !_textFollowsSelection
                ? (Text ?? string.Empty)
                : string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                TextRenderer.DrawText(g, text, ActiveFaceFont, rect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }

        // Draws ButtonImage at the leading edge of the text area — i.e. on the text's side, between
        // the text and the control edge — and returns what's left for the text.
        //
        // The text always clears the image's BASE slot, never its grown size, so hovering can't
        // shuffle the text sideways. The grown image is just drawn centred on that fixed slot.
        private Rectangle DrawButtonImage(Graphics g, Rectangle bounds)
        {
            Image image = CurrentButtonImage ?? _buttonImage!;
            Size baseSize = _buttonImageSize.IsEmpty ? image.Size : _buttonImageSize;
            Size drawSize = ResolveButtonImageDrawSize(bounds, baseSize);

            Rectangle slot = new Rectangle(
                bounds.Left,
                bounds.Top + (bounds.Height - baseSize.Height) / 2,
                baseSize.Width,
                baseSize.Height);

            System.Drawing.Drawing2D.InterpolationMode prev = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(
                image,
                slot.Left + (slot.Width - drawSize.Width) / 2,
                slot.Top + (slot.Height - drawSize.Height) / 2,
                drawSize.Width,
                drawSize.Height);
            g.InterpolationMode = prev;

            int textLeft = Math.Min(slot.Right + ButtonImageTextGap, bounds.Right);
            return Rectangle.FromLTRB(textLeft, bounds.Top, bounds.Right, bounds.Bottom);
        }

        // Hover adds ButtonImageHoverGrow px, stepping down until it fits the button's inner area,
        // so a value that is slightly too big still gives some feedback instead of none.
        private Size ResolveButtonImageDrawSize(Rectangle bounds, Size baseSize)
        {
            if (_buttonImageHoverGrow <= 0 || !_isHovered || !Enabled)
                return baseSize;

            for (int grow = _buttonImageHoverGrow; grow >= 1; grow--)
            {
                Size candidate = new Size(baseSize.Width + grow, baseSize.Height + grow);
                if (candidate.Width <= bounds.Width && candidate.Height <= bounds.Height)
                    return candidate;
            }

            return baseSize;
        }

        // Draws an item's icon (ItemImageSize square, if any) then its text, left to right,
        // vertically centred. Either or both may be present: media items are icon-only,
        // "All" is icon + "All".
        internal static void DrawItemContent(Graphics g, RoundedComboBoxItem item, Font font, Rectangle bounds, Color color, int imageSize = 24, bool hovered = false, int hoverGrow = 0)
        {
            int x = bounds.Left;

            Image? image = hovered ? (item.ImageHover ?? item.Image) : item.Image;
            if (image != null)
            {
                int size = Math.Max(1, imageSize);

                // Grown from the CENTRE, and the layout slot keeps the ungrown width, so the
                // text beside it never shifts when the row is hovered.
                int drawSize = hovered ? size + Math.Max(0, hoverGrow) : size;
                int inset = (drawSize - size) / 2;
                int y = bounds.Top + (bounds.Height - drawSize) / 2;

                System.Drawing.Drawing2D.InterpolationMode prev = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, x - inset, y, drawSize, drawSize);
                g.InterpolationMode = prev;
                x += size + 6;
            }

            if (!string.IsNullOrEmpty(item.Text))
            {
                RectangleF textRect = new RectangleF(x, bounds.Top, Math.Max(0, bounds.Right - x), bounds.Height);

                // GDI+ (DrawString), NOT GDI (TextRenderer): the dropdown is a layered/transparent
                // popup, and GDI text writes zero-alpha pixels — it would punch a see-through hole.
                using (SolidBrush textBrush = new SolidBrush(color))
                using (StringFormat sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    g.DrawString(item.Text, font, textBrush, textRect, sf);
                }
            }
        }

        private Rectangle GetTextRectangle()
        {
            int arrowWidth = 18;

            // The arrow's width comes off whichever end it is on, so the text never runs under it.
            int leftReserve = _arrowSide == RoundedComboBoxSide.Left ? arrowWidth : 0;
            int rightReserve = _arrowSide == RoundedComboBoxSide.Left ? 0 : arrowWidth;

            int innerLeft = _cachedRect.Left + _borderThickness + leftReserve + _textPaddingHorizontal;
            int innerTop = _cachedRect.Top + _borderThickness;
            int innerRight = _cachedRect.Right - _borderThickness - rightReserve - _textPaddingHorizontal;
            int innerBottom = _cachedRect.Bottom - _borderThickness;
            return Rectangle.FromLTRB(innerLeft, innerTop, innerRight, innerBottom);
        }

        private void DrawDropArrow(Graphics g)
        {
            int arrowWidth = 10;
            int arrowHeight = 6;
            int arrowEdgeMargin = 12;

            int cx = _arrowSide == RoundedComboBoxSide.Left
                ? _cachedRect.Left + arrowEdgeMargin
                : _cachedRect.Right - arrowEdgeMargin;
            int cy = _cachedRect.Top + _cachedRect.Height / 2;

            Point p1 = new Point(cx - arrowWidth / 2, cy - arrowHeight / 2);
            Point p2 = new Point(cx + arrowWidth / 2, cy - arrowHeight / 2);
            Point p3 = new Point(cx, cy + arrowHeight / 2);

            Color arrowColor = Enabled ? ForeColor : DisabledTextColor;  // ⭐ Use DisabledTextColor

            using (SolidBrush b = new SolidBrush(arrowColor))
            {
                g.FillPolygon(b, new[] { p1, p2, p3 });
            }
        }

        private void DrawLabel(Graphics g)
        {
            // In split-button mode the Text is the body face (see DrawSelectedText), not a
            // side label — so don't also draw it beside the control.
            if (SplitButtonMode || string.IsNullOrWhiteSpace(Text))
                return;

            Rectangle textRect = GetLabelRectangle();
            TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPadding;

            if (_labelSide == HorizontalAlignment.Left)
                flags |= TextFormatFlags.Right;

            Color textColor = Enabled ? ForeColor : DisabledTextColor;
            TextRenderer.DrawText(g, Text, ActiveLabelFont, textRect, textColor, flags);
        }

        private Rectangle GetLabelRectangle()
        {
            if (string.IsNullOrWhiteSpace(Text))
                return Rectangle.Empty;

            if (_labelSide == HorizontalAlignment.Right)
            {
                int textLeft = _cachedRect.Right + _textGap;
                return new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height);
            }

            int textWidth = Math.Max(0, _cachedRect.Left - _textGap);
            return new Rectangle(0, 0, textWidth, Height);
        }

        /// <summary>Float-precision rounded rect, for strokes that sit between whole pixels.</summary>
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

        private void BuildCachedGeometry()
        {
            InvalidateGeometryCache();
            _cachedRect = GetComboBoxRectangle();
            int radius = CornerRadius < 1 ? 1 : CornerRadius;
            _cachedPath = new GraphicsPath();
            int d = radius * 2;

            _cachedPath.AddArc(_cachedRect.X, _cachedRect.Y, d, d, 180, 90);
            _cachedPath.AddArc(_cachedRect.Right - d, _cachedRect.Y, d, d, 270, 90);
            _cachedPath.AddArc(_cachedRect.Right - d, _cachedRect.Bottom - d, d, d, 0, 90);
            _cachedPath.AddArc(_cachedRect.X, _cachedRect.Bottom - d, d, d, 90, 90);
            _cachedPath.CloseFigure();
        }

        private Rectangle GetComboBoxRectangle()
        {
            if (SplitButtonMode || string.IsNullOrWhiteSpace(Text))
                return new Rectangle(0, 0, Width - 1, Height - 1);

            int comboWidth = Math.Min(Math.Max(1, Width), Math.Max(40, _comboBoxWidth));
            if (_labelSide == HorizontalAlignment.Left)
                return new Rectangle(Math.Max(0, Width - comboWidth), 0, comboWidth - 1, Height - 1);

            return new Rectangle(0, 0, comboWidth - 1, Height - 1);
        }

        private void InvalidateGeometryCache()
        {
            if (_cachedPath != null)
            {
                _cachedPath.Dispose();
                _cachedPath = null;
            }
        }

        private bool IsInDesignMode()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return true;
            }

            return Site?.DesignMode == true || Parent?.Site?.DesignMode == true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                InvalidateGeometryCache();
                CloseDropDown();
            }
            base.Dispose(disposing);
        }

        // =============================================================================================
        // DROPDOWN LOGIC
        // =============================================================================================
        internal void ToggleDropDown()
        {
            if (_isDropDownOpen) CloseDropDown();
            else OpenDropDown();
        }

        // A virtualized child (ARP v2) has no handle — PointToScreen on it would mint one and
        // un-virtualize the control. Bounds are parent-client coordinates either way, so map
        // through the parent when handle-less.
        internal Point ClientPointToScreenSafe(Point clientPoint)
            => IsHandleCreated || Parent == null
                ? PointToScreen(clientPoint)
                : Parent.PointToScreen(new Point(Left + clientPoint.X, Top + clientPoint.Y));

        private void OpenDropDown()
        {
            if (_isDropDownOpen) return;

            // Raised BEFORE the empty check on purpose: a menu whose contents depend on the
            // world (recent files, autosaves) is built here, and starts empty. Checking first
            // would mean such a menu could never open the first time.
            DropDownOpening?.Invoke(this, EventArgs.Empty);

            if (_items.Count == 0) return;
            if (Parent == null) return;

            // ⭐ NEW - Dispose old dropdown if it exists
            if (_dropDown != null)
            {
                try
                {
                    if (!_dropDown.IsDisposed)
                    {
                        _dropDown.Close();
                        _dropDown.Dispose();
                    }
                }
                catch { }
                _dropDown = null;
            }

            int visibleRows = ShowAllDropDownRows ? _items.Count : Math.Min(_items.Count, _maxVisibleRows);
            int dropHeight = visibleRows * _itemHeight + 4;   // small bottom padding
            Rectangle comboRect = GetComboBoxRectangle();
            int dropWidth = comboRect.Width;

            _dropDown = new DropDownForm(this);
            _dropDown.Size = new Size(dropWidth, dropHeight);

            Point screenLocation = ClientPointToScreenSafe(comboRect.Location);

            // Right-aligned by default: ComboBoxWidth can be wider than the button itself (a
            // narrow split button with a wide menu), and left-aligning then leaves the list
            // hanging off to the right. A menu title wants the opposite - see DropDownAlign.
            // Clamped either way so it can't open off-screen.
            int dropLeft = _dropDownAlign == RoundedComboBoxSide.Left
                ? screenLocation.X + _dropDownOffsetX
                : ClientPointToScreenSafe(new Point(ClientSize.Width, 0)).X - dropWidth + _dropDownOffsetX;

            Rectangle working = Screen.FromPoint(screenLocation).WorkingArea;
            dropLeft = Math.Max(working.Left, Math.Min(dropLeft, working.Right - dropWidth));

            Point dropPos = new Point(dropLeft, screenLocation.Y + comboRect.Height);
            _dropDown.Location = dropPos;

            _isDropDownOpen = true;

            // Close dropdown when form loses focus
            _dropDown.Deactivate += (_, __) => CloseDropDown();
            _dropDown.FormClosed += (_, __) =>
            {
                _isDropDownOpen = false;
                _dropDown = null;
                Invalidate();
            };

            _dropDown.Show();
            _messageFilter = new DropDownMessageFilter(this, _dropDown);
            Application.AddMessageFilter(_messageFilter);
            _dropDown.ListControl.EnsureSelectionVisible();

            Form? parentForm = this.FindForm();
            if (parentForm != null)
            {
                EventHandler locationHandler = null!;
                locationHandler = (s, e) =>
                {
                    CloseDropDown();
                    parentForm.LocationChanged -= locationHandler;
                };
                parentForm.LocationChanged += locationHandler;
            }
        }

        public void CloseDropDown()
        {
            // 1. Check if it's already closed or null to prevent double-entry
            if (_dropDown == null)
            {
                _isDropDownOpen = false;
                return;
            }

            // 2. Remove the global click filter before closing
            if (_messageFilter != null)
            {
                Application.RemoveMessageFilter(_messageFilter);
                _messageFilter = null;
            }

            var windowToClose = _dropDown;
            _dropDown = null;

            if (!windowToClose.IsDisposed)
            {
                try
                {
                    windowToClose.Close();

                    // 3. Schedule disposal for the next UI tick.
                    // This ensures the MouseDown event that triggered this is fully finished.
                    // BeginInvoke needs a handle; a virtualized owner has none, so borrow the
                    // form's. No handle anywhere -> dispose inline (nothing is pumping anyway).
                    Control invoker = IsHandleCreated ? this : FindForm() as Control ?? this;
                    if (invoker.IsHandleCreated)
                    {
                        invoker.BeginInvoke(new Action(() => {
                            if (!windowToClose.IsDisposed)
                                windowToClose.Dispose();
                        }));
                    }
                    else
                    {
                        windowToClose.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    // Log or debug if something weird happens during the close
                    System.Diagnostics.Debug.WriteLine("Dropdown close error: " + ex.Message);
                }
            }

            _isDropDownOpen = false;
            Invalidate();
        }

        private void InvalidateDropDown()
        {
            if (_isDropDownOpen)
            {
                CloseDropDown();
                OpenDropDown();
            }
        }

        // =============================================================================================
        // INNER CLASS: MESSAGE FILTER — closes dropdown on outside click
        // =============================================================================================
        private sealed class DropDownMessageFilter : IMessageFilter
        {
            private const int WM_LBUTTONDOWN = 0x0201;
            private const int WM_NCLBUTTONDOWN = 0x00A1;

            private readonly RoundedComboBox _owner;
            private readonly DropDownForm _dropDown;

            public DropDownMessageFilter(RoundedComboBox owner, DropDownForm dropDown)
            {
                _owner = owner;
                _dropDown = dropDown;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_NCLBUTTONDOWN)
                {
                    Point cursor = Cursor.Position;

                    if (!_dropDown.IsDisposed && _dropDown.Bounds.Contains(cursor))
                        return false;

                    Rectangle ownerScreen = new Rectangle(_owner.ClientPointToScreenSafe(Point.Empty), _owner.Size);
                    if (ownerScreen.Contains(cursor))
                        return false;

                    _owner.CloseDropDown();
                }
                return false;
            }
        }

        // =============================================================================================
        // INNER CLASS: DROPDOWN FORM (DWM GLASS CONTAINER)
        // =============================================================================================
        private class DropDownForm : Form
        {
            private readonly ItemListControl _listControl;

            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int leftWidth, rightWidth, topHeight, bottomHeight;
            }

            [DllImport("dwmapi.dll")]
            private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

            public DropDownForm(RoundedComboBox owner)
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.Manual;
                this.BackColor = Color.Black;
                this.TopMost = true;

                _listControl = new ItemListControl(owner);
                _listControl.Dock = DockStyle.Fill;
                _listControl.BackColor = Color.Black;
                this.Controls.Add(_listControl);
            }

            public ItemListControl ListControl => _listControl;

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                MARGINS margins = new MARGINS { leftWidth = -1 };
                DwmExtendFrameIntoClientArea(this.Handle, ref margins);

                ListControl.UpdateScrollbar();
                ListControl.EnsureSelectionVisible();
            }
        }

        // =============================================================================================
        // INNER CLASS: ITEM LIST CONTROL (The content drawn on the glass)
        // =============================================================================================
        private class ItemListControl : Control
        {
            private readonly RoundedComboBox _owner;
            private Rectangle _itemsRect;
            private VScrollBar _scrollBar;
            private int _scrollOffset = 0;
            private int _hoveredIndex = -1;  // ⭐ NEW: Track hovered item

            public ItemListControl(RoundedComboBox owner)
            {
                _owner = owner;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.UserPaint |
                         ControlStyles.Opaque, true);

                this.BackColor = Color.Black;
                Cursor = Cursors.Hand;

                _scrollBar = new VScrollBar();
                _scrollBar.Dock = DockStyle.Right;
                _scrollBar.Width = 17;
                _scrollBar.Visible = false;
                _scrollBar.ValueChanged += ScrollBar_ValueChanged!;
                this.Controls.Add(_scrollBar);

                this.MouseWheel += ItemListControl_MouseWheel!;
            }

            // ⭐ NEW: Track mouse movement for hover effects
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int newHoveredIndex = HitTestItemIndex(e.Location);
                if (newHoveredIndex >= 0 && newHoveredIndex < _owner._items.Count
                    && _owner._items[newHoveredIndex].IsHeader)
                {
                    newHoveredIndex = -1;                      // no hover highlight on section titles
                }

                if (newHoveredIndex != _hoveredIndex)
                {
                    _hoveredIndex = newHoveredIndex;
                    Invalidate();
                }
            }

            // ⭐ NEW: Clear hover when mouse leaves
            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_hoveredIndex != -1)
                {
                    _hoveredIndex = -1;
                    Invalidate();
                }
            }

            public void UpdateScrollbar()
            {
                int visibleRows = this.Height / _owner._itemHeight;
                int totalRows = _owner._items.Count;

                if (totalRows > visibleRows)
                {
                    _scrollBar.Visible = true;
                    _scrollBar.Minimum = 0;
                    _scrollBar.Maximum = totalRows - 1;
                    _scrollBar.LargeChange = visibleRows;
                    _scrollBar.SmallChange = 1;
                }
                else
                {
                    _scrollBar.Visible = false;
                    _scrollOffset = 0;
                }
            }

            public void EnsureSelectionVisible()
            {
                if (_owner.SelectedIndex < 0) return;
                if (!_scrollBar.Visible) return;

                int visibleRows = this.Height / _owner._itemHeight;

                if (_owner.SelectedIndex < _scrollOffset)
                {
                    _scrollBar.Value = _owner.SelectedIndex;
                }
                else if (_owner.SelectedIndex >= _scrollOffset + visibleRows)
                {
                    _scrollBar.Value = _owner.SelectedIndex - visibleRows + 1;
                }
            }

            private void ScrollBar_ValueChanged(object sender, EventArgs e)
            {
                _scrollOffset = _scrollBar.Value;
                Invalidate();
            }

            private void ItemListControl_MouseWheel(object sender, MouseEventArgs e)
            {
                if (!_scrollBar.Visible) return;

                int delta = e.Delta / 120;
                int newValue = _scrollBar.Value - delta;
                newValue = Math.Max(_scrollBar.Minimum, Math.Min(_scrollBar.Maximum - _scrollBar.LargeChange + 1, newValue));
                _scrollBar.Value = newValue;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    int index = HitTestItemIndex(e.Location);
                    if (index >= 0 && index < _owner._items.Count
                        && !_owner._items[index].IsHeader)     // section titles aren't selectable
                    {
                        _owner.SelectedIndex = index;
                        _owner.CloseDropDown();
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int scrollBarWidth = _scrollBar.Visible ? _scrollBar.Width : 0;
                _itemsRect = new Rectangle(0, 0, this.Width - scrollBarWidth, this.Height);
                _itemsRect.Inflate(-1, -1);

                using (GraphicsPath path = CreateRoundedRectPath(_itemsRect, _owner.CornerRadius))
                {
                    using (SolidBrush brush = new SolidBrush(Color.White))
                        g.FillPath(brush, path);

                    // Match the control's own border so the list looks like part of the button
                    // rather than a separate popup with its own styling.
                    using (Pen pen = new Pen(_owner._borderColor, Math.Max(1, _owner._borderThickness))
                    {
                        Alignment = PenAlignment.Inset
                    })
                        g.DrawPath(pen, path);
                }

                using (GraphicsPath path = CreateRoundedRectPath(_itemsRect, _owner.CornerRadius))
                {
                    Region oldClip = g.Clip;
                    g.SetClip(path);

                    int visibleRows = this.Height / _owner._itemHeight;
                    int startIndex = _scrollOffset;
                    int endIndex = Math.Min(_scrollOffset + visibleRows + 1, _owner._items.Count);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        Rectangle itemBounds = GetItemBounds(i);

                        if (itemBounds.Bottom < _itemsRect.Top || itemBounds.Top > _itemsRect.Bottom)
                            continue;

                        // Section titles get no hover or selection chrome — just a muted bold label.
                        if (_owner._items[i].IsHeader)
                        {
                            DrawHeaderText(g, i, itemBounds);
                            DrawItemSeparators(g, i, itemBounds);
                            continue;
                        }

                        // Hover/selection fills are inset by the border thickness so they can't
                        // paint over the list's own border at the left and right edges.
                        int fillInset = Math.Max(1, _owner._borderThickness);
                        Rectangle fillBounds = new Rectangle(
                            itemBounds.Left + fillInset,
                            itemBounds.Top,
                            Math.Max(0, itemBounds.Width - (fillInset * 2)),
                            itemBounds.Height);

                        // ⭐ NEW: Draw hover background before selection
                        if (i == _hoveredIndex && i != _owner.SelectedIndex)
                        {
                            using (SolidBrush hoverBrush = new SolidBrush(_owner._itemHoverColor))
                                g.FillRectangle(hoverBrush, fillBounds);
                        }

                        if (i == _owner.SelectedIndex)
                        {
                            using (SolidBrush selBrush = new SolidBrush(Color.FromArgb(240, 244, 252)))
                                g.FillRectangle(selBrush, fillBounds);
                            if (_owner.ShowSelectedCheckmark) DrawSelectedCheckmark(g, itemBounds);
                        }

                        DrawItemText(g, i, itemBounds, hovered: i == _hoveredIndex);
                        DrawItemSeparators(g, i, itemBounds);
                    }

                    g.Clip = oldClip;
                }
            }

            private Rectangle GetItemBounds(int index)
            {
                int visualIndex = index - _scrollOffset;
                int top = _itemsRect.Top + visualIndex * _owner._itemHeight;
                return new Rectangle(_itemsRect.Left, top, _itemsRect.Width, _owner._itemHeight);
            }

            private int HitTestItemIndex(Point p)
            {
                if (!_itemsRect.Contains(p)) return -1;
                int relativeY = p.Y - _itemsRect.Top;
                int visualIndex = relativeY / _owner._itemHeight;
                int actualIndex = visualIndex + _scrollOffset;
                if (actualIndex < 0 || actualIndex >= _owner._items.Count) return -1;
                return actualIndex;
            }

            private void DrawItemText(Graphics g, int index, Rectangle bounds, bool hovered = false)
            {
                var item = _owner._items[index];
                // 10 lines the item text up with the section headers, which also inset by 10.
                int leftPadding = 10;
                Rectangle textRect = new Rectangle(
                    bounds.Left + leftPadding,
                    bounds.Top,
                    bounds.Width - leftPadding - 10,
                    bounds.Height);

                RoundedComboBox.DrawItemContent(g, item, _owner._itemFont, textRect, Color.FromArgb(31, 31, 31),
                    _owner._itemImageSize, hovered, _owner._itemImageHoverGrow);
            }

            private void DrawSelectedCheckmark(Graphics g, Rectangle itemBounds)
            {
                // Tick sits on the RIGHT: the item text now starts at 10 to align with the section
                // headers, so a left-hand tick would sit underneath it.
                int size = 10;
                int cx = itemBounds.Right - 18;
                int cy = itemBounds.Top + itemBounds.Height / 2;

                using (Pen pen = new Pen(Color.FromArgb(0, 120, 215), 1.6f))
                {
                    g.DrawLines(pen, new[]
                    {
                        new Point(cx - size / 2, cy),
                        new Point(cx - 1, cy + size / 2),
                        new Point(cx + size / 2, cy - size / 2)
                    });
                }
            }

            // A section title: muted, bold and slightly smaller, aligned with the item text.
            private void DrawHeaderText(Graphics g, int index, Rectangle itemBounds)
            {
                RoundedComboBoxItem item = _owner._items[index];
                if (string.IsNullOrEmpty(item.Text))
                {
                    return;
                }

                using Font headerFont = new Font(
                    _owner._itemFont.FontFamily,
                    Math.Max(6.5f, _owner._itemFont.SizeInPoints - 0.5f),
                    FontStyle.Bold);

                // Nudged down 3px — the smaller bold header sits high against the taller item rows.
                RectangleF textRect = new RectangleF(
                    itemBounds.Left + 10,
                    itemBounds.Top + 5,
                    Math.Max(0, itemBounds.Width - 20),
                    itemBounds.Height);

                // GDI+ (DrawString), NOT GDI (TextRenderer): the dropdown is a layered/transparent
                // window, and TextRenderer draws nothing on it — same rule as DrawItemContent.
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                using SolidBrush brush = new SolidBrush(Color.FromArgb(120, 128, 140));
                g.DrawString(item.Text, headerFont, brush, textRect, sf);
            }

            private void DrawItemSeparators(Graphics g, int index, Rectangle itemBounds)
            {
                var item = _owner._items[index];
                int lineLeft = itemBounds.Left + 10;
                int lineRight = itemBounds.Right - 10;

                using (Pen pen = new Pen(_owner._separatorColor, 1))
                {
                    if (item.SeparatorBefore)
                    {
                        int y = itemBounds.Top;
                        g.DrawLine(pen, lineLeft, y, lineRight, y);
                    }
                    if (item.SeparatorAfter)
                    {
                        int y = itemBounds.Bottom - 1;
                        g.DrawLine(pen, lineLeft, y, lineRight, y);
                    }
                }
            }

            private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
            {
                GraphicsPath path = new GraphicsPath();
                int d = radius * 2;
                if (radius < 1)
                {
                    path.AddRectangle(rect);
                    return path;
                }
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }

    /// <summary>Which end of a control something sits on. Shared by the arrow and the popup.</summary>
    public enum RoundedComboBoxSide
    {
        Right = 0,
        Left = 1,
    }

    public class RoundedComboBoxDesigner : ControlDesigner
    {
        private DesignerActionListCollection? _actionLists;

        public override void InitializeNewComponent(IDictionary? defaultValues)
        {
            base.InitializeNewComponent(defaultValues);

            if (Component is RoundedComboBox comboBox)
            {
                TypeDescriptor.GetProperties(comboBox)["Text"]?.SetValue(comboBox, string.Empty);
            }
        }

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    _actionLists.Add(new RoundedComboBoxActionList(Component));
                    _actionLists.AddRange(base.ActionLists);
                }

                return _actionLists;
            }
        }
    }

    public class RoundedComboBoxActionList : DesignerActionList
    {
        public RoundedComboBoxActionList(IComponent component) : base(component) { }

        private RoundedComboBox? ComboBox => Component as RoundedComboBox;

        private IComponentChangeService? ChangeService =>
            (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

        private IDesignerHost? DesignerHost =>
            (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

        public void ExpandToWidestItem()
        {
            if (ComboBox is not { } comboBox)
            {
                return;
            }

            PropertyDescriptor? comboWidthProp = TypeDescriptor.GetProperties(comboBox)["ComboBoxWidth"];
            PropertyDescriptor? widthProp = TypeDescriptor.GetProperties(comboBox)["Width"];
            int oldComboWidth = comboBox.ComboBoxWidth;
            int oldWidth = comboBox.Width;

            using var tx = DesignerHost?.CreateTransaction("Expand ComboBox to Widest Item");
            ChangeService?.OnComponentChanging(comboBox, comboWidthProp);
            ChangeService?.OnComponentChanging(comboBox, widthProp);

            comboBox.ExpandComboBoxToWidestItem();

            ChangeService?.OnComponentChanged(comboBox, comboWidthProp, oldComboWidth, comboBox.ComboBoxWidth);
            ChangeService?.OnComponentChanged(comboBox, widthProp, oldWidth, comboBox.Width);
            tx?.Commit();
        }

        public void ExpandLabelToFit()
        {
            if (ComboBox is not { } comboBox)
                return;

            if (string.IsNullOrWhiteSpace(comboBox.Text))
                return;

            int oldWidth = comboBox.Width;
            PropertyDescriptor? widthProp = TypeDescriptor.GetProperties(comboBox)["Width"];

            using var tx = DesignerHost?.CreateTransaction("Expand label to fit");
            ChangeService?.OnComponentChanging(comboBox, widthProp);
            comboBox.ExpandLabelToFitText();
            ChangeService?.OnComponentChanged(comboBox, widthProp, oldWidth, comboBox.Width);
            tx?.Commit();
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();
            items.Add(new DesignerActionHeaderItem("Sizing"));
            items.Add(new DesignerActionMethodItem(this, nameof(ExpandToWidestItem), "Expand to Widest Item", "Sizing", true));
            if (!string.IsNullOrWhiteSpace((Component as RoundedComboBox)?.Text))
                items.Add(new DesignerActionMethodItem(this, nameof(ExpandLabelToFit), "Expand Label to Fit", "Sizing", true));
            return items;
        }
    }

    public class RoundedComboBoxItem
    {
        public string Text { get; set; } = string.Empty;
        public object? Tag { get; set; }
        public Image? Image { get; set; }   // when set, drawn (ItemImageSize square) instead of Text

        /// <summary>
        /// Shown while the row is hovered. Falls back to <see cref="Image"/> when unset, so a
        /// highlight version is opt-in per item — same A/hover model as IconButton.
        /// </summary>
        public Image? ImageHover { get; set; }
        public bool SeparatorBefore { get; set; }
        public bool SeparatorAfter { get; set; }

        /// <summary>
        /// A non-selectable section title, so one list can carry several groups (e.g. "Max
        /// resolution" over the heights, "Audio" over the audio formats). Headers cannot be
        /// clicked, hovered or selected — they are drawn as a muted, bold label.
        /// </summary>
        public bool IsHeader { get; set; }

        public RoundedComboBoxItem() { }
        public RoundedComboBoxItem(string text) { Text = text; }
        public override string ToString() => Text;
    }
}
