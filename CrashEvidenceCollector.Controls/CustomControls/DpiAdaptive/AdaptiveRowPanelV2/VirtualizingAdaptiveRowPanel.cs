using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ProfessorSnowsVideoDownloader.CustomControls;

namespace ProfessorSnowsVideoDownloader.CustomControls;

[Flags]
public enum VirtualControlState
{
    None = 0,
    Hot = 1,
    Pressed = 2,
    Focused = 4,
}

public interface IAdaptiveVirtualControl
{
    bool LayoutVisible { get; set; }
    int VirtualMinimumWidth { get; set; }
    bool AcceptsVirtualInput { get; }
    void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state);
    void PerformVirtualClick();

    // Double clicks matter to a handful of buttons (the nav bar's paste button). Default no-op
    // so plain wrappers stay two-liners; a wrapper that cares raises its own MouseDoubleClick.
    void PerformVirtualDoubleClick(Point clientPoint) { }
}

public interface IAdaptiveVirtualPointerControl
{
    void PerformVirtualClick(Point clientPoint);
}

/// <summary>
/// Demo adapter around the real EliteBowserVDS IconButton. At design time it is a
/// normal child control; at runtime its parent can hide it and use the same property
/// values as the source for parent painting and click behavior.
/// </summary>
[ToolboxItem(true)]
public class VirtualIconButton : IconButton, IAdaptiveVirtualControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(8)]
    public int VirtualMinimumWidth { get; set; } = 8;

    [Browsable(false)]
    public bool AcceptsVirtualInput => true;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => IconButtonVirtualRenderer.Draw(graphics, bounds, this, state);

    public void PerformVirtualClick() => OnClick(EventArgs.Empty);

    public void PerformVirtualDoubleClick(Point clientPoint)
        => OnMouseDoubleClick(new MouseEventArgs(
            MouseButtons.Left, 2, clientPoint.X, clientPoint.Y, 0));
}

/// <summary>
/// The real ModernButton on the design surface and a parent-painted button at runtime.
/// </summary>
[ToolboxItem(true)]
public class VirtualModernButton : ModernButton, IAdaptiveVirtualControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(20)]
    public int VirtualMinimumWidth { get; set; } = 20;

    [Browsable(false)]
    public bool AcceptsVirtualInput => true;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => ModernButtonVirtualRenderer.Draw(graphics, bounds, this, state);

    public void PerformVirtualClick() => OnClick(EventArgs.Empty);
}

[ToolboxItem(true)]
public class VirtualAdaptiveRowLabel : AdaptiveRowLabel, IAdaptiveVirtualControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(8)]
    public int VirtualMinimumWidth { get; set; } = 8;

    /// <summary>
    /// Labels-as-buttons are a house pattern (a close X, the clickable time label). Opt-in per
    /// label: a clickable one takes part in hit testing (hand cursor, hover/pressed states)
    /// and forwards clicks; a plain caption stays inert and never steals the row's clicks.
    /// </summary>
    [Category("Virtualization"), DefaultValue(false)]
    public bool VirtualClickable { get; set; }

    [Browsable(false)]
    public bool AcceptsVirtualInput => VirtualClickable;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => ModernButtonVirtualRenderer.Draw(graphics, bounds, this,
            VirtualClickable ? state : VirtualControlState.None);

    public void PerformVirtualClick() => OnClick(EventArgs.Empty);
}

[ToolboxItem(false)]
internal class VirtualRoundedDropdownButton : RoundedDropdownButton,
    IAdaptiveVirtualControl, IAdaptiveVirtualPointerControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(40)]
    public int VirtualMinimumWidth { get; set; } = 40;

    [Browsable(false)]
    public bool AcceptsVirtualInput => true;

    public new event EventHandler? ButtonClick;
    public new event EventHandler<Rectangle>? DropDownButtonClicked;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => RoundedDropdownButtonVirtualRenderer.Draw(graphics, bounds, this, state);

    public void PerformVirtualClick() => ButtonClick?.Invoke(this, EventArgs.Empty);

    public void PerformVirtualClick(Point clientPoint)
    {
        if (ImageBUse == IconButtonImageBUse.Toggle)
            Toggled = !Toggled;

        // Follows ArrowSide, like the renderer does: with the arrow on the left the BUTTON half
        // is everything to its right, and a fixed right-hand test would hand every click on the
        // word to the dropdown.
        bool onButtonHalf = ArrowSide == RoundedComboBoxSide.Left
            ? clientPoint.X >= ArrowZoneWidth
            : clientPoint.X < Width - ArrowZoneWidth;

        if (SplitButtonMode && onButtonHalf)
        {
            ButtonClick?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Arrow zone. Built-in list mode opens the real dropdown machinery (it anchors from
        // the parent when the control is handle-less); custom-popup mode raises the event and
        // lets the host show its own menu.
        if (DropDownEnabled)
        {
            ToggleDropDown();
            return;
        }

        Point screenLocation = Parent?.PointToScreen(Bounds.Location) ?? Bounds.Location;
        DropDownButtonClicked?.Invoke(this,
            new Rectangle(screenLocation, Bounds.Size));
    }
}

/// <summary>
/// The plain combo, virtualised.
///
/// It was on the hosted list while its own SUBCLASS (RoundedDropdownButton, which changes nothing
/// but two default property values) had a twin — so the same rendering and layout code was
/// virtual in one guise and handle-bound in the other. There was no technical reason for it:
/// RoundedComboBox has no text entry at all (no inner TextBox, no editable mode), it is fully
/// custom-drawn, and its item list is a ToolStripDropDown that already anchors from the parent
/// when the control is handle-less — which the virtual dropdown button relies on today with
/// DropDownEnabled = true.
/// </summary>
// internal + not in the toolbox, matching VirtualRoundedDropdownButton: RoundedComboBox itself is
// internal, so a public twin is a CS0060. Placed through the items editor / converter, not dragged.
[ToolboxItem(false)]
internal class VirtualRoundedComboBox : RoundedComboBox, IAdaptiveVirtualControl, IAdaptiveVirtualPointerControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(40)]
    public int VirtualMinimumWidth { get; set; } = 40;

    [Browsable(false)]
    public bool AcceptsVirtualInput => true;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => RoundedDropdownButtonVirtualRenderer.Draw(graphics, bounds, this, state);

    // A plain combo has no split zones: a click anywhere opens the list, exactly as clicking the
    // real one does. The dropdown button needs the point because its left body and right arrow
    // do different things; this one does not.
    public void PerformVirtualClick() => ToggleDropDown();

    public void PerformVirtualClick(Point clientPoint) => ToggleDropDown();
}

