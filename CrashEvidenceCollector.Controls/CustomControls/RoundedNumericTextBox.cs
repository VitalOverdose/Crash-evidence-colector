#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum NumericSpinnerMode
    {
        /// <summary>The classic full control: bordered body around icon/caption/value/spinner.</summary>
        Full,

        /// <summary>Caption and value stay visible but float borderless — the rounded border
        /// capsules ONLY the spinner block (rounded cap on its left edge).</summary>
        CapSpinner,

        /// <summary>No value field, no caption — the control IS the spinner capsule.</summary>
        SpinnerOnly
    }

    [DesignerCategory("Code")]
    public class RoundedNumericTextBox : RoundedTextBox
    {
        private readonly Timer repeatTimer;

        private decimal _value;
        private decimal _minimum;
        private decimal _maximum = 100;
        private decimal _increment = 1;
        private int _decimalPlaces;
        private bool _thousandsSeparator;
        private bool _base60Mode;
        private string _captionText = string.Empty;

        private int _buttonWidth = 34;
        private int _borderThickness = 2;
        private int _hoverBorderThickness = 1;
        private int _selectedBorderThickness = 2;
        private int _textGap = 8;
        private int _numericButtonGap = 2;
        private int _numericFieldCharacters;

        // Zone-margin layout + modes — see the "ZONE MARGINS & MODES" property block.
        private Padding _marginIcon = Padding.Empty;
        private Padding _marginText = new Padding(8, 0, 0, 0);
        private Padding _marginNumeric = new Padding(8, 0, 2, 0);
        private Rectangle _captionRect = Rectangle.Empty;
        private NumericSpinnerMode _spinnerMode = NumericSpinnerMode.Full;
        private bool _hideNumbers;
        private Control? _externalDisplay;

        private Color _hoverBorderColor = Color.FromArgb(0, 120, 215);
        private Color _selectedBorderColor = Color.FromArgb(0, 120, 215);
        private Color _selectedInnerColor = Color.FromArgb(230, 240, 250);
        private Color _numericFocusBackColor = Color.FromArgb(230, 240, 250);
        private Color _separatorColor = Color.FromArgb(220, 220, 220);
        private int _triangleSize = 4;
        private Color _buttonColor = Color.Empty;
        private Color _buttonHoverColor = Color.FromArgb(240, 244, 252);
        private Color _buttonPressColor = Color.FromArgb(220, 230, 245);
        private Color _disabledInnerColor = Color.FromArgb(250, 250, 250);
        private Color _disabledBorderColor = Color.FromArgb(220, 220, 220);
        private Color _disabledTextColor = Color.FromArgb(160, 160, 160);

        private bool _upHovered;
        private bool _downHovered;
        private bool _upPressed;
        private bool _downPressed;
        private bool _editing;

        // Where the value field sits, kept even while the native editor is hidden: a DISABLED
        // Win32 EDIT ignores ForeColor and draws its own greyed text (a red ForeColor comes out
        // desaturated, never DisabledTextColor), so while disabled the editor is hidden and the
        // value is painted here, the way the caption already is.
        private Rectangle _fieldRect = Rectangle.Empty;
        private bool _hoverPropertiesEnabled = true;
        private bool _isSelected;
        private bool _scaleHorizontalChromeForDpi;

        public event EventHandler? ValueChanged;

        public RoundedNumericTextBox()
        {
            InnerTextBox.AutoSize = true;
            InnerTextBox.TextAlign = HorizontalAlignment.Left;
            InnerTextBox.GotFocus += (_, _) =>
            {
                _editing = true;
                UpdateTextBox();
                InnerTextBox.SelectAll();
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            };
            InnerTextBox.LostFocus += (_, _) =>
            {
                _editing = false;
                ApplyTextBoxValue();
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            };
            InnerTextBox.MouseEnter += (_, _) =>
            {
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            };
            InnerTextBox.MouseLeave += (_, _) =>
            {
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            };
            InnerTextBox.KeyPress += TextBox_KeyPress;
            InnerTextBox.KeyDown += TextBox_KeyDown;

            InnerContainer.MouseMove += Container_MouseMove;
            InnerContainer.MouseEnter += (_, _) =>
            {
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            };
            InnerContainer.MouseLeave += (_, _) =>
            {
                _upHovered = false;
                _downHovered = false;
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            };
            InnerContainer.MouseDown += Container_MouseDown;
            InnerContainer.MouseUp += Container_MouseUp;

            repeatTimer = new Timer { Interval = 50 };
            repeatTimer.Tick += RepeatTimer_Tick;

            Size = new Size(120, 35);
            TextBoxBackColor = Color.White;
            UpdateTextBox();
            RepositionTextBox();
        }

        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [Bindable(true)]
        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Caption drawn inside the rounded numeric control, to the left of the numeric editor.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [AllowNull]
        public override string Text
        {
            get => _captionText;
            set
            {
                string newText = value ?? string.Empty;
                if (_captionText == newText)
                    return;

                _captionText = newText;
                RepositionTextBox();
                InnerContainer.Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "0")]
        public decimal Value
        {
            get => _value;
            set
            {
                decimal newValue = CoerceValue(value);
                if (newValue == _value)
                {
                    UpdateTextBox();
                    return;
                }

                _value = newValue;
                UpdateTextBox();
                ValueChanged?.Invoke(this, EventArgs.Empty);
                InnerContainer.Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "0")]
        public decimal Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum < _minimum)
                    _maximum = _minimum;
                Value = _value;
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "100")]
        public decimal Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_minimum > _maximum)
                    _minimum = _maximum;
                Value = _value;
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "1")]
        public decimal Increment
        {
            get => _increment;
            set => _increment = value > 0 ? value : 1;
        }

        [Category("Data")]
        [DefaultValue(0)]
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                _decimalPlaces = Math.Max(0, value);
                UpdateTextBox();
                RepositionTextBox();
            }
        }

        [Category("Data")]
        [DefaultValue(false)]
        public bool ThousandsSeparator
        {
            get => _thousandsSeparator;
            set
            {
                _thousandsSeparator = value;
                UpdateTextBox();
                RepositionTextBox();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("When true, value wraps between 0 and 59.")]
        public bool Base60Mode
        {
            get => _base60Mode;
            set
            {
                _base60Mode = value;
                if (_base60Mode)
                {
                    _minimum = 0;
                    _maximum = 59;
                    Value = _value;
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(34)]
        public int ButtonWidth
        {
            get => _buttonWidth;
            set
            {
                _buttonWidth = Math.Max(18, value);
                RepositionTextBox();
                InnerContainer.Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(2)]
        [Description("0 switches the border off entirely. Flat mode + 0 + no ButtonColor = bare triangles on the parent surface.")]
        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                _borderThickness = Math.Max(0, value);
                InnerContainer.Invalidate();
            }
        }

        [Category("Appearance - Hover")]
        public Color HoverBorderColor
        {
            get => _hoverBorderColor;
            set { _hoverBorderColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Hover")]
        [DefaultValue(1)]
        public int HoverBorderThickness
        {
            get => _hoverBorderThickness;
            set
            {
                _hoverBorderThickness = Math.Max(0, value);
                InnerContainer.Invalidate();
            }
        }

        [Category("Appearance - Hover")]
        public Color HoverInnerColor
        {
            get => HoverBackColor;
            set { HoverBackColor = value; SyncTextBoxColors(); InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Hover")]
        [DefaultValue(true)]
        public bool HoverPropertiesEnabled
        {
            get => _hoverPropertiesEnabled;
            set { _hoverPropertiesEnabled = value; SyncTextBoxColors(); InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Selected")]
        [DefaultValue(false)]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; SyncTextBoxColors(); InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Selected")]
        public Color SelectedBorderColor
        {
            get => _selectedBorderColor;
            set { _selectedBorderColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Selected")]
        [DefaultValue(2)]
        public int SelectedBorderThickness
        {
            get => _selectedBorderThickness;
            set
            {
                _selectedBorderThickness = Math.Max(0, value);
                InnerContainer.Invalidate();
            }
        }

        [Category("Appearance - Selected")]
        public Color SelectedInnerColor
        {
            get => _selectedInnerColor;
            set { _selectedInnerColor = value; SyncTextBoxColors(); InnerContainer.Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Background of the whole interior — icon, caption and numeric zones all follow this one colour.")]
        public Color InnerColor
        {
            get => TextBoxBackColor;
            set
            {
                TextBoxBackColor = value;
                SyncTextBoxColors();
                InnerContainer.Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Background of the numeric editor while it has keyboard focus. Every other state follows InnerColor.")]
        public Color NumericFocusBackColor
        {
            get => _numericFocusBackColor;
            set { _numericFocusBackColor = value; SyncTextBoxColors(); InnerContainer.Invalidate(); }
        }
        private bool ShouldSerializeNumericFocusBackColor() => _numericFocusBackColor != Color.FromArgb(230, 240, 250);
        private void ResetNumericFocusBackColor() => NumericFocusBackColor = Color.FromArgb(230, 240, 250);

        [Category("Appearance - Buttons")]
        public Color SeparatorColor
        {
            get => _separatorColor;
            set { _separatorColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Buttons")]
        [DefaultValue(4)]
        [Description("Half-width of the up/down triangles in pixels (before DPI chrome scaling).")]
        public int TriangleSize
        {
            get => _triangleSize;
            set
            {
                _triangleSize = Math.Max(2, value);
                InnerContainer.Invalidate();
            }
        }

        [Category("Appearance - Buttons")]
        [Description("Resting face colour of the spinner block. Empty = inherit the body fill (and InnerColor in flat mode).")]
        public Color ButtonColor
        {
            get => _buttonColor;
            set { _buttonColor = value; InnerContainer.Invalidate(); }
        }
        private bool ShouldSerializeButtonColor() => _buttonColor != Color.Empty;
        private void ResetButtonColor() => ButtonColor = Color.Empty;

        [Category("Appearance - Buttons")]
        public Color ButtonHoverColor
        {
            get => _buttonHoverColor;
            set { _buttonHoverColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Buttons")]
        public Color ButtonPressColor
        {
            get => _buttonPressColor;
            set { _buttonPressColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledInnerColor
        {
            get => _disabledInnerColor;
            set { _disabledInnerColor = value; SyncTextBoxColors(); InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledBorderColor
        {
            get => _disabledBorderColor;
            set { _disabledBorderColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance - Disabled")]
        public Color DisabledTextColor
        {
            get => _disabledTextColor;
            set { _disabledTextColor = value; InnerContainer.Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(HorizontalAlignment.Left)]
        public HorizontalAlignment ValueTextAlignment
        {
            get => InnerTextBox.TextAlign;
            set => InnerTextBox.TextAlign = value;
        }

        // ============================================================================
        // ZONE MARGINS & MODES — the one layout system (2026-07-31 redesign)
        // ----------------------------------------------------------------------------
        // The control lays out as a left-flowing walk: [icon][caption][numeric][spinner].
        // Each zone owns a full Padding; an absent zone collapses to nothing. The numeric
        // editor takes ALL remaining width up to the spinner, so values sit straight
        // after the caption and can never be clipped by a guessed field width.
        // Top/Bottom of each Padding fine-shift that zone off vertical centre.
        // ============================================================================

        [Category("Layout")]
        [Description("How the control presents: Full body, CapSpinner (caption/value float borderless, the border capsules only the spinner), or SpinnerOnly (just the spinner capsule).")]
        [DefaultValue(NumericSpinnerMode.Full)]
        public NumericSpinnerMode SpinnerMode
        {
            get => _spinnerMode;
            set
            {
                if (_spinnerMode == value) return;
                _spinnerMode = value;
                RepositionTextBox();
                InnerContainer.Invalidate();
            }
        }

        [Category("Layout")]
        [Description("Hide the numeric value display (the value still exists — pair with ExternalDisplay to show it elsewhere).")]
        [DefaultValue(false)]
        public bool HideNumbers
        {
            get => _hideNumbers;
            set
            {
                if (_hideNumbers == value) return;
                _hideNumbers = value;
                RepositionTextBox();
                InnerContainer.Invalidate();
            }
        }

        [Category("Behavior")]
        [Description("A control whose Text this spinner drives directly (formatted the same as the value display). Lets a SpinnerOnly/HideNumbers spinner show its value anywhere.")]
        [DefaultValue(null)]
        public Control? ExternalDisplay
        {
            get => _externalDisplay;
            set
            {
                _externalDisplay = value;
                PushExternalDisplay();
            }
        }

        [Category("Layout")]
        [Description("Margins around the left icon zone (collapses entirely when no LeftIcon is set).")]
        public Padding MarginIcon
        {
            get => _marginIcon;
            set { _marginIcon = value; RepositionTextBox(); InnerContainer.Invalidate(); }
        }
        private bool ShouldSerializeMarginIcon() => _marginIcon != Padding.Empty;
        private void ResetMarginIcon() => MarginIcon = Padding.Empty;

        [Category("Layout")]
        [Description("Margins around the caption text zone (collapses entirely when Text is empty). Top/Bottom shift the caption off vertical centre.")]
        public Padding MarginText
        {
            get => _marginText;
            set { _marginText = value; RepositionTextBox(); InnerContainer.Invalidate(); }
        }
        private bool ShouldSerializeMarginText() => _marginText != new Padding(8, 0, 0, 0);
        private void ResetMarginText() => MarginText = new Padding(8, 0, 0, 0);

        [Category("Layout")]
        [Description("Margins around the numeric editor zone (Right = gap before the spinner). Top/Bottom shift the editor off vertical centre.")]
        public Padding MarginNumeric
        {
            get => _marginNumeric;
            set { _marginNumeric = value; RepositionTextBox(); InnerContainer.Invalidate(); }
        }
        private bool ShouldSerializeMarginNumeric() => _marginNumeric != new Padding(8, 0, 2, 0);
        private void ResetMarginNumeric() => MarginNumeric = new Padding(8, 0, 2, 0);

        // LEGACY, fully hidden — superseded by the zone margins; kept only so old designer
        // lines compile. They stop re-serialising, so they self-delete on each form's resave.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TextGap
        {
            get => _textGap;
            set => _textGap = Math.Max(0, value);
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int NumericButtonGap
        {
            get => _numericButtonGap;
            set => _numericButtonGap = Math.Max(0, value);
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int NumericFieldCharacters
        {
            get => _numericFieldCharacters;
            set => _numericFieldCharacters = Math.Max(0, value);
        }

        // ============================================================================
        // HIDDEN BASE MEMBERS
        // ----------------------------------------------------------------------------
        // The RightButton icon-slot family belongs to plain RoundedTextBox (open-folder
        // buttons on path boxes etc.). On a numeric box the SPINNER owns the right edge,
        // so these members can only confuse — shadowed out of the grid and serialisation.
        // They remain functional pass-throughs so any old designer lines still compile.
        // ============================================================================

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image? RightButtonA { get => base.RightButtonA; set => base.RightButtonA = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image? RightButtonAHover { get => base.RightButtonAHover; set => base.RightButtonAHover = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image? RightButtonAPressed { get => base.RightButtonAPressed; set => base.RightButtonAPressed = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image? RightButtonB { get => base.RightButtonB; set => base.RightButtonB = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image? RightButtonBHover { get => base.RightButtonBHover; set => base.RightButtonBHover = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Image? RightButtonBPressed { get => base.RightButtonBPressed; set => base.RightButtonBPressed = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool RightButtonAEnabled { get => base.RightButtonAEnabled; set => base.RightButtonAEnabled = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool RightButtonBEnabled { get => base.RightButtonBEnabled; set => base.RightButtonBEnabled = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new int RightButtonAOffset { get => base.RightButtonAOffset; set => base.RightButtonAOffset = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new int RightButtonBOffset { get => base.RightButtonBOffset; set => base.RightButtonBOffset = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new int RightButtonAHoverGrow { get => base.RightButtonAHoverGrow; set => base.RightButtonAHoverGrow = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new int RightButtonBHoverGrow { get => base.RightButtonBHoverGrow; set => base.RightButtonBHoverGrow = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Size ButtonImageSize { get => base.ButtonImageSize; set => base.ButtonImageSize = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Size RightButtonAImageSize { get => base.RightButtonAImageSize; set => base.RightButtonAImageSize = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Size RightButtonBImageSize { get => base.RightButtonBImageSize; set => base.RightButtonBImageSize = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler? RightButtonAClicked
        {
            add => base.RightButtonAClicked += value;
            remove => base.RightButtonAClicked -= value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler? RightButtonBClicked
        {
            add => base.RightButtonBClicked += value;
            remove => base.RightButtonBClicked -= value;
        }

        // Redundant aliases: the numeric grid speaks InnerColor/HoverInnerColor (the app's
        // newer idiom, shared with the panels) — the base spellings hide so the same value
        // stops appearing under two names.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color TextBoxBackColor { get => base.TextBoxBackColor; set => base.TextBoxBackColor = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color HoverBackColor { get => base.HoverBackColor; set => base.HoverBackColor = value; }

        // Text-editing surface that has no meaning on a one-line numeric value.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool Multiline { get => base.Multiline; set => base.Multiline = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool WordWrap { get => base.WordWrap; set => base.WordWrap = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ScrollBars ScrollBars { get => base.ScrollBars; set => base.ScrollBars = value; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new string PlaceholderText { get => base.PlaceholderText; set => base.PlaceholderText = value; }

        [Category("Layout")]
        [DefaultValue(false)]
        [Description("When true, horizontal spinner chrome grows with DPI. Default false keeps authored widths stable so text step-down handles high-DPI pressure.")]
        public bool ScaleHorizontalChromeForDpi
        {
            get => _scaleHorizontalChromeForDpi;
            set
            {
                if (_scaleHorizontalChromeForDpi == value)
                    return;

                _scaleHorizontalChromeForDpi = value;
                RepositionTextBox();
                InnerContainer.Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            InnerTextBox.Enabled = Enabled;
            SyncTextBoxColors();
            RepositionTextBox();   // swaps between the native editor and the painted value
            InnerContainer.Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RepositionTextBox();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Enabled || ReadOnly)
                return;

            if (e.Delta > 0)
                IncrementValue();
            else if (e.Delta < 0)
                DecrementValue();
        }

        protected override void Container_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, InnerContainer.Width - 1, InnerContainer.Height - 1);
            bool hovered = InnerContainer.ClientRectangle.Contains(InnerContainer.PointToClient(Cursor.Position));
            bool hoverVisual = _hoverPropertiesEnabled && hovered;
            // Body colour comes from the shared rule (GetSurfaceColor override below), the
            // same one the editor strip is stamped with - they cannot drift apart any more.
            Color bgColor = CurrentSurfaceColor;
            Color borderColor = !Enabled
                ? _disabledBorderColor
                : _isSelected
                    ? _selectedBorderColor
                    : hoverVisual
                        ? _hoverBorderColor
                        : InnerTextBox.Focused
                            ? FocusColor
                            : BorderColor;
            int borderThickness = _isSelected
                ? _selectedBorderThickness
                : hoverVisual
                    ? _hoverBorderThickness
                    : _borderThickness;

            PaintParentSurfaceBehind(InnerContainer, e.Graphics);

            // Full mode: the body wraps everything. Capsule modes: the border wraps ONLY the
            // spinner block (rounded cap on its left edge); caption/value float on the parent
            // surface (CapSpinner) or don't exist at all (SpinnerOnly).
            //
            // FlatLabelMode: the ZONES paint nothing at all — the parent's replayed surface
            // (gradients included) is the background, which is what blending means. Only the
            // SPINNER keeps a face and border (InnerColor face, normal state border), so it
            // stays a visible control; BorderThickness 0 flattens it too if wanted.
            if (FlatLabelMode)
            {
                bool hasFace = _buttonColor.A > 0;
                bool hasEdge = borderThickness > 0;

                // Total-minimum mode: flat + BorderThickness 0 + no ButtonColor paints NO
                // capsule at all — bare triangles floating on the parent surface.
                if (hasFace || hasEdge)
                {
                    Rectangle capsule = GetSpinnerCapsuleRect();
                    using GraphicsPath capsulePath = GetRoundedRect(
                        capsule, Math.Min(CornerRadius, Math.Max(1, Math.Min(capsule.Width, capsule.Height) / 2)));
                    using SolidBrush capsuleBrush = new SolidBrush(hasFace ? _buttonColor : CurrentSurfaceColor);

                    e.Graphics.FillPath(capsuleBrush, capsulePath);

                    if (hasEdge)
                    {
                        // Half-pen inset so the stroke sits ON the fill rather than beside it,
                        // otherwise the parent surface survives between the two anti-aliased
                        // edges as a pale ring.
                        float capsuleInset = borderThickness / 2f;
                        RectangleF capsuleStroke = RectangleF.Inflate(capsule, -capsuleInset, -capsuleInset);
                        float capsuleRadius = Math.Max(0f,
                            Math.Min(CornerRadius, Math.Max(1, Math.Min(capsule.Width, capsule.Height) / 2)) - capsuleInset);

                        using GraphicsPath capsuleBorder = GetRoundedRectF(capsuleStroke, capsuleRadius);
                        using Pen capsulePen = new Pen(borderColor, borderThickness);
                        e.Graphics.DrawPath(capsulePen, capsuleBorder);
                    }
                }
            }
            else
            {
                Rectangle bodyRect = _spinnerMode == NumericSpinnerMode.Full ? rect : GetSpinnerCapsuleRect();

                using GraphicsPath path = GetRoundedRect(bodyRect, Math.Min(CornerRadius, Math.Max(1, Math.Min(bodyRect.Width, bodyRect.Height) / 2)));
                using SolidBrush brush = new SolidBrush(bgColor);

                e.Graphics.FillPath(brush, path);

                // Thickness 0 = border off (a zero-width GDI+ pen would still draw a hairline).
                if (borderThickness > 0)
                {
                    float inset = borderThickness / 2f;
                    RectangleF strokeBounds = RectangleF.Inflate(bodyRect, -inset, -inset);
                    float strokeRadius = Math.Max(0f,
                        Math.Min(CornerRadius, Math.Max(1, Math.Min(bodyRect.Width, bodyRect.Height) / 2)) - inset);

                    using GraphicsPath borderPath = GetRoundedRectF(strokeBounds, strokeRadius);
                    using Pen pen = new Pen(borderColor, borderThickness);
                    e.Graphics.DrawPath(pen, borderPath);
                }
            }

            if (_spinnerMode != NumericSpinnerMode.SpinnerOnly)
                DrawCaption(e.Graphics);

            if (!Enabled && !_fieldRect.IsEmpty)
                DrawDisabledValue(e.Graphics);

            DrawButtons(e.Graphics);
        }

        // The bordered capsule around the spinner block in CapSpinner/SpinnerOnly modes.
        private Rectangle GetSpinnerCapsuleRect()
        {
            Rectangle buttons = GetButtonRect();
            return new Rectangle(buttons.Left - 1, buttons.Top - 1, buttons.Width + 1, buttons.Height + 1);
        }

        protected override void RepositionTextBox()
        {
            if (InnerTextBox.IsDisposed || InnerContainer.IsDisposed)
                return;

            // The value field exists unless the mode drops it or the numbers are hidden.
            bool showField = _spinnerMode != NumericSpinnerMode.SpinnerOnly && !_hideNumbers;

            // The native editor only shows while enabled; disabled, the value is painted in
            // Container_Paint instead (see _fieldRect). Written unconditionally: the Visible
            // getter reports EFFECTIVE visibility, so guarding on it can skip a needed write.
            InnerTextBoxHost.Visible = showField && Enabled;

            // Left-flowing zone walk: [icon][caption][numeric →][spinner]. Each zone owns its
            // margins; an absent zone collapses. The numeric editor takes ALL the remaining
            // width to the spinner — values sit straight after the caption, never clipped
            // against the buttons by a guessed field width.
            int x = GetTextBoxInset();

            if (LeftIcon != null)
                x += ScaleHorizontalChrome(_marginIcon.Left) + ScaleValue(IconSize.Width) + ScaleHorizontalChrome(_marginIcon.Right);

            _captionRect = Rectangle.Empty;
            if (_spinnerMode != NumericSpinnerMode.SpinnerOnly && !string.IsNullOrWhiteSpace(_captionText))
            {
                int captionLeft = x + ScaleHorizontalChrome(_marginText.Left);
                int captionWidth = TextRenderer.MeasureText(_captionText, Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

                // Full container height with a Top/Bottom fine-shift; DrawCaption centres in it.
                _captionRect = new Rectangle(captionLeft, _marginText.Top - _marginText.Bottom,
                    captionWidth, InnerContainer.ClientSize.Height);

                x = captionLeft + captionWidth + ScaleHorizontalChrome(_marginText.Right);
            }

            if (!showField)
            {
                _fieldRect = Rectangle.Empty;
                InnerContainer.Invalidate();
                return;
            }

            int left = x + ScaleHorizontalChrome(_marginNumeric.Left);
            int rightEdge = GetButtonRect().Left - ScaleHorizontalChrome(_marginNumeric.Right);
            int width = Math.Max(0, rightEdge - left);

            int borderInset = GetTextBoxHostBorderInset();
            int availableHeight = Math.Max(1, InnerContainer.ClientSize.Height - (borderInset * 2));
            int height = Math.Max(1, Math.Min(InnerTextBox.PreferredHeight, availableHeight));
            int top = borderInset + Math.Max(0, (availableHeight - height) / 2)
                      + _marginNumeric.Top - _marginNumeric.Bottom;

            SetInnerTextBoxBounds(left, top, width, height);
            _fieldRect = new Rectangle(left, top, width, height);
            InnerContainer.Invalidate();
        }

        // The disabled stand-in for the native editor: same font, same rect, same left inset
        // the EDIT control uses, in DisabledTextColor - so caption and value grey out together.
        private void DrawDisabledValue(Graphics g)
        {
            Rectangle rect = _fieldRect;
            rect.X += ScaleValue(1);
            rect.Width = Math.Max(0, rect.Width - ScaleValue(1));

            TextRenderer.DrawText(g, GetDisplayValueText(_value), InnerTextBox.Font, rect, _disabledTextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void DrawButtons(Graphics g)
        {
            Rectangle buttonRect = GetButtonRect();
            Rectangle upRect = GetUpButtonRect();
            Rectangle downRect = GetDownButtonRect();

            // Hover/press fills clip to the body the buttons live in — the full control in
            // Full mode, the spinner capsule in the capsule modes.
            Rectangle clipRect = _spinnerMode == NumericSpinnerMode.Full
                ? new Rectangle(0, 0, InnerContainer.Width - 1, InnerContainer.Height - 1)
                : GetSpinnerCapsuleRect();

            using Region oldClip = g.Clip.Clone();
            using GraphicsPath path = GetRoundedRect(
                clipRect,
                Math.Min(CornerRadius, Math.Max(1, Math.Min(clipRect.Width, clipRect.Height) / 2)));
            g.SetClip(path);

            // Resting face first (Empty inherits the body fill), then state fills on top.
            if (_buttonColor.A > 0)
                FillButton(g, buttonRect, _buttonColor);

            if (Enabled)
            {
                if (_upPressed)
                    FillButton(g, upRect, _buttonPressColor);
                else if (_upHovered)
                    FillButton(g, upRect, _buttonHoverColor);

                if (_downPressed)
                    FillButton(g, downRect, _buttonPressColor);
                else if (_downHovered)
                    FillButton(g, downRect, _buttonHoverColor);
            }

            g.Clip = oldClip;

            // Total-minimum (flat, no face, no border): bare triangles — a floating separator
            // line would just be debris.
            bool capsuleHasChrome = !FlatLabelMode || _buttonColor.A > 0 || _borderThickness > 0;
            if (capsuleHasChrome)
            {
                using Pen separatorPen = new Pen(_separatorColor, 1);
                int inset = ScaleHorizontalChrome(4);
                int midY = upRect.Bottom;

                // The vertical divider splits spinner from text area — Full mode only, and not
                // in flat (there the spinner is bordered as a capsule, whose rounded border IS
                // the left edge; a straight line would flatten the cap).
                if (_spinnerMode == NumericSpinnerMode.Full && !FlatLabelMode)
                    g.DrawLine(separatorPen, buttonRect.Left, buttonRect.Top + inset, buttonRect.Left, buttonRect.Bottom - inset);

                g.DrawLine(separatorPen, buttonRect.Left + inset, midY, buttonRect.Right - inset, midY);
            }

            Color arrowColor = Enabled ? ForeColor : _disabledTextColor;
            DrawArrow(g, upRect, arrowColor, true);
            DrawArrow(g, downRect, arrowColor, false);
        }

        private static void FillButton(Graphics g, Rectangle rect, Color color)
        {
            using SolidBrush brush = new SolidBrush(color);
            g.FillRectangle(brush, rect);
        }

        private void DrawArrow(Graphics g, Rectangle rect, Color color, bool up)
        {
            int size = ScaleHorizontalChrome(_triangleSize);
            int cx = rect.Left + rect.Width / 2;
            int cy = rect.Top + rect.Height / 2;

            Point p1;
            Point p2;
            Point p3;

            if (up)
            {
                p1 = new Point(cx - size, cy + size / 2);
                p2 = new Point(cx + size, cy + size / 2);
                p3 = new Point(cx, cy - size / 2);
            }
            else
            {
                p1 = new Point(cx - size, cy - size / 2);
                p2 = new Point(cx + size, cy - size / 2);
                p3 = new Point(cx, cy + size / 2);
            }

            using SolidBrush brush = new SolidBrush(color);
            g.FillPolygon(brush, new[] { p1, p2, p3 });
        }

        private int GetTextBoxInset()
        {
            int thickestBorder = Math.Max(_borderThickness, Math.Max(_hoverBorderThickness, _selectedBorderThickness));
            return ScaleValue(thickestBorder) + ScaleValue(2);
        }

        protected override int GetTextBoxHostBorderInset()
        {
            int thickestBorder = Math.Max(_borderThickness, Math.Max(_hoverBorderThickness, _selectedBorderThickness));
            return ScaleValue(thickestBorder);
        }

        // Computed by the zone walk in RepositionTextBox — one source of truth for layout.
        private Rectangle GetCaptionRect() =>
            string.IsNullOrWhiteSpace(_captionText) ? Rectangle.Empty : _captionRect;

        private void DrawCaption(Graphics g)
        {
            Rectangle captionRect = GetCaptionRect();
            if (captionRect.Width <= 0 || captionRect.Height <= 0)
                return;

            Color textColor = Enabled ? ForeColor : _disabledTextColor;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, _captionText, Font, captionRect, textColor, flags);
        }

        private Rectangle GetButtonRect()
        {
            int width = Math.Min(Math.Max(18, ScaleHorizontalChrome(_buttonWidth > 0 ? _buttonWidth : 34)), Math.Max(18, InnerContainer.ClientSize.Width / 2));
            return new Rectangle(InnerContainer.ClientSize.Width - width - 1, 1, width, Math.Max(1, InnerContainer.ClientSize.Height - 2));
        }

        private int ScaleHorizontalChrome(int value)
        {
            return _scaleHorizontalChromeForDpi ? ScaleValue(value) : value;
        }

        private Rectangle GetUpButtonRect()
        {
            Rectangle rect = GetButtonRect();
            return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2);
        }

        private Rectangle GetDownButtonRect()
        {
            Rectangle rect = GetButtonRect();
            int midY = rect.Y + rect.Height / 2;
            return new Rectangle(rect.X, midY, rect.Width, rect.Bottom - midY);
        }

        private void Container_MouseMove(object? sender, MouseEventArgs e)
        {
            bool upHovered = GetUpButtonRect().Contains(e.Location);
            bool downHovered = GetDownButtonRect().Contains(e.Location);

            if (upHovered == _upHovered && downHovered == _downHovered)
                return;

            _upHovered = upHovered;
            _downHovered = downHovered;
            SyncTextBoxColors();
            InnerContainer.Invalidate();
        }

        /// <summary>
        /// The ONE interior rule for the numeric box: disabled, selected, hover, normal. The
        /// base stamps it on the editor strip and paints the body from it, so a value set in
        /// the designer for any of these states colours the whole inside, not one strip of it.
        /// Flat mode still blends with the parent, in every state.
        /// </summary>
        protected override Color GetSurfaceColor(bool hovered)
        {
            if (FlatLabelMode)
                return base.GetSurfaceColor(hovered);

            if (!Enabled) return _disabledInnerColor;
            if (_isSelected) return _selectedInnerColor;
            if (_hoverPropertiesEnabled && hovered) return HoverBackColor;
            return TextBoxBackColor;
        }

        // Keyboard focus on the numeric editor gets its own colour - the one deliberate
        // departure from "the strip matches the body", and only while typing.
        protected override Color GetEditorSurfaceColor(bool hovered, bool focused) =>
            focused && Enabled ? _numericFocusBackColor : GetSurfaceColor(hovered);

        private void SyncTextBoxColors()
        {
            if (InnerTextBox.IsDisposed)
                return;

            InnerTextBox.ForeColor = Enabled ? ForeColor : _disabledTextColor;
            RefreshVisualState();   // re-reads hover, re-stamps the strip from the shared rule
        }

        private void Container_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!Enabled || ReadOnly || e.Button != MouseButtons.Left)
                return;

            if (GetUpButtonRect().Contains(e.Location))
            {
                _upPressed = true;
                IncrementValue();
                StartRepeat();
            }
            else if (GetDownButtonRect().Contains(e.Location))
            {
                _downPressed = true;
                DecrementValue();
                StartRepeat();
            }
            else
            {
                InnerTextBox.Focus();
            }

            InnerContainer.Invalidate();
        }

        private void Container_MouseUp(object? sender, MouseEventArgs e)
        {
            _upPressed = false;
            _downPressed = false;
            repeatTimer.Stop();
            InnerContainer.Invalidate();
        }

        private void StartRepeat()
        {
            repeatTimer.Interval = 400;
            repeatTimer.Start();
        }

        private void RepeatTimer_Tick(object? sender, EventArgs e)
        {
            if (repeatTimer.Interval > 50)
                repeatTimer.Interval = 50;

            if (_upPressed)
                IncrementValue();
            else if (_downPressed)
                DecrementValue();
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplyTextBoxValue();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                IncrementValue();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                DecrementValue();
                e.Handled = true;
            }
        }

        private void TextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            NumberFormatInfo format = CultureInfo.CurrentCulture.NumberFormat;
            string decimalSeparator = format.NumberDecimalSeparator;
            string negativeSign = format.NegativeSign;

            if (char.IsControl(e.KeyChar) ||
                char.IsDigit(e.KeyChar) ||
                e.KeyChar.ToString() == decimalSeparator ||
                e.KeyChar.ToString() == negativeSign ||
                e.KeyChar == ',')
            {
                return;
            }

            e.Handled = true;
        }

        private void IncrementValue()
        {
            if (_base60Mode && _value >= 59)
                Value = 0;
            else
                Value = _value + _increment;
        }

        private void DecrementValue()
        {
            if (_base60Mode && _value <= 0)
                Value = 59;
            else
                Value = _value - _increment;
        }

        private void ApplyTextBoxValue()
        {
            string raw = InnerTextBox.Text.Replace(",", "");
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal parsed))
                Value = parsed;
            else
                UpdateTextBox();
        }

        private decimal CoerceValue(decimal value)
        {
            if (_base60Mode)
                return Math.Max(0, Math.Min(59, value));

            return Math.Max(_minimum, Math.Min(_maximum, value));
        }

        private void UpdateTextBox()
        {
            if (InnerTextBox.IsDisposed)
                return;

            InnerTextBox.Text = _editing
                ? GetEditableValueText(_value)
                : GetDisplayValueText(_value);

            PushExternalDisplay();
        }

        // Mirrors the display-formatted value into the linked control, if any — how a
        // SpinnerOnly/HideNumbers spinner shows its value somewhere else in the layout.
        private void PushExternalDisplay()
        {
            if (_externalDisplay is { IsDisposed: false } target)
            {
                string text = GetDisplayValueText(_value);
                if (target.Text != text)
                    target.Text = text;
            }
        }

        private string GetEditableValueText(decimal value)
        {
            string format = (_thousandsSeparator ? "N" : "F") + _decimalPlaces.ToString(CultureInfo.InvariantCulture);
            return value.ToString(format, CultureInfo.CurrentCulture);
        }

        private string GetDisplayValueText(decimal value)
        {
            return GetEditableValueText(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                repeatTimer.Stop();
                repeatTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
