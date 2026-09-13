using System.ComponentModel;
using System.Diagnostics;

namespace ProfessorSnowsVideoDownloader.CustomControls;

public enum StripSpringMode { Expands, Contracts, Both }
public enum StripGapCompressionMode { RoundRobin, RightToLeft }
public enum StripContentAlignment { Left, Center, Right }

/// <summary>A DPI-aware, single-window, owner-drawn adaptive row.</summary>
[ToolboxItem(true)]
[Designer(typeof(AdaptiveRowStripDesigner))]
[DefaultEvent(nameof(ItemClicked))]
public class AdaptiveRowStrip : Control
{
    private readonly StripItemCollection _items;
    private readonly StripStyle _style;
    private StripItem[] _visibleItems = Array.Empty<StripItem>();
    private StripItem? _hotItem;
    private StripItem? _pressedItem;
    private StripItem? _focusedItem;
    private bool _layoutValid;
    private float _scale = 1f;
    private int _itemGap = 6;
    private int _edgePadding = 4;
    private int _verticalPadding = 2;
    private bool _useItemMargins = true;
    private StripSpringMode _springMode = StripSpringMode.Both;
    private StripGapCompressionMode _gapCompressionMode = StripGapCompressionMode.RoundRobin;
    private StripContentAlignment _contentAlignment;
    private string _lastLayoutReport = string.Empty;