/// <summary>
/// Uses the real AdaptiveRowSpacer on the design surface. At runtime its authored
/// width remains as empty space in the shared layout without retaining a handle.
/// </summary>
[ToolboxItem(true)]
public class VirtualAdaptiveRowSpacer : AdaptiveRowSpacer, IAdaptiveVirtualControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(1)]
    public int VirtualMinimumWidth { get; set; } = 1;

    [Browsable(false)]
    public bool AcceptsVirtualInput => false;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
    {
    }

    public void PerformVirtualClick()
    {
    }
}

/// <summary>
/// Virtual form of the AdaptiveLineBreak's ordinary vertical-divider role.
/// </summary>
[ToolboxItem(true)]
public class VirtualAdaptiveLineBreak : Control, IAdaptiveVirtualControl
{
    private Color _lineColor = Color.FromArgb(200, 200, 200);
    private int _lineThickness = 1;
    private int _verticalPadding = 4;

    public VirtualAdaptiveLineBreak()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Margin = new Padding(4, 0, 4, 0);
        Size = new Size(10, 30);
        TabStop = false;
    }

    [Category("Appearance"), DefaultValue(typeof(Color), "200, 200, 200")]
    public Color LineColor
    {
        get => _lineColor;
        set { _lineColor = value; Invalidate(); }
    }

    [Category("Appearance"), DefaultValue(1)]
    public int LineThickness
    {
        get => _lineThickness;
        set { _lineThickness = Math.Max(1, value); Invalidate(); }
    }

    [Category("Appearance"), DefaultValue(4)]
    public int VerticalPadding
    {
        get => _verticalPadding;
        set { _verticalPadding = Math.Max(0, value); Invalidate(); }
    }

    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(4)]
    public int VirtualMinimumWidth { get; set; } = 4;

    [Browsable(false)]
    public bool AcceptsVirtualInput => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawLine(e.Graphics, ClientRectangle);
    }

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => DrawLine(graphics, bounds);

    public void PerformVirtualClick()
    {
    }

    private void DrawLine(Graphics graphics, Rectangle bounds)
    {
        int x = bounds.Left + bounds.Width / 2;
        int top = Math.Min(bounds.Bottom, bounds.Top + _verticalPadding);
        int bottom = Math.Max(top, bounds.Bottom - _verticalPadding);
        using var pen = new Pen(_lineColor, _lineThickness);
        graphics.DrawLine(pen, x, top, x, bottom);
    }
}

/// <summary>
/// The real RoundedCheckBox at design time, with parent-painted interaction at runtime.
/// </summary>
[ToolboxItem(true)]
public class VirtualRoundedCheckBox : RoundedCheckBox, IAdaptiveVirtualControl
{
    [Category("Virtualization"), DefaultValue(true)]
    public bool LayoutVisible { get; set; } = true;

    [Category("Virtualization"), DefaultValue(20)]
    public int VirtualMinimumWidth { get; set; } = 20;

    [Browsable(false)]
    public bool AcceptsVirtualInput => true;

    public void DrawVirtual(Graphics graphics, Rectangle bounds, VirtualControlState state)
        => RoundedCheckBoxVirtualRenderer.Draw(graphics, bounds, this, state);

    public void PerformVirtualClick() => OnClick(EventArgs.Empty);
}

/// <summary>
/// Hybrid row: all children share one layout pass; virtual-capable controls are
/// parent-painted at runtime while native editors keep real WinForms bounds.
/// </summary>
[ToolboxItem(true)]
[Designer(typeof(VirtualizingAdaptiveRowPanelDesigner))]
public class VirtualizingAdaptiveRowPanel : Panel
{
    private readonly Dictionary<Control, int> _authoredWidths = new();
    private readonly Dictionary<Control, int> _authoredHeights = new();
    private Control? _hotControl;
    private Control? _pressedControl;
    private bool _pressWasDouble;
    private bool _applyingLayout;
    private readonly Dictionary<Control, float> _authoredFontPoints = new();
    private bool _stepDownFontAtHighDpi;
    private string _springControlName = string.Empty;
    private readonly HashSet<string> _springNames = new(StringComparer.OrdinalIgnoreCase);
    private float _disabledOpacity = 0.75f;
    private float _disabledGrayAmount = 1f;
    private AdaptivePanelItemCollection? _items;
    private readonly HybridPanelDesignerItemCollection _designerItems;

    public VirtualizingAdaptiveRowPanel()
    {
        _designerItems = new HybridPanelDesignerItemCollection(this);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable | ControlStyles.StandardDoubleClick, true);
        DoubleBuffered = true;
        Padding = new Padding(4);
        TabStop = true;
    }

    [Category("Layout"), DefaultValue(StripSpringMode.Both)]
    public StripSpringMode SpringMode { get; set; } = StripSpringMode.Both;

    // StretchChildHeight deliberately NOT ported from v1 — David dropped it during the
    // dl_FileBrowser conversion (the filter rows were its only users). Children keep their
    // authored heights, centred; size a child to the row explicitly if it should fill.

    private bool _adjustHeightForDpi;
    private int _dpiHeightGrowthPercent = 25;
    private int _dpiBaseHeight;

    // Ported from v1: the PANEL's own height scales (partially) with DPI, so a row authored
    // 36px tall doesn't strangle its content at 150%. Runtime only, like v1.
    [Category("Layout"), DefaultValue(false)]
    [Description("When true, the panel height scales with DPI changes, including when dragged between screens.")]
    public bool AdjustHeightForDpi
    {
        get => _adjustHeightForDpi;
        set
        {
            if (_adjustHeightForDpi == value) return;
            _adjustHeightForDpi = value;
            if (IsHandleCreated) ApplyDpiAdjustedHeight();
            PerformLayout();
        }
    }

    [Category("Layout"), DefaultValue(25)]
    [Description("Percent of the DPI growth applied to row height. 25 means 150% DPI becomes roughly 112.5% height.")]
    public int DpiHeightGrowthPercent
    {
        get => _dpiHeightGrowthPercent;
        set
        {
            int v = Math.Clamp(value, 0, 100);
            if (_dpiHeightGrowthPercent == v) return;
            _dpiHeightGrowthPercent = v;
            ApplyDpiAdjustedHeight();
            PerformLayout();
        }
    }

    private void ApplyDpiAdjustedHeight()
    {
        if (!_adjustHeightForDpi || !IsHandleCreated || IsDisposed || IsDesignerHosted()) return;

        if (_dpiBaseHeight == 0 && Height > 0) _dpiBaseHeight = Height;
        if (_dpiBaseHeight == 0) return;

        float scale = Math.Max(1f, GetScaleFactor());
        float adjusted = 1f + (Math.Max(0f, scale - 1f) * (_dpiHeightGrowthPercent / 100f));
        int target = Math.Max(1, (int)Math.Round(_dpiBaseHeight * adjusted));
        if (Height != target) Height = target;
    }


    private AdaptiveRowContentAlignment _contentAlignment = AdaptiveRowContentAlignment.Left;

    // Reuses v1's enum — same three positions, same serialized names.
    [Category("Layout"), DefaultValue(AdaptiveRowContentAlignment.Left)]
    [Description("Aligns the visible children as a group within the row. Only visible when the row has spare width — a spring absorbs the spare first, leaving nothing to align with.")]
    public AdaptiveRowContentAlignment ContentAlignment
    {
        get => _contentAlignment;
        set { if (_contentAlignment != value) { _contentAlignment = value; PerformLayout(); Invalidate(); } }
    }

    [Category("Layout"), DefaultValue(StripGapCompressionMode.RoundRobin)]
    public StripGapCompressionMode GapCompressionMode { get; set; } = StripGapCompressionMode.RoundRobin;

    // ── Rounding. Same names and semantics as RoundedPanelFaster so the set reads as one:
    // BackColor is the FACE inside the radius, the parent's BackColor shows outside it, and
    // the border is stroked inset by half its width so no pale ring survives between the
    // fill's anti-aliased edge and the pen's. All default to off, so an existing row is
    // pixel-identical until someone asks for a corner. Added here rather than as a subclass
    // because a new type would need its own VStackItemType before it could live in a stack.
    // The border does NOT push the content in - give the row Padding for that, as with every
    // other rounded panel in the app.

    private int _cornerRadius;
    private int _borderThickness;
    private Color _borderColor = Color.FromArgb(200, 210, 220);

    [Category("Appearance"), DefaultValue(0)]
    [Description("Radius of the rounded corners. 0 is square.")]
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    [Category("Appearance"), DefaultValue(0)]
    [Description("Thickness of the border. 0 draws none.")]
    public int BorderThickness
    {
        get => _borderThickness;
        set { _borderThickness = Math.Max(0, value); Invalidate(); }
    }

    [Category("Appearance")]
    [Description("Colour of the border.")]
    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; Invalidate(); }
    }

    private bool ShouldSerializeBorderColor() => _borderColor != Color.FromArgb(200, 210, 220);
    private void ResetBorderColor() => BorderColor = Color.FromArgb(200, 210, 220);

    private Color _focusBorderColor = Color.Empty;

    /// <summary>
    /// Border colour while focus is anywhere inside the row - the text box in a URL bar, say -
    /// so the whole bar lights up, not just the box. Empty means the border never changes.
    /// </summary>
    [Category("Appearance"), DefaultValue(typeof(Color), "")]
    [Description("Border colour while a control inside the row has focus. Empty for no change.")]
    public Color FocusBorderColor
    {
        get => _focusBorderColor;
        set { _focusBorderColor = value; Invalidate(); }
    }

    private bool _isSelected;
    private Color _selectedBorderColor = Color.Empty;

    /// <summary>
    /// Border colour while the row is marked selected - the active cell in the page grid. Wins
    /// over FocusBorderColor. Empty means selection never changes the border.
    /// </summary>
    [Category("Appearance"), DefaultValue(typeof(Color), "")]
    [Description("Border colour while IsSelected is true. Empty for no change.")]
    public Color SelectedBorderColor
    {
        get => _selectedBorderColor;
        set { _selectedBorderColor = value; Invalidate(); }
    }

    [Category("Appearance"), DefaultValue(false)]
    [Description("Paints the border in SelectedBorderColor. Set by the owner, e.g. the active page cell.")]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; Invalidate(); } }
    }

    private Color _focusBackColor = Color.Empty;
    private Color _restBackColor;
    private bool _faceSwapped;

    /// <summary>
    /// Face colour while focus is anywhere inside the row. Applied by swapping BackColor for
    /// the duration, so EVERYTHING inside follows without wiring: virtual children are painted
    /// on the face, and hosted natives in flat mode borrow the parent colour. Restored on
    /// Leave. Empty means the face never changes.
    /// </summary>
    [Category("Appearance"), DefaultValue(typeof(Color), "")]
    [Description("Face colour while a control inside the row has focus. Empty for no change.")]
    public Color FocusBackColor
    {
        get => _focusBackColor;
        set { _focusBackColor = value; Invalidate(); }
    }

    // Enter/Leave fire on a container when focus moves into or out of any of its children,
    // which is exactly the moment the chrome needs repainting.
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        if (!_focusBackColor.IsEmpty && !_faceSwapped && !DesignMode)
        {
            _restBackColor = BackColor;
            _faceSwapped = true;
            BackColor = _focusBackColor;   // raises ParentBackColorChanged in hosted children
        }
        if (HasRounding && !_focusBorderColor.IsEmpty) Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        if (_faceSwapped)
        {
            _faceSwapped = false;
            BackColor = _restBackColor;
        }
        if (HasRounding && !_focusBorderColor.IsEmpty) Invalidate();
    }

    private bool HasRounding => _cornerRadius > 0 || _borderThickness > 0;

    [Category("Appearance"), DefaultValue(0.75f)]
    public float DisabledOpacity
    {
        get => _disabledOpacity;
        set { _disabledOpacity = Math.Clamp(value, 0f, 1f); Invalidate(); }
    }

    [Category("Appearance"), DefaultValue(1f)]
    public float DisabledGrayAmount
    {
        get => _disabledGrayAmount;
        set { _disabledGrayAmount = Math.Clamp(value, 0f, 1f); Invalidate(); }
    }

    [Category("Layout"), DefaultValue("")]
    [Description("Comma list of child control NAMES that spring: they absorb spare width and compress first when space is tight. Multiple names split the spare equally — same format as v1's Spring.")]
    public string SpringControlName
    {
        get => _springControlName;
        set
        {
            string newValue = value ?? string.Empty;
            if (string.Equals(_springControlName, newValue, StringComparison.Ordinal)) return;
            _springControlName = newValue;

            // Multi-spring, v1 syntax. The layout engine already handles any number of springs
            // (ExpandEvenly / ReduceWidths run over ALL flagged indexes); only this matching
            // was single-name.
            _springNames.Clear();
            foreach (string part in _springControlName.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0) _springNames.Add(name);
            }

            // ORDER-PROOFING (the spring-eater bug): on designer load, children are adopted
            // into DesignerItems BEFORE this property deserializes (Controls.Add serializes
            // above panel properties), so every item starts Spring=false — and the deferred
            // item->panel sync then recomputed the spring string from those items, stomping
            // the authored value with "" and persisting the loss on the next save. Pushing
            // the flags INTO the items here makes the later sync reproduce this value
            // instead of erasing it, whichever order the designer replays things in.
            _designerItems.SyncSpringFlagsFromPanel();

            PerformLayout();
        }
    }

    /// <summary>
    /// SITE name first, exactly as HybridPanelDesignerItems.GetControlName resolves it — the two
    /// must agree or a spring is matched by one and missed by the other.
    ///
    /// THE SPRING-EATER, SECOND HELPING. Matching on control.Name alone loses the spring on every
    /// designer load, because of the order the generated file assigns things:
    ///
    ///     bookmarksShared = new BookmarksBarControl();               // Name is ""
    ///     SplitBarLayoutPanel.Controls.Add(bookmarksShared);
    ///     SplitBarLayoutPanel.SpringControlName = "bookmarksShared"; // setter runs HERE
    ///     ...  91 lines later ...
    ///     bookmarksShared.Name = "bookmarksShared";                  // far too late
    ///
    /// The setter calls SyncSpringFlagsFromPanel, which asks this method while Name is still
    /// empty — so every item is flagged Spring=false, and the deferred item->panel sync then
    /// recomputes the spring string as "" and the designer drops the line as a default. The
    /// earlier guard doesn't catch it: that one only declines to write when the collection is
    /// EMPTY, and here it is fully populated, just uniformly wrong.
    ///
    /// The designer sites a component (with its name) when it creates it, well before it
    /// deserialises the Name property, so Site.Name is already right at that point.
    /// </summary>
    internal bool IsSpringChild(Control control)
    {
        string name = control.Site?.Name ?? control.Name ?? string.Empty;
        return !string.IsNullOrWhiteSpace(name) && _springNames.Contains(name);
    }

    [Category("Layout"), DefaultValue(false)]
    [Description("Steps child fonts down 2pt at 150%+ DPI even if the global StepDownHighDpiFonts setting is off. Default off; the global setting normally controls it.")]
    public bool StepDownFontAtHighDpi
    {
        get => _stepDownFontAtHighDpi;
        set { if (_stepDownFontAtHighDpi != value) { _stepDownFontAtHighDpi = value; PerformLayout(); } }
    }

    private ToolTip? _toolTipSource;
    private ToolTip? _virtualTipDisplay;

    /// <summary>
    /// The form's ToolTip component. Tooltips are AUTHORED exactly as v1 — the designer's
    /// ToolTip extender on each child — but a virtual child has no HWND, so the component's
    /// per-window mouse tracking never fires for it. Point this at the component and the panel
    /// replays the authored text for whichever virtual child is under the mouse.
    /// </summary>
    [Category("Behavior")]
    [Description("The form's ToolTip component. The panel replays its authored texts for virtual (handle-less) children. Hosted children don't need it — their own HWND tracking works.")]
    public ToolTip? ToolTipSource
    {
        get => _toolTipSource;
        set => _toolTipSource = value;
    }

    private void UpdateVirtualToolTip(Control? hot)
    {
        if (_toolTipSource == null) return;

        string text = hot == null ? string.Empty : _toolTipSource.GetToolTip(hot) ?? string.Empty;
        _virtualTipDisplay ??= new ToolTip();
        if (_virtualTipDisplay.GetToolTip(this) != text)
            _virtualTipDisplay.SetToolTip(this, text.Length == 0 ? null : text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _virtualTipDisplay?.Dispose();
            _virtualTipDisplay = null;
        }

        base.Dispose(disposing);
    }

    [Browsable(false)]
    public int VirtualizedControlCount { get; private set; }

    [Browsable(false)]
    public int VirtualizedHandleCount => Controls.Cast<Control>().Count(control =>
        control is IAdaptiveVirtualControl && control.IsHandleCreated);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AdaptivePanelItemCollection Items => _items ??= new AdaptivePanelItemCollection(this);

    // NEVER serialized. The real Controls collection is the single source of truth: Visual
    // Studio serializes the controls, and this collection is REBUILT from them on designer load
    // (Owner_ControlAdded adopts each child as it arrives). Serializing it too meant the same
    // row existed twice in Designer.cs, and a descriptor whose ItemType was omitted because it
    // matched the [DefaultValue] (the enum's first member, IconButton) morphed the real control
    // to an IconButton on the next designer round-trip.
    [Category("Design")]
    [DisplayName("Items")]
    [Description("Add, remove, morph and reorder virtual items and hosted controls.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HybridPanelDesignerItemCollection DesignerItems => _designerItems;

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is not Control control) return;
        if (!IsDesignerHosted() && control is IAdaptiveVirtualControl)
            control.Visible = false;
        CaptureAuthoredSize(control);
        control.SizeChanged += ChildSizeChanged;

        // A virtual child has no HWND, so its own Invalidate() paints nothing — the PANEL
        // must repaint its rectangle when state the renderer reads changes. Everything that
        // RAISES a change event is hooked (Text, Enabled, ForeColor, BackColor, Font); the
        // one category that can't be is IMAGE properties (plain setters, no events) — an
        // image swap from code still needs a panel Invalidate().
        control.TextChanged += ChildAppearanceChanged;
        control.EnabledChanged += ChildAppearanceChanged;
        control.ForeColorChanged += ChildAppearanceChanged;
        control.BackColorChanged += ChildAppearanceChanged;
        control.FontChanged += ChildAppearanceChanged;

        // Type-specific STATE the renderers read. Control's own change events cover appearance,
        // but a checkbox's tick and a combo's selection are drawn from state with no base-class
        // event - so setting Checked from code repainted nothing and the tick only appeared on
        // the next unrelated repaint (a hover, a resize). Interaction always looked instant
        // because the mouse path calls InvalidateVirtual directly.
        if (control is RoundedCheckBox virtualCheckBox)
            virtualCheckBox.CheckedChanged += ChildAppearanceChanged;

        if (control is RoundedComboBox virtualCombo)
            virtualCombo.SelectedIndexChanged += ChildAppearanceChanged;

        if (control is ModernButton virtualButton)
            virtualButton.ToggledChanged += ChildAppearanceChanged;

        // ORDER. OnLayout sorts by TabIndex, so a TabIndex change reorders the row — but nothing
        // was listening, and the row kept its old order until some UNRELATED event forced a pass
        // (a resize, a hover, editing another property). Retyping a child or resequencing indexes
        // therefore looked like it had done nothing.
        control.TabIndexChanged += ChildOrderChanged;
        control.Move += ChildMoved;
        PerformLayout();
    }

    // The panel owns positions, but on a drag-drop ParentControlDesigner writes the cursor's
    // drop point AFTER OnControlAdded's snap — the designer gets the last word and the control
    // sits wherever it was dropped until the next layout event. Take the last word back:
    // any design-time location write we didn't make ourselves triggers a re-snap.
    private void ChildMoved(object? sender, EventArgs e)
    {
        if (!_applyingLayout && IsDesignerHosted()) PerformLayout();
    }

    private void ChildAppearanceChanged(object? sender, EventArgs e)
    {
        if (sender is Control control && control is IAdaptiveVirtualControl)
            InvalidateVirtual(control);
    }

    /// <summary>
    /// A child's TabIndex changed, which is this panel's ordering truth — so the whole row moves,
    /// not just that one child. A full repaint rather than InvalidateVirtual on the sender: every
    /// item after it has shifted, and the virtual ones leave nothing behind to repaint
    /// themselves.
    ///
    /// Guarded against our own layout: the pass assigns Bounds, and a control whose TabIndex is
    /// touched mid-pass would re-enter here.
    /// </summary>
    private void ChildOrderChanged(object? sender, EventArgs e)
    {
        if (_applyingLayout)
            return;

        PerformLayout();
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        // Control.CreateControl creates handles for visible children after the parent's
        // handle. Hide virtual children first so normal InitializeComponent construction
        // never gives them an HWND.
        if (!IsDesignerHosted())
        {
            foreach (Control control in Controls)
            {
                if (control is IAdaptiveVirtualControl)
                    control.Visible = false;
            }
        }
        base.OnHandleCreated(e);
        ApplyDpiAdjustedHeight();   // ported v1 AdjustHeightForDpi — baseline captured here
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is not Control control) return;
        control.SizeChanged -= ChildSizeChanged;
        control.TextChanged -= ChildAppearanceChanged;
        control.EnabledChanged -= ChildAppearanceChanged;
        control.ForeColorChanged -= ChildAppearanceChanged;
        control.BackColorChanged -= ChildAppearanceChanged;
        control.FontChanged -= ChildAppearanceChanged;

        if (control is RoundedCheckBox virtualCheckBox)
            virtualCheckBox.CheckedChanged -= ChildAppearanceChanged;

        if (control is RoundedComboBox virtualCombo)
            virtualCombo.SelectedIndexChanged -= ChildAppearanceChanged;

        if (control is ModernButton virtualButton)
            virtualButton.ToggledChanged -= ChildAppearanceChanged;

        control.TabIndexChanged -= ChildOrderChanged;
        control.Move -= ChildMoved;
        _authoredWidths.Remove(control);
        _authoredHeights.Remove(control);
        _authoredFontPoints.Remove(control);
        base.OnControlRemoved(e);
    }

    private void ChildSizeChanged(object? sender, EventArgs e)
    {
        if (!_applyingLayout && sender is Control control)
        {
            CaptureAuthoredSize(control);
            PerformLayout();
        }
    }

    private void CaptureAuthoredSize(Control control)
    {
        if (control.Width > 0) _authoredWidths[control] = control.Width;
        if (control.Height > 0) _authoredHeights[control] = control.Height;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_applyingLayout) return;

        bool designTime = IsDesignerHosted();
        if (!designTime) ApplyDpiFontStep();
        // Design time lays out EVERY child — the v1 contract. Collapse (LayoutVisible=false,
        // or Visible=false on hosted natives, which the designer shadows to true anyway) is a
        // runtime-only effect; the design surface always shows the fully assembled row.
        Control[] controls = Controls.Cast<Control>()
            .Where(control => designTime || (control is IAdaptiveVirtualControl virtualControl
                ? virtualControl.LayoutVisible
                : control.Visible))
            .OrderBy(control => control.TabIndex)
            .ThenBy(control => Controls.GetChildIndex(control))
            .ToArray();
        if (controls.Length == 0) return;

        int[] widths = controls.Select(control => _authoredWidths.GetValueOrDefault(control, Math.Max(1, control.Width))).ToArray();
        int[] minimumWidths = controls.Select(control => control is IAdaptiveVirtualControl virtualControl
            ? Math.Max(1, virtualControl.VirtualMinimumWidth)
            : Math.Max(1, control.MinimumSize.Width)).ToArray();
        bool[] springs = controls.Select(IsSpringChild).ToArray();
        int[] gaps = new int[Math.Max(0, controls.Length - 1)];
        for (int index = 0; index < gaps.Length; index++)
            gaps[index] = Math.Max(0, controls[index].Margin.Right) + Math.Max(0, controls[index + 1].Margin.Left);

        int leading = Math.Max(0, controls[0].Margin.Left);
        int trailing = Math.Max(0, controls[^1].Margin.Right);
        int available = Math.Max(0, ClientSize.Width - Padding.Horizontal - leading - trailing);
        RowLayoutResult result = RowLayoutEngine.Fit(widths, minimumWidths, springs, gaps,
            available, SpringMode, GapCompressionMode);

        // Alignment offsets the whole group into the spare width. With a live spring the spare
        // is already absorbed (result.UsedWidth == available), so the offset is naturally 0.
        int spare = Math.Max(0, available - result.UsedWidth);
        int alignmentOffset = _contentAlignment switch
        {
            AdaptiveRowContentAlignment.Center => spare / 2,
            AdaptiveRowContentAlignment.Right => spare,
            _ => 0,
        };

        int x = Padding.Left + leading + alignmentOffset;
        int availableHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
        int virtualCount = 0;
        _applyingLayout = true;
        try
        {
            for (int index = 0; index < controls.Length; index++)
            {
                Control control = controls[index];
                int height = Math.Min(availableHeight, _authoredHeights.GetValueOrDefault(control, control.Height));
                int y = Padding.Top + Math.Max(0, (availableHeight - height) / 2);
                control.Bounds = new Rectangle(x, y, result.Widths[index], Math.Max(1, height));

                if (!designTime && control is IAdaptiveVirtualControl)
                {
                    control.Visible = false;
                    virtualCount++;
                }
                else if (designTime && control is IAdaptiveVirtualControl)
                {
                    control.Visible = true;
                }

                x += result.Widths[index];
                if (index < result.Gaps.Length) x += result.Gaps[index];
            }
        }
        finally
        {
            _applyingLayout = false;
        }

        VirtualizedControlCount = virtualCount;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Body first, so the virtual children paint onto the face and not the backdrop.
        if (HasRounding) PaintRoundedBody(e.Graphics);

        if (IsDesignerHosted())
        {
            // The design surface hosts real HWNDs, which sit above anything drawn here - the
            // border still goes on so the row's shape can be judged in the designer.
            if (HasRounding) PaintRoundedBorder(e.Graphics);
            return;
        }

        foreach (Control control in Controls)
        {
            if (control is not IAdaptiveVirtualControl virtualControl || !virtualControl.LayoutVisible)
                continue;
            if (!e.ClipRectangle.IntersectsWith(control.Bounds))
                continue;
            VirtualControlState state = VirtualControlState.None;
            if (ReferenceEquals(control, _hotControl)) state |= VirtualControlState.Hot;
            if (ReferenceEquals(control, _pressedControl)) state |= VirtualControlState.Pressed;

            // WYSIWYG: a real child control is clipped to its own client rect by Windows, so
            // the DESIGN surface (where these are live HWNDs) can never paint outside a button.
            // Painted virtually there is no such boundary, and an ImageSize larger than the
            // control's Size silently overflowed at runtime only — the design view showed the
            // icon cropped and the running app showed it whole. Fine 2-3px adjustments were
            // impossible to judge from the designer. Clipping here gives the virtual pass the
            // same boundary the native one has, so the two views agree.
            GraphicsState clip = e.Graphics.Save();
            e.Graphics.IntersectClip(control.Bounds);
            try
            {
                virtualControl.DrawVirtual(e.Graphics, control.Bounds, state);
            }
            finally
            {
                e.Graphics.Restore(clip);
            }
        }

        // Border LAST, over the children: a virtual child at the row's edge would otherwise
        // paint across the stroke, and the row would look like it had a bite out of it.
        if (HasRounding) PaintRoundedBorder(e.Graphics);
    }

    // ── Rounding paint. Mirrors RoundedPanelFaster stroke for stroke.

    private RectangleF RoundedRect => new(0, 0, Width - 1, Height - 1);

    private void PaintRoundedBody(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Outside the radius shows the PARENT: fake transparency without a cascade of
        // transparent backgrounds, exactly as the rounded panels do.
        using (var backdrop = new SolidBrush(Parent?.BackColor ?? BackColor))
            g.FillRectangle(backdrop, 0, 0, Width, Height);

        using GraphicsPath body = RoundedPath(RoundedRect, _cornerRadius);
        using var face = new SolidBrush(BackColor);
        g.FillPath(face, body);
    }

    private void PaintRoundedBorder(Graphics g)
    {
        if (_borderThickness <= 0) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Inset by HALF the pen: a pen straddles its path, so stroking the fill's own edge
        // leaves a pale ring where two anti-aliased fringes meet over the backdrop.
        float inset = _borderThickness / 2f;
        RectangleF strokeBounds = RectangleF.Inflate(RoundedRect, -inset, -inset);
        float strokeRadius = Math.Max(0f, _cornerRadius - inset);

        bool selected = _isSelected && !_selectedBorderColor.IsEmpty;
        bool focused = !_focusBorderColor.IsEmpty && ContainsFocus;
        Color edgeColor = selected ? _selectedBorderColor : focused ? _focusBorderColor : _borderColor;

        using GraphicsPath edge = RoundedPath(strokeBounds, strokeRadius);
        using var pen = new Pen(edgeColor, _borderThickness);
        g.DrawPath(pen, edge);
    }

    private static GraphicsPath RoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        float d = Math.Min(radius * 2f, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// True when the point sits on a live virtual child — RoundedForm.EnableDrag yields there,
    /// or every click on a painted button would become a 0px window drag (David's dead
    /// btnCloseB after the header row converted).
    /// </summary>
    public bool IsPointOnVirtualChild(Point point) => HitTestVirtual(point) != null;

    private Control? HitTestVirtual(Point point)
        => Controls.Cast<Control>().FirstOrDefault(control => control is IAdaptiveVirtualControl virtualControl
            && virtualControl.LayoutVisible && virtualControl.AcceptsVirtualInput
            && control.Enabled && control.Bounds.Contains(point));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Control? hit = HitTestVirtual(e.Location);
        if (ReferenceEquals(hit, _hotControl)) return;
        InvalidateVirtual(_hotControl);
        _hotControl = hit;
        InvalidateVirtual(_hotControl);
        Cursor = hit is null ? Cursors.Default : Cursors.Hand;
        UpdateVirtualToolTip(hit);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateVirtual(_hotControl);
        _hotControl = null;
        Cursor = Cursors.Default;
        UpdateVirtualToolTip(null);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        _pressedControl = HitTestVirtual(e.Location);
        _pressWasDouble = e.Clicks > 1;
        InvalidateVirtual(_pressedControl);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        Control? pressed = _pressedControl;
        _pressedControl = null;
        InvalidateVirtual(pressed);
        if (pressed is IAdaptiveVirtualControl virtualControl
            && ReferenceEquals(pressed, HitTestVirtual(e.Location)))
        {
            // Mirrors native CS_DBLCLKS: the second press of a double click replaces the
            // single click, it does not add one.
            if (_pressWasDouble)
                virtualControl.PerformVirtualDoubleClick(new Point(
                    e.X - pressed.Left, e.Y - pressed.Top));
            else if (pressed is IAdaptiveVirtualPointerControl pointerControl)
                pointerControl.PerformVirtualClick(new Point(
                    e.X - pressed.Left, e.Y - pressed.Top));
            else
                virtualControl.PerformVirtualClick();
        }
        _pressWasDouble = false;
    }

    private void InvalidateVirtual(Control? control)
    {
        if (control is not null) Invalidate(Rectangle.Inflate(control.Bounds, 2, 2));
    }

    // ── DPI font step — direct port of v1 ApplyDpiFontStep ──────────────────────────────
    //
    // The app's forms are AutoScaleMode.None: control boxes keep their authored pixel sizes
    // while point fonts render ~1.5x at 150% DPI, so text crowds and clips. Step every child
    // font down 2pt at >=150%. Authored size is captured once per child and the target is
    // recomputed from it, so this is idempotent and self-restores at 100%. Virtual children
    // never have handles, so mutating their Font is cheap and the renderers pick it up on
    // the next paint; hosted natives step exactly as they do under v1.
    private void ApplyDpiFontStep()
    {
        bool stepEnabled = ControlSettings.StepDownHighDpiFonts
            || _stepDownFontAtHighDpi;
        int reduction = (GetScaleFactor() >= 1.5f && stepEnabled) ? 2 : 0;

        bool previous = _applyingLayout;
        _applyingLayout = true;
        try
        {
            foreach (Control child in Controls)
            {
                if (!_authoredFontPoints.TryGetValue(child, out float basePoints))
                {
                    basePoints = child.Font.SizeInPoints;
                    _authoredFontPoints[child] = basePoints;
                }

                float targetPoints = Math.Max(1f, basePoints - reduction);
                if (Math.Abs(child.Font.SizeInPoints - targetPoints) <= 0.05f) continue;
                child.Font = new Font(child.Font.FontFamily, targetPoints, child.Font.Style, GraphicsUnit.Point);
            }
        }
        finally
        {
            _applyingLayout = previous;
        }
    }

    private float GetScaleFactor()
    {
        int dpi = DeviceDpi;
        return dpi > 0 ? dpi / 96f : 1f;
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyDpiAdjustedHeight();
        PerformLayout();
        Invalidate();
    }

    private bool IsDesignerHosted()
    {
        for (Control? control = this; control != null; control = control.Parent)
            if (control.Site?.DesignMode == true) return true;
        return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
    }
}