    public AdaptiveRowStrip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        _items = new StripItemCollection(this);
        _style = new StripStyle(this);
        Height = 56;
        TabStop = true;
    }

    [Category("Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public StripItemCollection Items => _items;

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public StripStyle Style => _style;

    [Category("Layout"), DefaultValue(6)]
    public int ItemGap { get => _itemGap; set => SetLayoutValue(ref _itemGap, Math.Max(0, value)); }

    [Category("Layout"), DefaultValue(4)]
    public int EdgePadding { get => _edgePadding; set => SetLayoutValue(ref _edgePadding, Math.Max(0, value)); }

    [Category("Layout"), DefaultValue(2)]
    public int VerticalPadding { get => _verticalPadding; set => SetLayoutValue(ref _verticalPadding, Math.Max(0, value)); }

    [Category("Layout"), DefaultValue(true)]
    public bool UseItemMargins { get => _useItemMargins; set => SetLayoutValue(ref _useItemMargins, value); }

    [Category("Layout"), DefaultValue(StripSpringMode.Both)]
    public StripSpringMode SpringMode { get => _springMode; set => SetLayoutValue(ref _springMode, value); }

    [Category("Layout"), DefaultValue(StripGapCompressionMode.RoundRobin)]
    public StripGapCompressionMode GapCompressionMode { get => _gapCompressionMode; set => SetLayoutValue(ref _gapCompressionMode, value); }

    [Category("Layout"), DefaultValue(StripContentAlignment.Left)]
    public StripContentAlignment ContentAlignment { get => _contentAlignment; set => SetLayoutValue(ref _contentAlignment, value); }

    [Category("Diagnostics"), DefaultValue(false)]
    public bool DebugLayout { get; set; }

    [Browsable(false)] public string LastLayoutReport => _lastLayoutReport;
    [Browsable(false)] public bool LayoutOverflows { get; private set; }

    [Category("Action")]
    public event EventHandler<StripItemClickedEventArgs>? ItemClicked;
    public event EventHandler? LayoutReportChanged;

    private void SetLayoutValue<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        InvalidateStripLayout();
    }

    public void InvalidateStripLayout()
    {
        _layoutValid = false;
        Invalidate();
    }

    internal void InvalidateItem(StripItem item)
    {
        if (item.Bounds.Width > 0 && item.Bounds.Height > 0)
            Invalidate(Rectangle.Inflate(item.Bounds, 1, 1));
    }

    internal int LogicalToDeviceUnitsCached(int value)
        => (int)Math.Round(value * _scale, MidpointRounding.AwayFromZero);

    private void EnsureLayout()
    {
        if (_layoutValid) return;
        _layoutValid = true;
        _visibleItems = _items.Where(item => item.Visible).ToArray();
        NormalizeInteractionState();
        if (_visibleItems.Length == 0)
        {
            LayoutOverflows = false;
            UpdateLayoutReport("empty");
            return;
        }

        int edge = LogicalToDeviceUnitsCached(_edgePadding);
        int vertical = LogicalToDeviceUnitsCached(_verticalPadding);
        int available = Math.Max(0, ClientSize.Width - edge * 2);
        int leading = _useItemMargins ? LogicalToDeviceUnitsCached(Math.Max(0, _visibleItems[0].Margin.Left)) : 0;
        int trailing = _useItemMargins ? LogicalToDeviceUnitsCached(Math.Max(0, _visibleItems[^1].Margin.Right)) : 0;
        int[] widths = _visibleItems.Select(item => Math.Max(LogicalToDeviceUnitsCached(item.Width), LogicalToDeviceUnitsCached(item.MinWidth))).ToArray();
        int[] minimumWidths = _visibleItems.Select(item => Math.Max(1, LogicalToDeviceUnitsCached(item.MinWidth))).ToArray();
        bool[] springs = _visibleItems.Select(item => item.IsSpring).ToArray();
        int[] gaps = BuildGaps();
        int fitWidth = Math.Max(0, available - leading - trailing);
        RowLayoutResult result = RowLayoutEngine.Fit(widths, minimumWidths, springs, gaps, fitWidth, _springMode, _gapCompressionMode);
        int spare = Math.Max(0, fitWidth - result.UsedWidth);
        int offset = _contentAlignment switch
        {
            StripContentAlignment.Center => spare / 2,
            StripContentAlignment.Right => spare,
            _ => 0,
        };

        int x = edge + leading + offset;
        int itemHeight = Math.Max(1, ClientSize.Height - vertical * 2);
        for (int index = 0; index < _visibleItems.Length; index++)
        {
            _visibleItems[index].Bounds = new Rectangle(x, vertical, result.Widths[index], itemHeight);
            x += result.Widths[index];
            if (index < result.Gaps.Length) x += result.Gaps[index];
        }

        LayoutOverflows = result.Overflow > 0;
        string items = string.Join(" | ", _visibleItems.Select((item, index) => $"{item.GetType().Name}:{widths[index]}->{result.Widths[index]}"));
        UpdateLayoutReport($"dpi={DeviceDpi} available={fitWidth} used={result.UsedWidth} overflow={result.Overflow} | {items}");
    }

    private int[] BuildGaps()
    {
        if (_visibleItems.Length < 2) return Array.Empty<int>();
        var gaps = new int[_visibleItems.Length - 1];
        for (int index = 0; index < gaps.Length; index++)
        {
            int logicalGap = _useItemMargins
                ? Math.Max(0, _visibleItems[index].Margin.Right) + Math.Max(0, _visibleItems[index + 1].Margin.Left)
                : _itemGap;
            gaps[index] = LogicalToDeviceUnitsCached(logicalGap);
        }
        return gaps;
    }

    private void UpdateLayoutReport(string report)
    {
        if (string.Equals(report, _lastLayoutReport, StringComparison.Ordinal)) return;
        _lastLayoutReport = report;
        if (DebugLayout) Debug.WriteLine($"[AdaptiveRowStrip:{Name}] {report}");
        LayoutReportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NormalizeInteractionState()
    {
        if (_hotItem is not null && !_visibleItems.Contains(_hotItem)) _hotItem = null;
        if (_pressedItem is not null && !_visibleItems.Contains(_pressedItem)) _pressedItem = null;
        if (_focusedItem is not null && (!_visibleItems.Contains(_focusedItem) || !_focusedItem.Interactive || !_focusedItem.Enabled)) _focusedItem = null;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); InvalidateStripLayout(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); InvalidateStripLayout(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RefreshDpi(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RefreshDpi(); }

    private void RefreshDpi()
    {
        _scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
        foreach (StripIconButton item in _items.OfType<StripIconButton>()) item.RefreshScaledIcon();
        InvalidateStripLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        EnsureLayout();
        foreach (StripItem item in _visibleItems)
        {
            if (!e.ClipRectangle.IntersectsWith(item.Bounds)) continue;
            StripItemState state = StripItemState.None;
            if (ReferenceEquals(item, _hotItem)) state |= StripItemState.Hot;
            if (ReferenceEquals(item, _pressedItem)) state |= StripItemState.Pressed;
            if (ReferenceEquals(item, _focusedItem) && Focused) state |= StripItemState.Focused;
            item.Draw(e.Graphics, state, _style);
        }
    }

    private StripItem? HitTest(Point point)
    {
        EnsureLayout();
        return _visibleItems.FirstOrDefault(item => item.Interactive && item.Bounds.Contains(point));
    }

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); SetHot(HitTest(e.Location)); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHot(null); }

    private void SetHot(StripItem? item)
    {
        if (ReferenceEquals(item, _hotItem)) return;
        InvalidateItemIfPresent(_hotItem);
        _hotItem = item;
        InvalidateItemIfPresent(_hotItem);
        Cursor = item is { Enabled: true } ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        _pressedItem = HitTest(e.Location);
        if (_pressedItem is { Enabled: true }) SetFocusedItem(_pressedItem);
        InvalidateItemIfPresent(_pressedItem);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        StripItem? pressed = _pressedItem;
        _pressedItem = null;
        InvalidateItemIfPresent(pressed);
        if (pressed is not null && ReferenceEquals(pressed, HitTest(e.Location))) PerformItemClick(pressed);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        StripItem? hit = HitTest(e.Location);
        if (hit is not null) PerformItemClick(hit);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Left: MoveFocus(-1); return true;
            case Keys.Right: MoveFocus(1); return true;
            case Keys.Home: MoveFocusToEdge(true); return true;
            case Keys.End: MoveFocusToEdge(false); return true;
            case Keys.F4:
            case Keys.Alt | Keys.Down:
                if (_focusedItem is StripDropDown) { PerformItemClick(_focusedItem); return true; }
                break;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter && _focusedItem is not null)
        {
            PerformItemClick(_focusedItem);
            e.Handled = true;
        }
    }

    private void MoveFocus(int direction)
    {
        EnsureLayout();
        if (_visibleItems.Length == 0) return;
        int current = _focusedItem is null ? -1 : Array.IndexOf(_visibleItems, _focusedItem);
        int start = current < 0 ? (direction > 0 ? -1 : _visibleItems.Length) : current;
        for (int index = start + direction; index >= 0 && index < _visibleItems.Length; index += direction)
        {
            if (_visibleItems[index] is { Interactive: true, Enabled: true }) { SetFocusedItem(_visibleItems[index]); return; }
        }
    }

    private void MoveFocusToEdge(bool fromStart)
    {
        EnsureLayout();
        IEnumerable<StripItem> candidates = fromStart ? _visibleItems : _visibleItems.Reverse();
        SetFocusedItem(candidates.FirstOrDefault(item => item.Interactive && item.Enabled));
    }

    private void SetFocusedItem(StripItem? item)
    {
        if (ReferenceEquals(item, _focusedItem)) return;
        InvalidateItemIfPresent(_focusedItem);
        _focusedItem = item;
        InvalidateItemIfPresent(_focusedItem);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (_focusedItem is null) MoveFocus(1); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); _pressedItem = null; Invalidate(); }

    private void PerformItemClick(StripItem item)
    {
        if (!item.Enabled || !item.Visible || !item.Interactive) return;
        item.PerformClick();
        ItemClicked?.Invoke(this, new StripItemClickedEventArgs(item));
    }

    private void InvalidateItemIfPresent(StripItem? item) { if (item is not null) InvalidateItem(item); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (StripItem item in _items.ToArray()) item.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class StripItemClickedEventArgs : EventArgs
{
    public StripItemClickedEventArgs(StripItem item) => Item = item;
    public StripItem Item { get; }
}