internal static class RoundedCheckBoxVirtualRenderer
{
    public static void Draw(Graphics graphics, Rectangle bounds,
        VirtualRoundedCheckBox checkBox, VirtualControlState state)
    {
        bool hot = checkBox.Enabled && (state & VirtualControlState.Hot) != 0;
        bool pressed = checkBox.Enabled && (state & VirtualControlState.Pressed) != 0;
        int diameter = Math.Min(checkBox.Radius,
            Math.Max(1, bounds.Height - checkBox.Padding.Vertical));
        int top = bounds.Top + checkBox.Padding.Top
            + Math.Max(0, (bounds.Height - checkBox.Padding.Vertical - diameter) / 2);
        int left = checkBox.CheckBoxAlignment == HorizontalAlignment.Right
            ? bounds.Right - checkBox.Padding.Right - diameter - 1
            : bounds.Left + checkBox.Padding.Left + 1;
        Rectangle circle = new(left, top, diameter, diameter);

        // Hover semantics match the real control (David): the fill NEVER changes on hover —
        // feedback lives in the border.
        Color fill = checkBox.Checked ? checkBox.CheckedColor : checkBox.CheckBoxBackColor;
        Color border = hot ? checkBox.HoverBorderColor : checkBox.BorderColor;
        if (pressed)
        {
            fill = Blend(fill, Color.Black, 0.14f);
            border = Blend(border, Color.Black, 0.18f);
        }

        Color surface = checkBox.Parent?.BackColor ?? SystemColors.Control;

        // The CONTROL's own DisabledFadePercent, exactly as the real RoundedCheckBox applies it
        // (a straight fade toward the parent background, no desaturation). This used to read
        // the row panel's DisabledOpacity instead, so the property sitting right there on the
        // checkbox in the designer did nothing at all in a virtual row.
        float opacity = 1f - Math.Clamp(checkBox.DisabledFadePercent, 0, 100) / 100f;
        const float gray = 0f;

        if (!checkBox.Enabled)
        {
            fill = Disabled(fill, surface, opacity, gray);
            border = Disabled(border, surface, opacity, gray);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(fill))
            graphics.FillEllipse(brush, circle);
        using (var pen = new Pen(border, 2f))
            graphics.DrawEllipse(pen, circle);

        Color tick = hot ? checkBox.TickHoverColor : checkBox.TickColor;
        if (checkBox.Checked)
            DrawCheck(graphics, circle, checkBox.Enabled
                ? tick
                : Disabled(tick, surface, opacity, gray));

        if (!string.IsNullOrWhiteSpace(checkBox.Text))
        {
            Rectangle textBounds;
            if (checkBox.CheckBoxAlignment == HorizontalAlignment.Right)
            {
                textBounds = new Rectangle(bounds.Left + checkBox.Padding.Left, bounds.Top,
                    Math.Max(0, circle.Left - bounds.Left - checkBox.Padding.Left - checkBox.TextGap),
                    bounds.Height);
            }
            else
            {
                int textLeft = circle.Right + checkBox.TextGap;
                textBounds = new Rectangle(textLeft, bounds.Top,
                    Math.Max(0, bounds.Right - checkBox.Padding.Right - textLeft), bounds.Height);
            }

            Color textColor = checkBox.Enabled
                ? checkBox.ForeColor
                : Disabled(checkBox.ForeColor, surface, opacity, gray);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            if (checkBox.CheckBoxAlignment == HorizontalAlignment.Right)
                flags |= TextFormatFlags.Right;
            if (checkBox.RightToLeft == RightToLeft.Yes)
                flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(graphics, checkBox.Text, checkBox.Font, textBounds, textColor, flags);
        }

        if ((state & VirtualControlState.Focused) != 0)
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -2, -2));
    }

    private static void DrawCheck(Graphics graphics, Rectangle circle, Color color)
    {
        float scale = circle.Width / 18f;
        int centerX = circle.Left + circle.Width / 2;
        int centerY = circle.Top + circle.Height / 2;
        Point[] points =
        {
            new(centerX - (int)(4 * scale), centerY),
            new(centerX - (int)(1 * scale), centerY + (int)(3 * scale)),
            new(centerX + (int)(5 * scale), centerY - (int)(3 * scale)),
        };
        using var pen = new Pen(color, Math.Max(2f, scale * 2f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        graphics.DrawLines(pen, points);
    }

    private static Color Disabled(Color source, Color surface, float opacity, float grayAmount)
    {
        int luminance = (int)Math.Round(source.R * 0.299 + source.G * 0.587 + source.B * 0.114);
        Color gray = Color.FromArgb(source.A, luminance, luminance, luminance);
        return Blend(Blend(source, gray, grayAmount), surface, 1f - opacity);
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(from.A + (to.A - from.A) * amount),
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }
}

internal static class IconButtonVirtualRenderer
{
    public static void Draw(Graphics graphics, Rectangle bounds, VirtualIconButton button, VirtualControlState state)
    {
        bool hot = (state & VirtualControlState.Hot) != 0 && button.Enabled;
        bool pressed = (state & VirtualControlState.Pressed) != 0 && button.Enabled;
        bool toggled = button.ImageBUse == IconButtonImageBUse.Toggle && button.Toggled;
        Image? normal = toggled ? button.ButtonImageB ?? button.ButtonImageA : button.ButtonImageA;
        // Same precedence as IconButton.CurrentImage: an authored disabled face wins outright.
        bool ownDisabledFace = !button.Enabled && button.ButtonImageDisabled != null;
        Image? image = ownDisabledFace
            ? button.ButtonImageDisabled
            : pressed
                ? (toggled ? button.ButtonImageBPressed ?? button.ButtonImageBHover ?? normal : button.ButtonImageAPressed ?? button.ButtonImageAHover ?? normal)
                : hot
                    ? (toggled ? button.ButtonImageBHover ?? normal : button.ButtonImageAHover ?? normal)
                    : normal;
        string text = toggled && !string.IsNullOrEmpty(button.ToggledText) ? button.ToggledText : button.Text;
        bool hasText = button.TextPosition != IconButtonTextPosition.None && !string.IsNullOrEmpty(text);
        Font font = hot && button.FontHovered != null ? button.FontHovered : button.Font;
        Size imageSize = button.ImageSize;
        Size textSize = hasText ? TextRenderer.MeasureText(graphics, text, font) : Size.Empty;
        const int gap = 4;
        Rectangle imageRect;
        Rectangle textRect;

        if (!hasText)
        {
            imageRect = Center(bounds, imageSize);
            textRect = Rectangle.Empty;
        }
        else if (button.TextPosition is IconButtonTextPosition.Up or IconButtonTextPosition.Down)
        {
            int totalHeight = imageSize.Height + gap + textSize.Height;
            int top = bounds.Top + (bounds.Height - totalHeight) / 2;
            bool textFirst = button.TextPosition == IconButtonTextPosition.Up;
            textRect = new Rectangle(bounds.Left + (bounds.Width - textSize.Width) / 2,
                textFirst ? top : top + imageSize.Height + gap, textSize.Width, textSize.Height);
            imageRect = new Rectangle(bounds.Left + (bounds.Width - imageSize.Width) / 2,
                textFirst ? textRect.Bottom + gap : top, imageSize.Width, imageSize.Height);
        }
        else if (button.TextPosition == IconButtonTextPosition.Center)
        {
            imageRect = Center(bounds, imageSize);
            textRect = Center(bounds, textSize);
        }
        else
        {
            bool textFirst = button.TextPosition == IconButtonTextPosition.Left;
            int totalWidth = imageSize.Width + gap + textSize.Width;
            int left = bounds.Left + (bounds.Width - totalWidth) / 2;
            textRect = new Rectangle(textFirst ? left : left + imageSize.Width + gap,
                bounds.Top + (bounds.Height - textSize.Height) / 2, textSize.Width, textSize.Height);
            imageRect = new Rectangle(textFirst ? textRect.Right + gap : left,
                bounds.Top + (bounds.Height - imageSize.Height) / 2, imageSize.Width, imageSize.Height);
        }

        imageRect.Offset(button.ImageOffsetX, button.ImageOffsetY);
        int delta = pressed ? button.PressedSize : hot ? button.HoveredSize : 0;
        imageRect.Inflate(delta / 2, delta / 2);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (image != null)
            DrawImage(graphics, image, imageRect, button.Enabled || ownDisabledFace, pressed ? button.PressedBrightness : hot ? button.HoveredBrightness : 0);
        if (hasText)
        {
            textRect.Offset(button.TextOffsetX, button.TextOffsetY);
            TextRenderer.DrawText(graphics, text, font, textRect,
                button.Enabled ? button.TextColor : Color.FromArgb(160, 160, 160),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
        if (image == null && !hasText)
            TextRenderer.DrawText(graphics, "Icon\\nButton", SystemFonts.SmallCaptionFont, bounds, Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if ((state & VirtualControlState.Focused) != 0)
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -2, -2));
    }

    private static Rectangle Center(Rectangle bounds, Size size)
        => new(bounds.Left + (bounds.Width - size.Width) / 2, bounds.Top + (bounds.Height - size.Height) / 2, size.Width, size.Height);

    private static void DrawImage(Graphics graphics, Image image, Rectangle destination, bool enabled, int brightness)
    {
        // Disabled goes through the REAL IconButton's grayscale (luminance desaturation +
        // half alpha), so a virtualized icon disables pixel-identically to a native one.
        // The old inline matrix here DARKENED the channels to 30% instead of desaturating —
        // disabled icons came out as muddy dark blobs.
        if (!enabled)
        {
            IconButton.DrawDisabledImage(graphics, image, destination);
            return;
        }

        if (brightness == 0)
        {
            graphics.DrawImage(image, destination);
            return;
        }

        float shift = brightness / 100f;
        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix
        {
            Matrix40 = shift,
            Matrix41 = shift,
            Matrix42 = shift,
        };
        attributes.SetColorMatrix(matrix);
        graphics.DrawImage(image, destination, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
    }
}
