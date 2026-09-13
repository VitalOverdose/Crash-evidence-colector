using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum AdaptiveRowMinimumWidthMode
    {
        Pixels,
        PercentOfBaseWidth
    }

    public enum AdaptiveRowGapCompressionMode
    {
        RoundRobin,
        RightToLeft
    }

    public enum AdaptiveRowSpringMode
    {
        Expands,
        Contracts,
        Both
    }

    public enum AdaptiveRowContentAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Very small horizontal layout panel for DPI/layout experiments.
    /// It keeps child controls in a single row so one place owns size and position.
    /// </summary>
    [Designer(typeof(AdaptiveRowPanelDesigner))]
    public class AdaptiveRowPanel : Panel
    {
        private int _itemGap = 6;
        private int _defaultChildHorizontalMargin = 4;
        private int _minimumChildWidthValue = 24;
        private AdaptiveRowMinimumWidthMode _minimumChildWidthMode = AdaptiveRowMinimumWidthMode.Pixels;
        private int _springControlTabIndex = -1;
        private string? _spring;
        private readonly HashSet<string> _springControlNames = new(StringComparer.OrdinalIgnoreCase);
        private AdaptiveRowSpringMode _springExpands = AdaptiveRowSpringMode.Both;
        private AdaptiveRowGapCompressionMode _gapCompressionMode = AdaptiveRowGapCompressionMode.RoundRobin;
        private bool _usePaddingFromItems = true;
        private bool _useTabIndexForOrder = true;
        private bool _stretchChildHeight;
        private AdaptiveRowContentAlignment _contentAlignment = AdaptiveRowContentAlignment.Left;
        private bool _debugLayout;
        private readonly Dictionary<Control, int> _baseWidths = new();
        private readonly Dictionary<Control, int> _baseLefts = new();
        private readonly Dictionary<Control, float> _baseFontSizes = new();
        private bool _isApplyingLayout;
        private string _lastLayoutReport = string.Empty;
        private readonly AdaptiveRowPanelContents _contentsEditorProxy;
        private int _baseHeight = 0;
        private readonly AdaptiveRowItemRuleCollection _itemRules = new();
        private bool _isSyncingItemRules;
        private bool _itemRulesSyncPending;
        private readonly HashSet<string> _pendingItemRuleChildRemovals = new(StringComparer.Ordinal);
        private readonly HashSet<AdaptiveRowItemRule> _pendingItemRuleChildCreates = new();
        private readonly Dictionary<string, ModernButtonAppearanceSnapshot> _modernButtonAppearanceSnapshots = new(StringComparer.Ordinal);
        private int _suppressChildRulePrune;
        private bool _postUndoRefreshQueued;
        private const string ItemRuleTagPrefix = "arp:";

        public event EventHandler? LayoutReportChanged;

        public AdaptiveRowPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = false;
            _contentsEditorProxy = new AdaptiveRowPanelContents(this);
            _itemRules.Owner = this;
        }

        // The designer only serialises Padding when it differs from DefaultPadding. Without this
        // override it compared against Control's (0,0,0,0), so a user-set ZERO padding was never
        // written to Designer.cs and the constructor default silently returned on every reload.
        protected override Padding DefaultPadding => new Padding(1, 1, 1, 1);

        // RETIRED: spacing lives entirely in the child margins now (see GetInterItemSpacing).
        // This no longer affects the default layout — kept only so the legacy uniform mode
        // (UsePaddingFromItems = false, unused) and old serialized `.ItemGap = n` lines still work.
        // Use the "Set Gap" / "Adjust Child Margins" smart-tag actions instead.
        [DefaultValue(6)]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int ItemGap
        {
            get => _itemGap;
            set
            {
                int newValue = Math.Max(0, value);
                if (_itemGap == newValue)
                {
                    return;
                }

                _itemGap = newValue;
                PerformLayout();
            }
        }

        [DefaultValue(4)]
        [Category("Layout")]
        [Description("Default left/right margin applied once to newly dropped child controls that still have the untouched designer default margin.")]
        public int DefaultChildHorizontalMargin
        {
            get => _defaultChildHorizontalMargin;
            set => _defaultChildHorizontalMargin = Math.Max(0, value);
        }

        [DefaultValue(24)]
        [Category("Layout")]
        [Description("Minimum width each child can be compressed to. Interpreted as pixels or percent based on MinimumChildWidthMode.")]
        public int MinimumChildWidthValue
        {
            get => _minimumChildWidthValue;
            set
            {
                int newValue = _minimumChildWidthMode == AdaptiveRowMinimumWidthMode.PercentOfBaseWidth
                    ? Math.Clamp(value, 0, 100)
                    : Math.Max(0, value);
                if (_minimumChildWidthValue == newValue)
                {
                    return;
                }

                _minimumChildWidthValue = newValue;
                PerformLayout();
            }
        }

        [DefaultValue(AdaptiveRowMinimumWidthMode.Pixels)]
        [Category("Layout")]
        [Description("Controls whether MinimumChildWidthValue is treated as a pixel width or a percent of each child's base width.")]
        public AdaptiveRowMinimumWidthMode MinimumChildWidthMode
        {
            get => _minimumChildWidthMode;
            set
            {
                if (_minimumChildWidthMode == value)
                {
                    return;
                }

                _minimumChildWidthMode = value;
                if (_minimumChildWidthMode == AdaptiveRowMinimumWidthMode.PercentOfBaseWidth)
                {
                    _minimumChildWidthValue = Math.Clamp(_minimumChildWidthValue, 0, 100);
                }
                PerformLayout();
            }
        }

        [DefaultValue(-1)]
        [Category("Layout")]
        [Description("LEGACY: TabIndex of the control that compresses first when space is tight. Set to -1 to disable. Ignored when Spring (names) is set; kept so old serialized forms keep working until re-saved.")]
        public int SpringControlTabIndex
        {
            get => _springControlTabIndex;
            set
            {
                if (_springControlTabIndex == value)
                    return;
                _springControlTabIndex = value;
                PerformLayout();
            }
        }

        [DefaultValue(null)]
        [Category("Layout")]
        [Description("Comma list of child control NAMES that spring: they absorb spare width and compress first when space is tight. Multiple names split the spare equally. Takes precedence over the legacy SpringControlTabIndex.")]
        public string? Spring
        {
            get => _spring;
            set
            {
                if (string.Equals(_spring, value, StringComparison.Ordinal))
                    return;
                _spring = value;
                ParseSpring();
                PerformLayout();
            }
        }

        private void ParseSpring()
        {
            _springControlNames.Clear();
            if (string.IsNullOrWhiteSpace(_spring))
                return;

            foreach (string part in _spring.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0)
                    _springControlNames.Add(name);
            }
        }

        // Springs match by Name when Spring is set (names are safe through InitializeComponent —
        // they are "" until each control's own property block runs, so a mid-init layout springs
        // nothing). The legacy TabIndex path only applies when no names are configured.
        internal bool IsSpringControl(Control control)
        {
            if (_springControlNames.Count > 0)
            {
                return !string.IsNullOrWhiteSpace(control.Name)
                    && _springControlNames.Contains(control.Name);
            }

            return _springControlTabIndex >= 0 && control.TabIndex == _springControlTabIndex;
        }

        private int[] GetSpringIndexes(Control[] controls)
        {
            List<int> indexes = new();
            for (int i = 0; i < controls.Length; i++)
            {
                if (IsSpringControl(controls[i]))
                    indexes.Add(i);
            }

            return indexes.ToArray();
        }

        [DefaultValue(AdaptiveRowSpringMode.Both)]
        [Category("Layout")]
        [Description("Controls whether the spring expands into spare width, contracts under overflow, or does both.")]
        public AdaptiveRowSpringMode SpringExpands
        {
            get => _springExpands;
            set
            {
                if (_springExpands == value)
                {
                    return;
                }

                _springExpands = value;
                PerformLayout();
            }
        }

        [DefaultValue(AdaptiveRowGapCompressionMode.RoundRobin)]
        [Category("Layout")]
        [Description("Controls how gaps are eaten when the row overflows. RoundRobin compresses all gaps equally; RightToLeft eats from the rightmost gap first, keeping left-side positions stable.")]
        public AdaptiveRowGapCompressionMode GapCompressionMode
        {
            get => _gapCompressionMode;
            set
            {
                if (_gapCompressionMode == value)
                    return;
                _gapCompressionMode = value;
                PerformLayout();
            }
        }

        [DefaultValue(true)]
        [Description("When true, horizontal spacing is authored from each child control's Left/Right margin instead of inferred designer gaps.")]
        public bool UsePaddingFromItems
        {
            get => _usePaddingFromItems;
            set
            {
                if (_usePaddingFromItems == value)
                {
                    return;
                }

                _usePaddingFromItems = value;
                PerformLayout();
            }
        }

        [DefaultValue(true)]
        [Description("When true, child controls are laid out by TabIndex, which gives you a stable designer-authored order via View -> Tab Order.")]
        public bool UseTabIndexForOrder
        {
            get => _useTabIndexForOrder;
            set
            {
                if (_useTabIndexForOrder == value)
                {
                    return;
                }

                _useTabIndexForOrder = value;
                PerformLayout();
            }
        }

        [DefaultValue(false)]
        [Description("When true, every visible child is resized to the row's usable height.")]
        public bool StretchChildHeight
        {
            get => _stretchChildHeight;
            set
            {
                if (_stretchChildHeight == value)
                {
                    return;
                }

                _stretchChildHeight = value;
                PerformLayout();
            }
        }

        [DefaultValue(AdaptiveRowContentAlignment.Left)]
        [Category("Layout")]
        [Description("Aligns the visible child controls as a group within the row.")]
        public AdaptiveRowContentAlignment ContentAlignment
        {
            get => _contentAlignment;
            set
            {
                if (_contentAlignment == value)
                {
                    return;
                }

                _contentAlignment = value;
                PerformLayout();
            }
        }

        [DefaultValue(false)]
        public bool DebugLayout
        {
            get => _debugLayout;
            set
            {
                if (_debugLayout == value)
                {
                    return;
                }

                _debugLayout = value;
                PerformLayout();
            }
        }

        [Browsable(false)]
        public string LastLayoutReport => _lastLayoutReport;

        [Category("Design")]
        [DisplayName("Contents")]
        [Description("Edit the order of child controls used by the adaptive row layout.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Editor(typeof(AdaptiveRowPanelContentsEditor), typeof(UITypeEditor))]
        public string Contents
        {
            get => _contentsEditorProxy.ToString();
            set
            {
                // No-op. This property exists to surface a modal editor in the designer.
            }
        }

        private bool _adjustHeightForDpi = false;
        private int _dpiHeightGrowthPercent = 25;

        [DefaultValue(false)]
        [Category("Layout")]
        [Description("When true, the panel height scales with DPI changes including when dragged between screens.")]
        public bool AdjustHeightForDpi
        {
            get => _adjustHeightForDpi;
            set
            {
                if (_adjustHeightForDpi == value) return;
                _adjustHeightForDpi = value;
                if (IsHandleCreated)
                {
                    CaptureCurrentHeightAsDpiBaseline();
                    ApplyDpiAdjustedHeight();
                }
                PerformLayout();
            }
        }

        [DefaultValue(25)]
        [Category("Layout")]
        [Description("Percent of the DPI growth applied to row height. 25 means 150% DPI becomes roughly 112.5% height.")]
        public int DpiHeightGrowthPercent
        {
            get => _dpiHeightGrowthPercent;
            set
            {
                int newValue = Math.Clamp(value, 0, 100);
                if (_dpiHeightGrowthPercent == newValue) return;
                _dpiHeightGrowthPercent = newValue;
                ApplyDpiAdjustedHeight();
                PerformLayout();
            }
        }

        [Browsable(false)]
        internal AdaptiveRowPanelContents ContentsProxy => _contentsEditorProxy;

        [Category("Design")]
        [Description("Design-time item rules that store layout properties for child controls. Dragging a control in creates its rule; deleting or dragging a control out removes its rule immediately.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdaptiveRowItemRuleCollection ItemRules => _itemRules;

        public void CaptureCurrentLayoutAsBaseline()
        {
            foreach (Control control in Controls.Cast<Control>())
            {
                _baseWidths[control] = control.Width;
                _baseLefts[control] = control.Left;
            }
            PerformLayout();
        }

        public void ForceRefreshLayout()
        {
            NormalizeDesignTimeBaselines();
            PerformLayout();
        }

        internal IReadOnlyList<Control> GetOrderedChildren()
        {
            return Controls
                .Cast<Control>()
                .Where(c => c != null && !string.IsNullOrEmpty(c.Name))
                .Distinct()
                .OrderBy(GetLayoutOrder)
                .ThenBy(c => c.Top)
                .ToArray();
        }

        internal void ApplyDesignerOrder(IReadOnlyList<Control> orderedControls)
        {
            for (int i = 0; i < orderedControls.Count; i++)
            {
                Control control = orderedControls[i];
                TypeDescriptor.GetProperties(control)["TabIndex"]?.SetValue(control, i * 2);
            }

            PerformLayout();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control == null)
            {
                return;
            }

            ApplyDefaultChildMargin(e.Control);

            // At design time capture immediately so the designer reflects the right baseline.
            // At runtime, Controls.Add fires before InitializeComponent sets the control's Size,
            // so capturing here would freeze the constructor default (e.g. 120px) not the
            // authored design-time size. GetBaseWidth has a lazy fallback that reads
            // control.Width on first layout, by which point ResumeLayout has run and all
            // sizes are correct.
            if (IsInDesignMode())
                CaptureControlBaseline(e.Control);
            e.Control.VisibleChanged += ChildLayoutChanged;
            e.Control.SizeChanged += ChildLayoutChanged;
            e.Control.FontChanged += ChildLayoutChanged;

            AdoptExternalChildIntoItemRules(e.Control);

            if (IsInDesignMode())
            {
                NormalizeDesignTimeBaselines();
                PerformLayout();
            }
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            if (e.Control == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ARP-DBG] OnControlRemoved fired. child='{e.Control.Name}' type={e.Control.GetType().Name} designMode={IsInDesignMode()} syncing={_isSyncingItemRules}");

            if (e.Control.TabIndex == SpringControlTabIndex)
            {
                SpringControlTabIndex = -1;
            }

            _baseWidths.Remove(e.Control);
            _baseLefts.Remove(e.Control);
            _baseFontSizes.Remove(e.Control);
            e.Control.VisibleChanged -= ChildLayoutChanged;
            e.Control.SizeChanged -= ChildLayoutChanged;
            e.Control.FontChanged -= ChildLayoutChanged;

            RemoveExternalChildFromItemRules(e.Control);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!DesignMode) return;
            using var pen = new System.Drawing.Pen(System.Drawing.Color.Gray);
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            // While the undo engine replays state, restored child locations must not be
            // clobbered by a row-layout pass (undo restores Location while the control may
            // still transiently be parented here). Re-layout once after the undo finishes.
            if (IsUndoInProgress())
            {
                QueuePostUndoRefresh();
                return;
            }

            base.OnLayout(levent);
            LayoutChildren();
        }

        private void ChildLayoutChanged(object? sender, EventArgs e)
        {
            if (_isApplyingLayout)
            {
                return;
            }

            if (IsUndoInProgress())
            {
                QueuePostUndoRefresh();
                return;
            }

            if (!_isApplyingLayout && sender is Control child && IsInDesignMode())
            {
                CaptureControlBaseline(child);
                SyncRuleFromChild(child);
            }

            if (!IsDisposed && !Disposing)
            {
                PerformLayout();
            }
        }

        private void LayoutChildren()
        {
            var visibleControls = Controls
                .Cast<Control>()
                .Where(c => c.Visible)
                .OrderBy(GetLayoutOrder)
                .ThenBy(c => c.Top)
                .ToArray();

            if (visibleControls.Length == 0)
            {
                return;
            }

            ApplyDpiFontStep(visibleControls);

            int topInset = Padding.Top;
            int bottomInset = Padding.Bottom;
            int availableHeight = Math.Max(0, ClientSize.Height - topInset - bottomInset);
            int availableWidth = Math.Max(0, ClientSize.Width - Padding.Left - Padding.Right);
            List<string>? debugParts = _debugLayout ? new List<string>() : null;
            int[] widths = visibleControls.Select(GetEffectiveWidth).ToArray();
            int[] gaps = BuildGaps(visibleControls);
            int leadingSpacing = GetLeadingSpacing(visibleControls[0]);
            int trailingSpacing = GetTrailingSpacing(visibleControls[^1]);

            FitRowToAvailableWidth(visibleControls, widths, gaps, availableWidth, leadingSpacing, trailingSpacing);
            int x = GetAlignedStartX(widths, gaps, availableWidth, leadingSpacing, trailingSpacing);

            _isApplyingLayout = true;
            try
            {
                for (int i = 0; i < visibleControls.Length; i++)
                {
                    Control child = visibleControls[i];
                    int width = widths[i];

                    int height = _stretchChildHeight ? Math.Max(1, availableHeight) : child.Height;
                    int y = _stretchChildHeight
                        ? topInset
                        : topInset + Math.Max(0, (availableHeight - height) / 2);
                    Rectangle newBounds = new Rectangle(x, y, width, height);

                    if (child.Bounds != newBounds)
                    {
                        child.Bounds = newBounds;
                    }

                    if (debugParts != null)
                    {
                        string gapText = i < gaps.Length ? $",g={gaps[i]}" : string.Empty;
                        debugParts.Add($"{child.Name}:{GetBaseWidth(child)}->{width}{gapText}");
                    }

                    if (i < visibleControls.Length - 1)
                    {
                        x += width + gaps[i];
                    }
                }
            }
            finally
            {
                _isApplyingLayout = false;
            }

            if (debugParts != null)
            {
                string report = $"dpi={DeviceDpi} {string.Join(" | ", debugParts)}";
                Debug.WriteLine($"[AdaptiveRowPanel:{Name}] {report}");
                if (!string.Equals(_lastLayoutReport, report, StringComparison.Ordinal))
                {
                    _lastLayoutReport = report;
                    LayoutReportChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private float GetScaleFactor()
        {
            int dpi = GetCurrentDpi();
            return dpi > 0 ? dpi / 96f : 1f;
        }

        private int GetCurrentDpi()
        {
            if (IsHandleCreated)
            {
                try
                {
                    uint dpi = GetDpiForWindow(Handle);
                    if (dpi > 0)
                    {
                        return (int)dpi;
                    }
                }
                catch (EntryPointNotFoundException)
                {
                }
                catch (DllNotFoundException)
                {
                }
            }

            return DeviceDpi > 0 ? DeviceDpi : 96;
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private bool _stepDownFontAtHighDpi;

        [DefaultValue(false)]
        [Category("Layout")]
        [Description("When true, this panel steps its child fonts down 2pt at 150%+ DPI even if the global StepDownHighDpiFonts setting is off. Default off; the global setting normally controls it.")]
        public bool StepDownFontAtHighDpi
        {
            get => _stepDownFontAtHighDpi;
            set
            {
                if (_stepDownFontAtHighDpi == value) return;
                _stepDownFontAtHighDpi = value;
                PerformLayout();
            }
        }

        // Runtime DPI font fit: point fonts render ~1.5x the pixels at 150% but the
        // fixed-pixel control boxes don't grow (AutoScaleMode.None), so text crowds and
        // clips. Step every child font down 2pt at high DPI. The authored size is
        // captured once per control and the target is recomputed from it, so this is
        // idempotent and self-restores at 100%. Guarded with _isApplyingLayout so the
        // resulting FontChanged can't recurse layout. Design time is left untouched.
        private void ApplyDpiFontStep(Control[] children)
        {
            if (IsInDesignMode())
            {
                return;
            }

            bool stepEnabled = ControlSettings.StepDownHighDpiFonts
                || _stepDownFontAtHighDpi;
            int reduction = (GetScaleFactor() >= 1.5f && stepEnabled) ? 2 : 0;

            bool previous = _isApplyingLayout;
            _isApplyingLayout = true;
            try
            {
                foreach (Control child in children)
                {
                    if (!_baseFontSizes.TryGetValue(child, out float basePoints))
                    {
                        basePoints = child.Font.SizeInPoints;
                        _baseFontSizes[child] = basePoints;
                    }

                    float targetPoints = Math.Max(1f, basePoints - reduction);
                    ApplySteppedFont(child, targetPoints);
                }
            }
            finally
            {
                _isApplyingLayout = previous;
            }
        }

        private static void ApplySteppedFont(Control child, float targetPoints)
        {
            if (Math.Abs(child.Font.SizeInPoints - targetPoints) <= 0.05f)
            {
                return;
            }

            Font steppedFont = new Font(child.Font.FontFamily, targetPoints, child.Font.Style, GraphicsUnit.Point);
            child.Font = steppedFont;

            if (child is DateTimePicker dateTimePicker)
            {
                PropertyDescriptor? calendarFont = TypeDescriptor.GetProperties(dateTimePicker)["CalendarFont"];
                if (calendarFont != null && !calendarFont.IsReadOnly)
                {
                    calendarFont.SetValue(dateTimePicker, new Font(steppedFont.FontFamily, targetPoints, steppedFont.Style, GraphicsUnit.Point));
                }

                if (dateTimePicker.IsHandleCreated)
                {
                    dateTimePicker.Invalidate();
                    dateTimePicker.Update();
                }
            }
        }

        private int GetEffectiveWidth(Control control)
        {
            int baseWidth = GetBaseWidth(control);
            if (IsInDesignMode())
            {
                return Math.Max(1, baseWidth);
            }

            return Math.Max(GetMinimumWidth(control, baseWidth), baseWidth);
        }

        private int[] BuildGaps(Control[] controls)
        {
            if (controls.Length <= 1)
            {
                return Array.Empty<int>();
            }

            int[] gaps = new int[controls.Length - 1];
            for (int i = 0; i < controls.Length - 1; i++)
            {
                gaps[i] = _usePaddingFromItems
                    ? GetInterItemSpacing(controls[i], controls[i + 1])
                    : GetAuditedGap(controls[i], controls[i + 1]);
            }

            return gaps;
        }

        private int GetAuditedGap(Control current, Control next)
        {
            int currentRight = GetBaseLeft(current) + GetBaseWidth(current);
            int nextLeft = GetBaseLeft(next);
            int gap = Math.Max(0, nextLeft - currentRight);
            return gap > 0 ? gap : _itemGap;
        }

        private int GetLeadingSpacing(Control control)
        {
            if (!_usePaddingFromItems)
            {
                return 0;
            }

            return Math.Max(0, control.Margin.Left);
        }





        private int GetTrailingSpacing(Control control)
        {
            if (!_usePaddingFromItems)
            {
                return 0;
            }

            return Math.Max(0, control.Margin.Right);
        }

        private int GetInterItemSpacing(Control current, Control next)
        {
            // The gap is the sum of the two facing margins — the single source of truth.
            // No ItemGap fallback: zero margins mean a flush edge, not a hidden default gap.
            return Math.Max(0, current.Margin.Right) + Math.Max(0, next.Margin.Left);
        }

        private int GetAlignedStartX(
            int[] widths,
            int[] gaps,
            int availableWidth,
            int leadingSpacing,
            int trailingSpacing)
        {
            int groupWidth = leadingSpacing + widths.Sum() + gaps.Sum() + trailingSpacing;
            int spareWidth = Math.Max(0, availableWidth - groupWidth);
            int offset = _contentAlignment switch
            {
                AdaptiveRowContentAlignment.Center => spareWidth / 2,
                AdaptiveRowContentAlignment.Right => spareWidth,
                _ => 0
            };

            return Padding.Left + leadingSpacing + offset;
        }

        private void FitRowToAvailableWidth(
            Control[] controls,
            int[] widths,
            int[] gaps,
            int availableWidth,
            int leadingSpacing,
            int trailingSpacing)
        {
            int edgeSpacing = leadingSpacing + trailingSpacing;
            int totalWidth = edgeSpacing + widths.Sum() + gaps.Sum();
            if (totalWidth <= availableWidth)
            {
                if (CanSpringExpand())
                    ExpandSpringToFillAvailableWidth(controls, widths, availableWidth - totalWidth);
                return;
            }

            int overflow = totalWidth - availableWidth;
            int[] minimumWidths = controls
                .Select((control, index) => GetMinimumWidth(control, widths[index]))
                .ToArray();

            int[] springIndexes = GetSpringIndexes(controls);
            if (springIndexes.Length > 0 && CanSpringContract())
            {
                foreach (int springIndex in springIndexes)
                {
                    minimumWidths[springIndex] = GetSpringMinimumWidth(controls[springIndex]);
                }

                overflow -= ReduceSpringWidthsRoundRobin(widths, minimumWidths, springIndexes, overflow);
            }

            if (overflow <= 0)
                return;

            ReduceGaps(gaps, overflow);

            totalWidth = edgeSpacing + widths.Sum() + gaps.Sum();
            if (totalWidth <= availableWidth)
            {
                if (CanSpringExpand())
                    ExpandSpringToFillAvailableWidth(controls, widths, availableWidth - totalWidth);
                return;
            }

            overflow = totalWidth - availableWidth;
            ReduceWidthsRoundRobin(widths, minimumWidths, overflow);
        }

        private bool CanSpringExpand()
        {
            return _springExpands is AdaptiveRowSpringMode.Expands or AdaptiveRowSpringMode.Both;
        }

        private bool CanSpringContract()
        {
            return _springExpands is AdaptiveRowSpringMode.Contracts or AdaptiveRowSpringMode.Both;
        }

        private void ExpandSpringToFillAvailableWidth(Control[] controls, int[] widths, int spareWidth)
        {
            if (spareWidth <= 0)
            {
                return;
            }

            int[] springIndexes = GetSpringIndexes(controls);
            if (springIndexes.Length == 0)
            {
                return;
            }

            int perSpring = spareWidth / springIndexes.Length;
            int remainder = spareWidth % springIndexes.Length;
            foreach (int springIndex in springIndexes)
            {
                int extra = perSpring + (remainder > 0 ? 1 : 0);
                if (remainder > 0)
                {
                    remainder--;
                }

                widths[springIndex] += extra;
            }
        }

        // Shrink the springs round-robin toward their floors; returns how much width was reclaimed.
        private static int ReduceSpringWidthsRoundRobin(int[] widths, int[] minimumWidths, int[] springIndexes, int reductionNeeded)
        {
            int reduced = 0;
            while (reductionNeeded > 0)
            {
                bool changed = false;
                foreach (int i in springIndexes)
                {
                    if (reductionNeeded <= 0)
                        break;

                    if (widths[i] <= minimumWidths[i])
                        continue;

                    widths[i]--;
                    reductionNeeded--;
                    reduced++;
                    changed = true;
                }

                if (!changed)
                    break;
            }

            return reduced;
        }

        private void ReduceGaps(int[] gaps, int reductionNeeded)
        {
            if (_gapCompressionMode == AdaptiveRowGapCompressionMode.RightToLeft)
                ReduceValuesRightToLeft(gaps, reductionNeeded);
            else
                ReduceValuesRoundRobin(gaps, reductionNeeded);
        }

        private static void ReduceValuesRightToLeft(int[] values, int reductionNeeded)
        {
            if (reductionNeeded <= 0 || values.Length == 0)
                return;

            for (int i = values.Length - 1; i >= 0 && reductionNeeded > 0; i--)
            {
                int reduce = Math.Min(values[i], reductionNeeded);
                values[i] -= reduce;
                reductionNeeded -= reduce;
            }
        }

        private static void ReduceValuesRoundRobin(int[] values, int reductionNeeded)
        {
            if (reductionNeeded <= 0 || values.Length == 0)
            {
                return;
            }

            while (reductionNeeded > 0)
            {
                bool changed = false;
                for (int i = 0; i < values.Length && reductionNeeded > 0; i++)
                {
                    if (values[i] <= 0)
                    {
                        continue;
                    }

                    values[i]--;
                    reductionNeeded--;
                    changed = true;
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private static void ReduceWidthsRoundRobin(int[] widths, int[] minimumWidths, int reductionNeeded)
        {
            if (reductionNeeded <= 0 || widths.Length == 0)
            {
                return;
            }

            while (reductionNeeded > 0)
            {
                bool changed = false;
                for (int i = 0; i < widths.Length && reductionNeeded > 0; i++)
                {
                    if (widths[i] <= minimumWidths[i])
                    {
                        continue;
                    }

                    widths[i]--;
                    reductionNeeded--;
                    changed = true;
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private int GetBaseWidth(Control control)
        {
            if (_baseWidths.TryGetValue(control, out int width) && width > 0)
            {
                if (!IsInDesignMode()
                    && TryGetAuthoredRuleWidth(control, out int authoredWidth)
                    && authoredWidth > width)
                {
                    width = authoredWidth;
                    _baseWidths[control] = width;
                }

                return width;
            }

            width = !IsInDesignMode() && TryGetAuthoredRuleWidth(control, out int ruleWidth)
                ? ruleWidth
                : control.Width;
            _baseWidths[control] = width;
            return width;
        }

        private bool TryGetAuthoredRuleWidth(Control control, out int width)
        {
            width = 0;
            if (string.IsNullOrWhiteSpace(control.Name))
            {
                return false;
            }

            AdaptiveRowItemRule? rule = _itemRules.FirstOrDefault(item =>
                string.Equals(item.ControlName?.Trim(), control.Name, StringComparison.Ordinal));
            if (rule == null || rule.Width <= 0)
            {
                return false;
            }

            width = rule.Width;
            return true;
        }

        private int GetBaseLeft(Control control)
        {
            if (_baseLefts.TryGetValue(control, out int left))
            {
                return left;
            }

            left = control.Left;
            _baseLefts[control] = left;
            return left;
        }

        private void CaptureControlBaseline(Control control)
        {
            _baseWidths[control] = control.Width;
            _baseLefts[control] = control.Left;
        }

        private void ApplyDefaultChildMargin(Control control)
        {
            if (!IsInDesignMode())
            {
                return;
            }

            Padding margin = control.Margin;
            bool hasUntouchedDefaultMargin =
                margin.Left == 3 &&
                margin.Top == 3 &&
                margin.Right == 3 &&
                margin.Bottom == 3;

            if (!hasUntouchedDefaultMargin)
            {
                return;
            }

            control.Margin = new Padding(
                _defaultChildHorizontalMargin,
                margin.Top,
                _defaultChildHorizontalMargin,
                margin.Bottom);
        }

        private void NormalizeDesignTimeBaselines()
        {
            Control[] orderedControls = Controls
                .Cast<Control>()
                .Where(c => c.Visible)
                .OrderBy(GetLayoutOrder)
                .ThenBy(c => c.Top)
                .ToArray();

            if (orderedControls.Length == 0)
            {
                return;
            }

            int x = Padding.Left + GetLeadingSpacing(orderedControls[0]);

            for (int i = 0; i < orderedControls.Length; i++)
            {
                Control control = orderedControls[i];
                _baseWidths[control] = control.Width;
                _baseLefts[control] = x;

                if (i >= orderedControls.Length - 1)
                {
                    continue;
                }

                int gap = _usePaddingFromItems
                    ? GetInterItemSpacing(control, orderedControls[i + 1])
                    : _itemGap;

                x += control.Width + gap;
            }
        }

        private int GetLayoutOrder(Control control)
        {
            if (_useTabIndexForOrder)
            {
                return control.TabIndex;
            }

            return GetBaseLeft(control);
        }

        private int GetMinimumWidth(Control control, int currentWidth)
        {
            // Structural dividers keep their authored width — the panel minimum is for compressible
            // content controls, not a thin line break, which would otherwise be padded out to it.
            if (control is AdaptiveLineBreak)
            {
                return Math.Max(1, Math.Max(GetContentMinimumWidth(control), control.MinimumSize.Width));
            }

            int panelMinimum = _minimumChildWidthMode == AdaptiveRowMinimumWidthMode.PercentOfBaseWidth
                ? (int)Math.Round(currentWidth * (_minimumChildWidthValue / 100f))
                : _minimumChildWidthValue;

            return Math.Max(GetContentMinimumWidth(control), Math.Max(control.MinimumSize.Width, Math.Max(1, panelMinimum)));
        }

        private static int GetSpringMinimumWidth(Control control)
        {
            return Math.Max(GetContentMinimumWidth(control), Math.Max(0, control.MinimumSize.Width));
        }

        private static int GetContentMinimumWidth(Control control)
        {
            return control is ModernButton button
                ? button.GetPreferredContentWidth()
                : 0;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CaptureCurrentHeightAsDpiBaseline();
            ApplyDpiAdjustedHeight();
            PerformLayout();

            if (IsInDesignMode() && _itemRulesSyncPending)
            {
                BeginInvoke((MethodInvoker)SyncChildrenFromRules);
            }
        }


        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyDpiAdjustedHeight();
            PerformLayout();
        }

        private void ApplyDpiAdjustedHeight()
        {
            if (!_adjustHeightForDpi || !IsHandleCreated || IsInDesignMode() || IsDisposed)
            {
                return;
            }

            if (_baseHeight == 0)
            {
                _baseHeight = Height;
            }

            float scale = Math.Max(1f, GetScaleFactor());
            float growth = Math.Max(0f, scale - 1f);
            float adjustedScale = 1f + (growth * (_dpiHeightGrowthPercent / 100f));
            int adjustedHeight = Math.Max(1, (int)Math.Round(_baseHeight * adjustedScale));

            if (Height != adjustedHeight)
            {
                Height = adjustedHeight;
            }
        }

        private void CaptureCurrentHeightAsDpiBaseline()
        {
            if (_adjustHeightForDpi && _baseHeight == 0 && Height > 0)
            {
                _baseHeight = Height;
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

        private bool IsDesignerHostLoading()
        {
            return ((IDesignerHost?)Site?.GetService(typeof(IDesignerHost)))?.Loading == true;
        }

        // UndoEngine lives in the designer assemblies (not referenced at compile time),
        // so it's resolved by reflection inside the design process. Outside the designer
        // (or if resolution fails) this simply reports false.
        private static Type[] _undoEngineCandidateTypes = Array.Empty<Type>();
        private static Type? _undoEngineServiceType;
        private static System.Reflection.PropertyInfo? _undoInProgressProperty;
        private static bool _undoEngineResolveAttempted;

        private bool IsUndoInProgress()
        {
            if (Site == null)
            {
                return false;
            }

            if (!_undoEngineResolveAttempted)
            {
                _undoEngineResolveAttempted = true;
                // The OOP designer (DesignToolsServer) registers its own undo engine type;
                // the classic System.Design one is tried as a fallback for other hosts.
                _undoEngineCandidateTypes = new[]
                    {
                        AppDomain.CurrentDomain.GetAssemblies()
                            .Select(assembly => assembly.GetType("Microsoft.DotNet.DesignTools.Undo.UndoEngine"))
                            .FirstOrDefault(type => type != null),
                        Type.GetType("System.ComponentModel.Design.UndoEngine, System.Windows.Forms.Design")
                            ?? Type.GetType("System.ComponentModel.Design.UndoEngine, System.Design")
                    }
                    .Where(type => type != null)
                    .Cast<Type>()
                    .ToArray();
            }

            object? undoEngine = null;
            if (_undoEngineServiceType != null)
            {
                undoEngine = Site.GetService(_undoEngineServiceType);
            }
            else
            {
                foreach (Type candidate in _undoEngineCandidateTypes)
                {
                    undoEngine = Site.GetService(candidate);
                    if (undoEngine != null)
                    {
                        _undoEngineServiceType = candidate;
                        break;
                    }
                }
            }

            if (undoEngine == null)
            {
                return false;
            }

            if (_undoInProgressProperty == null)
            {
                _undoInProgressProperty = undoEngine.GetType().GetProperty(
                    "UndoInProgress",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (_undoInProgressProperty == null)
                {
                    return false;
                }
            }

            return _undoInProgressProperty.GetValue(undoEngine) is true;
        }

        // One deferred relayout after the undo engine finishes replaying — BeginInvoke
        // lands after the (synchronous, UI-thread) undo completes.
        private void QueuePostUndoRefresh()
        {
            if (_postUndoRefreshQueued || !IsHandleCreated || Disposing || IsDisposed)
            {
                return;
            }

            _postUndoRefreshQueued = true;
            BeginInvoke((MethodInvoker)(() =>
            {
                _postUndoRefreshQueued = false;
                if (!Disposing && !IsDisposed)
                {
                    ForceRefreshLayout();
                }
            }));
        }

        internal void OnItemRulesChanged()
        {
            RequestItemRulesSync();
        }

        internal void OnItemRuleSpringChanged(AdaptiveRowItemRule rule)
        {
            if (_isSyncingItemRules)
            {
                return;
            }

            RequestItemRulesSync();
        }

        // Raised when the user deliberately changes a rule's ControlType in the rules editor.
        // This is the one place a type change SHOULD rebuild the control — the user is morphing
        // it to a new type, so losing the old control's type-specific properties is expected.
        // (The generic SyncChildrenFromRules stays non-destructive and never replaces, so a
        // transient rule/child type mismatch during a layout swap can't silently rebuild.)
        internal void OnItemRuleControlTypeChanged(AdaptiveRowItemRule rule)
        {
            if (!IsInDesignMode() || _isSyncingItemRules)
            {
                return;
            }

            if (IsSupportedItemRule(rule)
                && FindChildForRule(rule) is Control child
                && !RuleMatchesExistingChild(rule.ControlType, child)
                && IsItemRuleManagedChild(child))
            {
                _isSyncingItemRules = true;
                try
                {
                    ReplaceDesignerChild(child, rule);
                }
                finally
                {
                    _isSyncingItemRules = false;
                }
            }

            RequestItemRulesSync();
        }

        internal void PrepareNewItemRule(AdaptiveRowItemRule rule)
        {
            bool hadControlName = !string.IsNullOrWhiteSpace(rule.ControlName);
            bool isBlankRule = rule.ControlType == AdaptiveRowPanelAddControlType.Spacer
                && string.IsNullOrWhiteSpace(rule.ControlName)
                && string.IsNullOrWhiteSpace(rule.Text);

            if (isBlankRule)
            {
                rule.ControlType = AdaptiveRowPanelAddControlType.Button;
            }

            if (!IsSupportedItemRule(rule))
            {
                return;
            }

            string namePrefix = GetDefaultNamePrefix(rule.ControlType);
            string textPrefix = GetDefaultTextPrefix(rule.ControlType);

            int index = GetNextItemRuleIndex(namePrefix, rule);
            if (string.IsNullOrWhiteSpace(rule.ControlName))
            {
                rule.ControlName = $"{namePrefix}{index}";
            }

            if (!hadControlName)
            {
                // A new collection-editor row may need a designer child, but ID migration
                // is deliberately only done by the smart-tag action.
                _pendingItemRuleChildCreates.Add(rule);
            }

            if (string.IsNullOrWhiteSpace(rule.Text))
            {
                rule.Text = UsesBlankDefaultText(rule.ControlType) ? string.Empty : $"{textPrefix} {index}";
            }
        }

        internal void OnItemRuleNameChanged(AdaptiveRowItemRule rule, string oldControlName, string newControlName)
        {
            if (!IsSupportedItemRule(rule) || !IsInDesignMode() || _isSyncingItemRules)
            {
                return;
            }

            Control? oldChild = FindChildForRule(rule) ?? (!string.IsNullOrWhiteSpace(oldControlName) ? FindChild(oldControlName) : null);
            if (oldChild != null
                && IsValidComponentName(newControlName)
                && FindChild(newControlName) == null
                && IsDesignerComponentNameAvailable(newControlName, oldChild))
            {
                RenameDesignerChild(oldChild, newControlName);
                ApplyRuleToChild(rule, oldChild);
                CaptureCurrentLayoutAsBaseline();
                ForceRefreshLayout();
                return;
            }

            RequestItemRulesSync();
        }

        internal void SyncRuleFromChild(Control child)
        {
            if (!IsInDesignMode() || _isSyncingItemRules || string.IsNullOrWhiteSpace(child.Name))
            {
                return;
            }

            AdaptiveRowItemRule? rule = FindRuleForChild(child);

            if (rule == null)
            {
                return;
            }

            // Silent on purpose — no change-service announcements; see the comment in
            // AdoptExternalChildIntoItemRules. This runs on every child change (including
            // mid-drag via the designer's ComponentChanged handler), and announcing here
            // poisons the drag undo unit's snapshots.
            _isSyncingItemRules = true;
            try
            {
                SyncRuleIdFromChild(rule, child);
                rule.ControlName = child.Name;
                rule.ControlType = GetControlTypeId(child);
                rule.Text = child.Text;
                rule.Width = child.Width;
                rule.MarginLeft = child.Margin.Left;
                rule.MarginRight = child.Margin.Right;
            }
            finally
            {
                _isSyncingItemRules = false;
            }
        }

        /// <summary>
        /// Design-time repair for rule/child drift: create a rule for any managed child that has
        /// none, and prune rules whose control no longer exists (orphans). Blank authoring
        /// placeholders (no Id and no ControlName) are left alone. Triggered by the smart-tag
        /// "Reindex Rules" action; wrap the call in a designer transaction so it serialises + undoes.
        /// </summary>
        public void ReindexItemRules()
        {
            if (!IsInDesignMode())
            {
                return;
            }

            // 0) De-duplicate child tags. Copy/pasting a child in the designer CLONES its Tag GUID,
            //    so the copy resolves to the original's rule — adoption then thinks it is already
            //    ruled and skips it forever ("reindex won't accept my pasted control"). Re-mint a
            //    fresh tag for every extra holder so step 2 can adopt them properly.
            _isSyncingItemRules = true;
            try
            {
                var duplicateTagGroups = Controls.Cast<Control>()
                    .Where(c => IsPrefixedItemRuleId(GetChildRuleId(c)))
                    .GroupBy(c => GetChildRuleId(c), StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .ToArray();

                foreach (var group in duplicateTagGroups)
                {
                    AdaptiveRowItemRule? owner = _itemRules.FirstOrDefault(r =>
                        string.Equals(r.Id, group.Key, StringComparison.Ordinal));

                    // The child that keeps the tag is the one the rule actually names; otherwise the
                    // first one wins and the rest are treated as copies.
                    Control keeper = group.FirstOrDefault(c => owner != null
                            && string.Equals(c.Name, owner.ControlName?.Trim(), StringComparison.Ordinal))
                        ?? group.First();

                    foreach (Control duplicate in group)
                    {
                        if (ReferenceEquals(duplicate, keeper))
                        {
                            continue;
                        }

                        TrySetChildRuleTag(duplicate, CreateItemRuleId(), overwrite: true);
                    }
                }
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            // 1) Repair rules whose Id no longer matches their child's tag (copy/paste GUID drift).
            //    This MUST run before any pruning: FindChildForRule deliberately returns null when a
            //    rule and its child BOTH carry GUIDs that differ, so an unrepaired rule looks like an
            //    orphan and would be deleted even though its control is sitting right there — taking
            //    that control's width/margins/spring with it.
            _isSyncingItemRules = true;
            try
            {
                ReconcileOrphanedRuleIds();
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            // 2) Adopt children that have no rule. AdoptExternalChildIntoItemRules is idempotent
            //    (it skips children that already have a rule and unsupported types) and manages
            //    the _isSyncingItemRules flag itself, so it runs OUTSIDE the prune guard below.
            foreach (Control child in Controls.Cast<Control>().ToArray())
            {
                AdoptExternalChildIntoItemRules(child);
            }

            // 3) Prune orphaned rules — a rule whose control no longer exists at all.
            _isSyncingItemRules = true;
            try
            {
                Control[] children = Controls.Cast<Control>().ToArray();

                for (int i = _itemRules.Count - 1; i >= 0; i--)
                {
                    AdaptiveRowItemRule rule = _itemRules[i];

                    // Leave blank authoring placeholders (no id AND no control name) untouched.
                    if (string.IsNullOrWhiteSpace(rule.Id) && string.IsNullOrWhiteSpace(rule.ControlName))
                    {
                        continue;
                    }

                    if (FindChildForRule(rule) != null)
                    {
                        continue;
                    }

                    // Safety net: never delete a rule while a live child still carries its name.
                    // Losing a real control's rule scrambles the row; leaving a stale rule does not.
                    string name = rule.ControlName?.Trim() ?? string.Empty;
                    if (IsValidComponentName(name)
                        && children.Any(c => string.Equals(c.Name, name, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    _itemRules.RemoveAt(i);
                }
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            ForceRefreshLayout();
        }

        internal void AdoptExternalChildIntoItemRules(Control child)
        {
            // Runs during undo/redo replay too: rules are invisible to the undo engine,
            // so when undo re-adds a child (e.g. undoing a drag-out) the rule must be
            // recreated here — nothing else will.
            if (!IsInDesignMode() || _isSyncingItemRules || string.IsNullOrWhiteSpace(child.Name))
            {
                return;
            }

            AdaptiveRowPanelAddControlType controlType = GetControlTypeId(child);
            if (!IsSupportedItemRuleType(controlType))
            {
                return;
            }

            AdaptiveRowItemRule? existingRule = FindRuleForChild(child);
            if (existingRule != null)
            {
                SyncRuleIdFromChild(existingRule, child);
                return;
            }

            // Deliberately NO IComponentChangeService announcements anywhere in adoption
            // (and none in the Tag/TabIndex writes below). Any announcement here lands
            // inside the designer's open drag undo unit and gets the child/panel
            // snapshotted at the drop location — undoing the drag then restores the
            // control to the wrong place. Rules shadow child membership silently; the
            // Designer.cs picks them up from live state at flush time.
            // The whole adoption runs under _isSyncingItemRules to block reentrancy via
            // AdaptiveRowPanelDesigner's ComponentChanged handler.
            _isSyncingItemRules = true;
            try
            {
                string id;
                if (IsDesignerHostLoading())
                {
                    // Designer load: children land in Controls before ItemRules deserialize,
                    // so keep whatever id the Tag already carries — the serialized rule with
                    // the same id arrives right after and InsertItem merges by ControlName.
                    string childId = GetChildRuleId(child);
                    id = IsPrefixedItemRuleId(childId) || IsBareDesignerGuid(childId)
                        ? childId
                        : string.Empty;
                    if (string.IsNullOrWhiteSpace(id) && CanAssignRuleTag(child))
                    {
                        id = CreateItemRuleId();
                        TrySetChildRuleTag(child, id);
                    }
                }
                else
                {
                    // Genuine drag-in: the control is new to this panel, so anything the Tag
                    // held (stale arp id from a previous panel, or otherwise) is replaced with
                    // a fresh id, and the control goes to the end of the tab order.
                    id = CreateItemRuleId();
                    TrySetChildRuleTag(child, id, overwrite: true);
                    SetChildTabIndex(child, GetNextChildTabIndex(child));
                }

                var newRule = new AdaptiveRowItemRule
                {
                    Id = id,
                    ControlName = child.Name,
                    ControlType = controlType,
                    Text = child.Text,
                    Width = Math.Max(1, child.Width),
                    MarginLeft = Math.Max(0, child.Margin.Left),
                    MarginRight = Math.Max(0, child.Margin.Right),
                    IsSpring = IsSpringControl(child)
                };

                int insertIndex = FindRuleInsertIndexByTabIndex(child);
                if (insertIndex >= 0 && insertIndex < _itemRules.Count)
                    _itemRules.Insert(insertIndex, newRule);
                else
                    _itemRules.Add(newRule);
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            CaptureCurrentLayoutAsBaseline();
            ForceRefreshLayout();
        }

        private int FindRuleInsertIndexByTabIndex(Control child)
        {
            var nameToTabIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Control c in Controls)
                if (!string.IsNullOrWhiteSpace(c.Name))
                    nameToTabIndex[c.Name] = c.TabIndex;

            int insertAfter = -1;
            for (int i = 0; i < _itemRules.Count; i++)
            {
                string name = _itemRules[i].ControlName?.Trim() ?? string.Empty;
                if (nameToTabIndex.TryGetValue(name, out int existingTabIndex) && existingTabIndex < child.TabIndex)
                    insertAfter = i;
            }

            return insertAfter + 1;
        }

        internal void RemoveExternalChildFromItemRules(Control child)
        {
            // Immediate rule deletion: fires when the user deletes a child or drags it
            // out to another parent — and during undo replay (undoing a drag-in pulls the
            // child out; the rule must go with it since rules are invisible to the undo
            // engine). Silent on purpose: no change-service announcements — see the
            // comment in AdoptExternalChildIntoItemRules. The remaining guards keep it
            // from firing for rule-driven child destruction (_suppressChildRulePrune),
            // sync passes, form teardown, and designer (re)load — where OnControlRemoved
            // fires for every child and deleting rules would wipe the collection.
            if (!IsInDesignMode() || _suppressChildRulePrune > 0 || _isSyncingItemRules
                || Disposing || IsDisposed || IsDesignerHostLoading())
                return;

            AdaptiveRowItemRule? rule = FindRuleForChild(child);
            if (rule == null)
                return;

            _isSyncingItemRules = true;
            try
            {
                _itemRules.Remove(rule);
                _pendingItemRuleChildCreates.Remove(rule);
                string key = GetRuleKey(rule);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _pendingItemRuleChildRemovals.Remove(key);
                }
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            // This removal is deliberately silent (no sync), so Spring must be recomputed
            // here or a dragged-out/deleted spring child leaves its name behind.
            ApplyItemRuleSpringState();
            CaptureCurrentLayoutAsBaseline();
            ForceRefreshLayout();
        }

        private void RequestItemRulesSync()
        {
            if (!IsInDesignMode() || _isSyncingItemRules || _itemRulesSyncPending)
            {
                return;
            }

            _itemRulesSyncPending = true;
            if (IsHandleCreated && !Disposing && !IsDisposed)
            {
                BeginInvoke((MethodInvoker)SyncChildrenFromRules);
            }
        }

        private void SyncChildrenFromRules()
        {
            if (_isSyncingItemRules)
            {
                return;
            }

            _itemRulesSyncPending = false;

            if (!IsInDesignMode())
            {
                return;
            }

            _isSyncingItemRules = true;
            try
            {
                // Repair paste-orphaned rules (GUID link severed, name still matches) before we act
                // on them, so IsSpring/Width etc. bind to the right child.
                ReconcileOrphanedRuleIds();

                foreach (AdaptiveRowItemRule rule in _itemRules.ToArray())
                {
                    if (!IsSupportedItemRule(rule))
                    {
                        continue;
                    }

                    string controlName = rule.ControlName.Trim();
                    if (!IsValidComponentName(controlName))
                    {
                        continue;
                    }

                    Control? child = FindChildForRule(rule);

                    // A rule can name a control that exists on the form but is NOT a child of
                    // this panel yet — controls mid drag/reparent during a layout swap, or a
                    // non-ARP control deleted and recreated. Materializing here would spawn a
                    // duplicate alongside the real control. Skip this pass: OnControlAdded adopts
                    // the real control when it lands, and the rule matches on the next sync.
                    if (child == null && !IsDesignerComponentNameAvailable(controlName))
                    {
                        continue;
                    }

                    // Only rules freshly added in the collection editor may create a child.
                    // Any other unmatched rule is left alone: deleting a child now removes
                    // its rule immediately in RemoveExternalChildFromItemRules, so a rule
                    // without a child here is transient (mid drag/reparent, or load order)
                    // and must not be pruned or materialized.
                    if (child == null && !_pendingItemRuleChildCreates.Contains(rule))
                    {
                        continue;
                    }

                    if (child != null && !RuleMatchesExistingChild(rule.ControlType, child))
                    {
                        // An authored control already exists under this name, but the rule's
                        // declared type disagrees — this happens transiently during layout swaps
                        // and reparents. NEVER destroy the authored control to satisfy the rule:
                        // recreating it rebuilds from defaults and silently wipes every authored
                        // property (LabelSide, colours, sizes...). The live control is the source
                        // of truth — bend the rule to match it instead. To deliberately change a
                        // control's type, delete it and add the replacement.
                        rule.ControlType = GetControlTypeId(child);
                    }

                    bool createdChild = child == null;
                    child ??= CreateDesignerChild(rule);
                    _pendingItemRuleChildCreates.Remove(rule);
                    // If the designer host assigned a different name (conflict resolved at creation time),
                    // update the rule so the Designer.cs and rule stay in sync.
                    string resolvedName = child.Name;
                    if (!string.Equals(controlName, resolvedName, StringComparison.Ordinal) && IsValidComponentName(resolvedName))
                    {
                        rule.ControlName = resolvedName;
                        SyncRuleTextToResolvedName(rule);
                    }
                    ApplyRuleToChild(rule, child, createdChild);
                }

                RemovePendingItemRuleChildren();
                ApplyItemRuleOrderToChildren();
                ApplyItemRuleSpringState();
                CaptureCurrentLayoutAsBaseline();
                ForceRefreshLayout();
            }
            finally
            {
                _isSyncingItemRules = false;
            }
        }

        internal void QueueItemRuleChildRemoval(AdaptiveRowItemRule rule)
        {
            if (!IsSupportedItemRule(rule))
            {
                return;
            }

            string key = GetRuleKey(rule);
            if (!string.IsNullOrWhiteSpace(key))
            {
                _pendingItemRuleChildRemovals.Add(key);
            }

            RequestItemRulesSync();
        }

        private void RemovePendingItemRuleChildren()
        {
            if (_pendingItemRuleChildRemovals.Count == 0)
            {
                return;
            }

            HashSet<string> activeRuleKeys = _itemRules
                .SelectMany(GetRuleKeys)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.Ordinal);

            foreach (string key in _pendingItemRuleChildRemovals.ToArray())
            {
                if (activeRuleKeys.Contains(key))
                {
                    continue;
                }

                if (FindChildByRuleKey(key) is Control child)
                {
                    DestroyDesignerChild(child);
                }
            }

            _pendingItemRuleChildRemovals.Clear();
        }

        private static bool IsSupportedItemRule(AdaptiveRowItemRule rule)
        {
            return IsSupportedItemRuleType(rule.ControlType);
        }

        private static bool IsSupportedItemRuleType(AdaptiveRowPanelAddControlType controlType)
        {
            return true;
        }

        private static string GetDefaultNamePrefix(AdaptiveRowPanelAddControlType controlType)
        {
            return controlType switch
            {
                AdaptiveRowPanelAddControlType.Spacer => "spacer",
                AdaptiveRowPanelAddControlType.HeaderButton => "headerButton",
                AdaptiveRowPanelAddControlType.IconButton => "iconButton",
                AdaptiveRowPanelAddControlType.Label => "label",
                AdaptiveRowPanelAddControlType.TextBox => "textBox",
                AdaptiveRowPanelAddControlType.ComboBox => "comboBox",
                AdaptiveRowPanelAddControlType.DropdownButton => "dropdownButton",
                AdaptiveRowPanelAddControlType.MultiSelectComboBox => "multiSelectComboBox",
                AdaptiveRowPanelAddControlType.NumericTextBox => "numericTextBox",
                AdaptiveRowPanelAddControlType.CheckBox => "checkBox",
                AdaptiveRowPanelAddControlType.ToggleSwitch => "toggleSwitch",
                AdaptiveRowPanelAddControlType.TrackBar => "trackBar",
                AdaptiveRowPanelAddControlType.LineBreak => "lineBreak",
                AdaptiveRowPanelAddControlType.ProgressBar => "progressBar",
                AdaptiveRowPanelAddControlType.ListView => "listView",
                AdaptiveRowPanelAddControlType.PictureBox => "pictureBox",
                AdaptiveRowPanelAddControlType.DateTimePicker => "dateTimePicker",
                _ => "button"
            };
        }

        private static string GetDefaultTextPrefix(AdaptiveRowPanelAddControlType controlType)
        {
            return controlType switch
            {
                AdaptiveRowPanelAddControlType.Spacer => "Spacer",
                AdaptiveRowPanelAddControlType.HeaderButton => "Header",
                AdaptiveRowPanelAddControlType.Label => "Label",
                AdaptiveRowPanelAddControlType.TextBox => "Text",
                AdaptiveRowPanelAddControlType.ComboBox => "Combo",
                AdaptiveRowPanelAddControlType.DropdownButton => "Dropdown",
                AdaptiveRowPanelAddControlType.MultiSelectComboBox => "Multi Select",
                AdaptiveRowPanelAddControlType.NumericTextBox => "Number",
                AdaptiveRowPanelAddControlType.CheckBox => "Check",
                AdaptiveRowPanelAddControlType.ToggleSwitch => "Toggle",
                AdaptiveRowPanelAddControlType.TrackBar => "Track",
                AdaptiveRowPanelAddControlType.LineBreak => "Line Break",
                AdaptiveRowPanelAddControlType.ProgressBar => "Progress",
                AdaptiveRowPanelAddControlType.ListView => "List View",
                AdaptiveRowPanelAddControlType.PictureBox => "Picture",
                AdaptiveRowPanelAddControlType.DateTimePicker => "Date",
                _ => "Button"
            };
        }

        private static bool UsesBlankDefaultText(AdaptiveRowPanelAddControlType controlType)
        {
            return controlType is AdaptiveRowPanelAddControlType.IconButton
                or AdaptiveRowPanelAddControlType.TextBox
                or AdaptiveRowPanelAddControlType.NumericTextBox
                or AdaptiveRowPanelAddControlType.ComboBox
                or AdaptiveRowPanelAddControlType.DropdownButton
                or AdaptiveRowPanelAddControlType.MultiSelectComboBox
                or AdaptiveRowPanelAddControlType.ProgressBar
                or AdaptiveRowPanelAddControlType.ListView
                or AdaptiveRowPanelAddControlType.PictureBox
                or AdaptiveRowPanelAddControlType.DateTimePicker;
        }

        // When a rule's ControlName is resolved/bumped (conflict avoidance or host-assigned),
        // keep the visible Text aligned with the final name for caption-bearing control types.
        // Blank-default types (textbox/numeric/combo/etc.) stay blank, and any Text the user
        // has customised away from the system default is left untouched.
        private void SyncRuleTextToResolvedName(AdaptiveRowItemRule rule)
        {
            if (UsesBlankDefaultText(rule.ControlType) || string.IsNullOrWhiteSpace(rule.ControlName))
            {
                return;
            }

            if (IsSystemDefaultRuleText(rule))
            {
                rule.Text = rule.ControlName;
            }
        }

        // True when rule.Text still looks system-generated rather than user-authored:
        // empty, the literal control-name pattern (any prefix + digits), or the friendly
        // "{TextPrefix} {n}" default this control type would have been given.
        private bool IsSystemDefaultRuleText(AdaptiveRowItemRule rule)
        {
            string text = rule.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return true;
            }

            if (string.Equals(text, rule.ControlName.Trim(), StringComparison.Ordinal))
            {
                return true;
            }

            string namePrefix = GetDefaultNamePrefix(rule.ControlType);
            string textPrefix = GetDefaultTextPrefix(rule.ControlType);
            return MatchesPrefixWithTrailingNumber(text, namePrefix)
                || MatchesPrefixWithTrailingNumber(text, $"{textPrefix} ");
        }

        private static bool MatchesPrefixWithTrailingNumber(string value, string prefix)
        {
            if (prefix.Length == 0 || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string remainder = value.Substring(prefix.Length);
            return remainder.Length > 0 && remainder.All(char.IsDigit);
        }

        private int GetNextItemRuleIndex(string namePrefix, AdaptiveRowItemRule currentRule)
        {
            HashSet<string> usedNames = GetDesignerComponentNames();

            foreach (string name in Controls
                .Cast<Control>()
                .Select(child => child.Name)
                .Concat(_itemRules
                    .Where(rule => !ReferenceEquals(rule, currentRule))
                    .Select(rule => rule.ControlName))
                .Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                usedNames.Add(name);
            }

            for (int index = 1; index < 10000; index++)
            {
                if (!usedNames.Contains($"{namePrefix}{index}"))
                {
                    return index;
                }
            }

            return Controls.Count + _itemRules.Count + 1;
        }

        private string GetNextItemRuleName(string namePrefix, AdaptiveRowItemRule currentRule)
        {
            return $"{namePrefix}{GetNextItemRuleIndex(namePrefix, currentRule)}";
        }

        private HashSet<string> GetDesignerComponentNames()
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            if (designerHost?.Container == null)
            {
                return names;
            }

            foreach (IComponent component in designerHost.Container.Components)
            {
                string? componentName = component.Site?.Name;
                if (!string.IsNullOrWhiteSpace(componentName))
                {
                    names.Add(componentName);
                    continue;
                }

                if (component is Control control && !string.IsNullOrWhiteSpace(control.Name))
                {
                    names.Add(control.Name);
                }
            }

            if (designerHost.RootComponent is Control rootControl)
            {
                AddControlTreeNames(rootControl, names);
            }

            return names;
        }

        private bool IsDesignerComponentNameAvailable(string componentName, IComponent? allowedComponent = null)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            if (designerHost?.Container == null)
            {
                return true;
            }

            // When not checking a rename, delegate to INameCreationService — it is the same
            // validator that CreateComponent uses internally, so our pre-check and the actual
            // creation can never disagree.
            if (allowedComponent == null)
            {
                INameCreationService? nameService = designerHost.GetService(typeof(INameCreationService)) as INameCreationService;
                if (nameService != null)
                {
                    return nameService.IsValidName(componentName);
                }
            }

            foreach (IComponent component in designerHost.Container.Components)
            {
                if (ReferenceEquals(component, allowedComponent))
                {
                    continue;
                }

                string? existingName = component.Site?.Name;
                if (string.IsNullOrWhiteSpace(existingName) && component is Control control)
                {
                    existingName = control.Name;
                }

                if (string.Equals(existingName, componentName, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (designerHost.RootComponent is Control rootControl
                && ControlTreeContainsName(rootControl, componentName, allowedComponent))
            {
                return false;
            }

            return true;
        }

        private static void AddControlTreeNames(Control root, HashSet<string> names)
        {
            if (!string.IsNullOrWhiteSpace(root.Name))
            {
                names.Add(root.Name);
            }

            foreach (Control child in root.Controls)
            {
                AddControlTreeNames(child, names);
            }
        }

        private static bool ControlTreeContainsName(Control root, string componentName, IComponent? allowedComponent)
        {
            if (!ReferenceEquals(root, allowedComponent)
                && string.Equals(root.Name, componentName, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (Control child in root.Controls)
            {
                if (ControlTreeContainsName(child, componentName, allowedComponent))
                {
                    return true;
                }
            }

            return false;
        }

        internal void RemoveItemRuleChild(AdaptiveRowItemRule rule)
        {
            if (!IsInDesignMode() || _isSyncingItemRules || !IsSupportedItemRule(rule))
            {
                return;
            }

            if (FindChildForRule(rule) is not Control child)
            {
                return;
            }

            if (rule.IsSpring)
            {
                SpringControlTabIndex = -1;
            }

            DestroyDesignerChild(child);
            CaptureCurrentLayoutAsBaseline();
            ForceRefreshLayout();
        }

        private Control CreateDesignerChild(AdaptiveRowItemRule rule)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];
            AdaptiveRowPanelAddControlType controlType = rule.ControlType;
            string controlName = rule.ControlName.Trim();
            Type childType = GetControlType(controlType);

            Control? child = null;
            if (designerHost != null)
            {
                try
                {
                    child = designerHost.CreateComponent(childType, controlName) as Control;
                }
                catch
                {
                    // Name was rejected by the designer naming service; let the host assign a unique name.
                    try { child = designerHost.CreateComponent(childType) as Control; }
                    catch { child = null; }
                }
            }

            if (child == null)
            {
                child = (Control)Activator.CreateInstance(childType)!;
                child.Name = controlName;
            }
            // When created via designerHost the component is already sited with the correct name —
            // do not set child.Name here, as that would call Site.Name and re-enter the naming service.

            ConfigureNewRuleChild(child, controlType);
            RestoreModernButtonAppearance(child);
            if (!string.IsNullOrWhiteSpace(rule.Id) && CanAssignRuleTag(child))
            {
                TrySetChildRuleTag(child, rule.Id);
            }

            changeService?.OnComponentChanging(this, controlsProperty);
            Controls.Add(child);
            changeService?.OnComponentChanged(this, controlsProperty, null, child);

            return child;
        }

        private Control ReplaceDesignerChild(Control oldChild, AdaptiveRowItemRule rule)
        {
            int tabIndex = oldChild.TabIndex;
            Padding margin = oldChild.Margin;
            DestroyDesignerChild(oldChild);

            Control newChild = CreateDesignerChild(rule);
            newChild.TabIndex = tabIndex;
            newChild.Margin = margin;
            return newChild;
        }

        private void DestroyDesignerChild(Control child)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];

            CaptureModernButtonAppearance(child);
            _suppressChildRulePrune++;
            try
            {
                changeService?.OnComponentChanging(this, controlsProperty);
                Controls.Remove(child);
                if (designerHost != null)
                {
                    designerHost.DestroyComponent(child);
                }
                else
                {
                    child.Dispose();
                }
                changeService?.OnComponentChanged(this, controlsProperty, child, null);
            }
            finally
            {
                _suppressChildRulePrune--;
            }
        }

        private void CaptureModernButtonAppearance(Control child)
        {
            if (child is ModernButton button && IsValidComponentName(button.Name))
            {
                _modernButtonAppearanceSnapshots[button.Name] = ModernButtonAppearanceSnapshot.From(button);
            }
        }

        private void RestoreModernButtonAppearance(Control child)
        {
            if (child is ModernButton button
                && IsValidComponentName(button.Name)
                && _modernButtonAppearanceSnapshots.Remove(button.Name, out ModernButtonAppearanceSnapshot snapshot))
            {
                snapshot.ApplyTo(button);
            }
        }

        private void RenameDesignerChild(Control child, string newControlName)
        {
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? nameProperty = TypeDescriptor.GetProperties(child)[nameof(Control.Name)];
            string oldName = child.Name;

            changeService?.OnComponentChanging(child, nameProperty);
            if (nameProperty != null)
            {
                nameProperty.SetValue(child, newControlName);
            }
            else
            {
                child.Name = newControlName;
            }
            changeService?.OnComponentChanged(child, nameProperty, oldName, newControlName);
        }

        private Control? FindChild(string controlName)
        {
            return Controls
                .Cast<Control>()
                .FirstOrDefault(child => string.Equals(child.Name, controlName, StringComparison.Ordinal));
        }

        private AdaptiveRowItemRule? FindRuleForChild(Control child)
        {
            string childId = GetChildRuleId(child);
            if (!string.IsNullOrWhiteSpace(childId))
            {
                AdaptiveRowItemRule? byId = _itemRules.FirstOrDefault(rule =>
                    string.Equals(rule.Id, childId, StringComparison.Ordinal));
                if (byId != null)
                {
                    return byId;
                }
            }

            if (string.IsNullOrWhiteSpace(child.Name))
            {
                return null;
            }

            return _itemRules.FirstOrDefault(rule =>
                string.Equals(rule.ControlName, child.Name, StringComparison.Ordinal));
        }

        // Repairs rules whose GUID link to their child was severed — e.g. pasting a whole ARP into
        // another form re-tags the child but the rule keeps its old Id, so FindChildForRule (which
        // pairs rule.Id == child Tag) can no longer match them and IsSpring/Width silently do nothing.
        // Heals ONLY the unambiguous case: a rule that resolves to no child by Id, whose ControlName
        // matches exactly one live child, whose Tag is claimed by no other rule. Adopts that child's
        // Tag into the rule so the pair matches again. Ambiguous cases are left untouched, so it can
        // never wrongly merge two genuinely different controls.
        private void ReconcileOrphanedRuleIds()
        {
            Control[] children = Controls.Cast<Control>().ToArray();

            foreach (AdaptiveRowItemRule rule in _itemRules.ToArray())
            {
                // Already linked by Id → nothing to fix.
                if (!string.IsNullOrWhiteSpace(rule.Id) &&
                    children.Any(c => string.Equals(GetChildRuleId(c), rule.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                string name = rule.ControlName?.Trim() ?? string.Empty;
                if (!IsValidComponentName(name))
                {
                    continue;
                }

                // Exactly one live child carries this name.
                Control[] byName = children
                    .Where(c => string.Equals(c.Name, name, StringComparison.Ordinal))
                    .ToArray();
                if (byName.Length != 1)
                {
                    continue;
                }

                string childId = GetChildRuleId(byName[0]);
                if (!IsPrefixedItemRuleId(childId))
                {
                    continue;   // child has no usable tag → leave it to the normal adoption path
                }

                // That child's tag must not already belong to a different rule (otherwise ambiguous).
                if (_itemRules.Any(r => !ReferenceEquals(r, rule) &&
                                        string.Equals(r.Id, childId, StringComparison.Ordinal)))
                {
                    continue;
                }

                // Unambiguous paste-orphan → re-pair by adopting the child's tag.
                if (!string.Equals(rule.Id, childId, StringComparison.Ordinal))
                {
                    rule.Id = childId;
                }
            }
        }

        private Control? FindChildForRule(AdaptiveRowItemRule rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.Id))
            {
                Control? byId = Controls
                    .Cast<Control>()
                    .FirstOrDefault(child => string.Equals(GetChildRuleId(child), rule.Id, StringComparison.Ordinal));
                if (byId != null)
                {
                    return byId;
                }
            }

            string controlName = rule.ControlName.Trim();
            Control? byName = IsValidComponentName(controlName) ? FindChild(controlName) : null;
            if (byName == null)
            {
                return null;
            }

            string childId = GetChildRuleId(byName);
            if (!string.IsNullOrWhiteSpace(rule.Id))
            {
                if (string.IsNullOrWhiteSpace(childId))
                {
                    return byName;
                }

                return string.Equals(childId, rule.Id, StringComparison.Ordinal) ? byName : null;
            }

            if (IsPrefixedItemRuleId(childId))
            {
                rule.Id = childId;
            }

            return byName;
        }

        private Control? FindChildByRuleKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            Control? byId = Controls
                .Cast<Control>()
                .FirstOrDefault(child => string.Equals(GetChildRuleId(child), key, StringComparison.Ordinal));
            if (byId != null)
            {
                return byId;
            }

            return IsValidComponentName(key) ? FindChild(key) : null;
        }

        private string GetRuleKey(AdaptiveRowItemRule rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.Id))
            {
                return rule.Id;
            }

            string controlName = rule.ControlName.Trim();
            return IsValidComponentName(controlName) ? controlName : string.Empty;
        }

        private static IEnumerable<string> GetRuleKeys(AdaptiveRowItemRule rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.Id))
            {
                yield return rule.Id;
            }

            string controlName = rule.ControlName.Trim();
            if (IsValidComponentName(controlName))
            {
                yield return controlName;
            }
        }

        private static string GetChildRuleId(Control child)
        {
            return child.Tag as string ?? string.Empty;
        }

        private static string CreateItemRuleId()
        {
            return $"{ItemRuleTagPrefix}{Guid.NewGuid():N}";
        }

        private static bool IsPrefixedItemRuleId(string value)
        {
            return value.StartsWith(ItemRuleTagPrefix, StringComparison.Ordinal);
        }

        private static bool IsBareDesignerGuid(string value)
        {
            return Guid.TryParseExact(value, "N", out _)
                || Guid.TryParseExact(value, "D", out _);
        }

        private static bool CanAssignRuleTag(Control child)
        {
            return child.Tag == null
                || (child.Tag is string value && string.IsNullOrWhiteSpace(value));
        }

        private void SyncRuleIdFromChild(AdaptiveRowItemRule rule, Control child)
        {
            string childId = GetChildRuleId(child);
            if (string.IsNullOrWhiteSpace(rule.Id) && IsPrefixedItemRuleId(childId))
            {
                rule.Id = childId;
            }
        }

        private bool TrySetChildRuleTag(Control child, string id, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            string current = child.Tag as string ?? string.Empty;
            if (string.Equals(current, id, StringComparison.Ordinal))
            {
                return true;
            }

            if (!overwrite && !CanAssignRuleTag(child))
            {
                return false;
            }

            // Deliberately a plain assignment. PropertyDescriptor.SetValue (or explicit
            // OnComponentChanging/Changed) injects the child into the designer's open undo
            // unit mid-drag, snapshotting it at the drop location — which made undoing a
            // drag-in restore the control to the wrong place. The Designer.cs still picks
            // the Tag up at flush time from the live value.
            child.Tag = id;
            return true;
        }

        // Dragged-in controls join the end of the row: +2 past the highest existing
        // TabIndex, matching the 0,2,4... scheme used by ResequenceTabIndexes.
        private int GetNextChildTabIndex(Control child)
        {
            int maxTabIndex = -2;
            foreach (Control existing in Controls)
            {
                if (!ReferenceEquals(existing, child) && existing.TabIndex > maxTabIndex)
                {
                    maxTabIndex = existing.TabIndex;
                }
            }

            return maxTabIndex + 2;
        }

        private void SetChildTabIndex(Control child, int tabIndex)
        {
            // Plain assignment on purpose — see TrySetChildRuleTag for why announcing
            // child changes mid-drag corrupts the undo unit's location snapshot.
            child.TabIndex = tabIndex;
        }

        internal int AssignMissingItemRuleIds()
        {
            if (!IsInDesignMode())
            {
                return 0;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? itemRulesProperty = TypeDescriptor.GetProperties(this)[nameof(ItemRules)];
            int changed = 0;
            bool rulesChanging = false;

            void EnsureRulesChanging()
            {
                if (rulesChanging)
                {
                    return;
                }

                changeService?.OnComponentChanging(this, itemRulesProperty);
                rulesChanging = true;
            }

            _isSyncingItemRules = true;
            try
            {
                foreach (AdaptiveRowItemRule rule in _itemRules.ToArray())
                {
                    if (!IsSupportedItemRule(rule))
                    {
                        continue;
                    }

                    Control? child = FindChildForRule(rule);
                    if (child == null)
                    {
                        string controlName = rule.ControlName.Trim();
                        child = IsValidComponentName(controlName) ? FindChild(controlName) : null;
                    }

                    if (child == null)
                    {
                        continue;
                    }

                    string childId = GetChildRuleId(child);
                    string usableChildId = IsPrefixedItemRuleId(childId) || IsBareDesignerGuid(childId)
                        ? childId
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(rule.Id))
                    {
                        if (!string.IsNullOrWhiteSpace(usableChildId))
                        {
                            EnsureRulesChanging();
                            rule.Id = usableChildId;
                            changed++;
                            continue;
                        }

                        if (CanAssignRuleTag(child))
                        {
                            string newId = CreateItemRuleId();
                            EnsureRulesChanging();
                            rule.Id = newId;
                            changed++;
                            TrySetChildRuleTag(child, newId);
                        }

                        continue;
                    }

                    if (string.Equals(childId, rule.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (CanAssignRuleTag(child) && TrySetChildRuleTag(child, rule.Id))
                    {
                        changed++;
                    }
                }
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            if (changed > 0)
            {
                if (rulesChanging)
                {
                    changeService?.OnComponentChanged(this, itemRulesProperty, null, _itemRules);
                }
                CaptureCurrentLayoutAsBaseline();
                ForceRefreshLayout();
            }

            return changed;
        }

        private void ApplyRuleToChild(AdaptiveRowItemRule rule, Control child, bool createdFromRule = false)
        {
            SyncRuleIdFromChild(rule, child);

            if (ShouldApplyRuleTextToChild(child))
            {
                if (!createdFromRule && IsSystemDefaultRuleText(rule))
                {
                    rule.Text = child.Text ?? string.Empty;
                }
                else
                {
                    child.Text = rule.Text;
                }
            }

            if (rule.Width > 0)
            {
                child.Width = rule.Width;
            }

            Padding margin = child.Margin;
            child.Margin = new Padding(
                Math.Max(0, rule.MarginLeft),
                margin.Top,
                Math.Max(0, rule.MarginRight),
                margin.Bottom);
        }

        private static bool ShouldApplyRuleTextToChild(Control child)
        {
            return child is AdaptiveRowLabel
                or ModernButton
                or HeaderButton
                or RoundedCheckBox
                or RoundedToggleSwitch;
        }

        private static bool IsItemRuleManagedChild(Control child)
        {
            return child is AdaptiveRowSpacer
                or AdaptiveRowLabel
                or HeaderButton
                or ModernButton
                or RoundedNumericTextBox
                or RoundedTextBox
                or RoundedMultiSelectComboBox
                or RoundedComboBox
                or RoundedCheckBox
                or RoundedToggleSwitch
                or RoundedTrackBar
                or AdaptiveLineBreak
                or RoundedProgressBar
                or ListView
                or DateTimePicker;
        }

        private void ApplyItemRuleOrderToChildren()
        {
            List<Control> orderedControls = _itemRules
                .Select(FindChildForRule)
                .Where(child => child != null)
                .Cast<Control>()
                .Distinct()
                .ToList();

            orderedControls.AddRange(GetOrderedChildren()
                .Where(child => !orderedControls.Contains(child)));

            ApplyDesignerOrder(orderedControls);
        }

        // Rules are the design-time source of truth for springs: every IsSpring rule with a live
        // child contributes its NAME to Spring (multi-spring supported). The legacy TabIndex value
        // is retired on the same sync, so forms migrate themselves as they get re-saved.
        private void ApplyItemRuleSpringState()
        {
            List<string> springNames = new();
            foreach (AdaptiveRowItemRule rule in _itemRules)
            {
                if (!rule.IsSpring || !IsSupportedItemRule(rule))
                {
                    continue;
                }

                if (FindChildForRule(rule) is not Control child || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                springNames.Add(child.Name);
            }

            Spring = springNames.Count == 0 ? null : string.Join(",", springNames);
            SpringControlTabIndex = -1;
        }

        private void ConfigureNewRuleChild(Control child, AdaptiveRowPanelAddControlType controlType)
        {
            ApplyStandardFont(child);
            child.Margin = new Padding(_defaultChildHorizontalMargin, 3, _defaultChildHorizontalMargin, 3);
            child.TabIndex = Math.Max(0, Controls.Count * 2);
            child.Height = Math.Max(18, Height - Padding.Top - Padding.Bottom);

            switch (child)
            {
                case AdaptiveRowSpacer spacer:
                    spacer.Size = new Size(80, Math.Max(18, child.Height));
                    spacer.Margin = new Padding(_defaultChildHorizontalMargin, 0, _defaultChildHorizontalMargin, 0);
                    break;
                case AdaptiveRowLabel label:
                    label.Text = "Label";
                    label.LabelHeight = Math.Max(18, child.Height);
                    label.Size = new Size(80, label.LabelHeight);
                    break;
                case HeaderButton headerButton:
                    headerButton.Text = "Header";
                    headerButton.Size = new Size(120, child.Height);
                    break;
                case ModernButton button:
                    button.Text = "Button";
                    button.Size = new Size(100, Math.Max(18, child.Height));
                    break;
                case IconButton iconButton:
                    iconButton.Size = new Size(44, Math.Max(18, child.Height));
                    break;
                case RoundedComboBox comboBox:
                    comboBox.Size = new Size(140, Math.Max(18, child.Height));
                    break;
                case RoundedMultiSelectComboBox multiSelectComboBox:
                    multiSelectComboBox.PlaceholderText = "Search Engine";
                    multiSelectComboBox.Size = new Size(140, Math.Max(18, child.Height));
                    break;
                case RoundedNumericTextBox numericTextBox:
                    numericTextBox.Size = new Size(120, Math.Max(18, child.Height));
                    break;
                case RoundedTextBox textBox:
                    textBox.PlaceholderText = "Text...";
                    textBox.Size = new Size(160, Math.Max(18, child.Height));
                    break;
                case RoundedCheckBox checkBox:
                    checkBox.Text = "CheckBox";
                    checkBox.Size = new Size(140, Math.Max(18, child.Height));
                    break;
                case RoundedToggleSwitch toggle:
                    toggle.Text = "Toggle";
                    toggle.Size = new Size(120, Math.Max(18, child.Height));
                    break;
                case RoundedTrackBar trackBar:
                    trackBar.Size = new Size(180, Math.Max(34, child.Height));
                    break;
                case AdaptiveLineBreak lineBreak:
                    lineBreak.Size = new Size(10, Math.Max(18, child.Height));
                    lineBreak.Margin = new Padding(2, 0, 2, 0);
                    break;
                case RoundedProgressBar progressBar:
                    progressBar.Size = new Size(200, 12);
                    progressBar.Margin = new Padding(_defaultChildHorizontalMargin, 0, _defaultChildHorizontalMargin, 0);
                    break;
                case ListView listView:
                    listView.View = View.Details;
                    listView.Size = new Size(220, Math.Max(60, child.Height));
                    break;
                case AspectPictureBox pictureBox:
                    pictureBox.Size = new Size(24, 24);
                    break;
                case DateTimePicker dateTimePicker:
                    dateTimePicker.Format = DateTimePickerFormat.Custom;
                    dateTimePicker.CustomFormat = "dd/MM/yyyy  HH:mm";
                    dateTimePicker.ShowUpDown = true;
                    dateTimePicker.Size = new Size(184, Math.Max(18, child.Height));
                    break;
                default:
                    child.Size = new Size(100, Math.Max(18, child.Height));
                    break;
            }
        }

        private static Type GetControlType(AdaptiveRowPanelAddControlType controlType)
        {
            return controlType switch
            {
                AdaptiveRowPanelAddControlType.Label => typeof(AdaptiveRowLabel),
                AdaptiveRowPanelAddControlType.Button => typeof(ModernButton),
                AdaptiveRowPanelAddControlType.IconButton => typeof(IconButton),
                AdaptiveRowPanelAddControlType.TextBox => typeof(RoundedTextBox),
                AdaptiveRowPanelAddControlType.ComboBox => typeof(RoundedComboBox),
                AdaptiveRowPanelAddControlType.DropdownButton => typeof(RoundedDropdownButton),
                AdaptiveRowPanelAddControlType.MultiSelectComboBox => typeof(RoundedMultiSelectComboBox),
                AdaptiveRowPanelAddControlType.NumericTextBox => typeof(RoundedNumericTextBox),
                AdaptiveRowPanelAddControlType.CheckBox => typeof(RoundedCheckBox),
                AdaptiveRowPanelAddControlType.ToggleSwitch => typeof(RoundedToggleSwitch),
                AdaptiveRowPanelAddControlType.TrackBar => typeof(RoundedTrackBar),
                AdaptiveRowPanelAddControlType.LineBreak => typeof(AdaptiveLineBreak),
                AdaptiveRowPanelAddControlType.HeaderButton => typeof(HeaderButton),
                AdaptiveRowPanelAddControlType.ProgressBar => typeof(RoundedProgressBar),
                AdaptiveRowPanelAddControlType.ListView => typeof(ListView),
                AdaptiveRowPanelAddControlType.PictureBox => typeof(AspectPictureBox),
                AdaptiveRowPanelAddControlType.DateTimePicker => typeof(DateTimePicker),
                _ => typeof(AdaptiveRowSpacer)
            };
        }

        private static AdaptiveRowPanelAddControlType GetControlTypeId(Control child)
        {
            return child switch
            {
                AdaptiveRowLabel => AdaptiveRowPanelAddControlType.Label,
                HeaderButton => AdaptiveRowPanelAddControlType.HeaderButton,
                IconButton => AdaptiveRowPanelAddControlType.IconButton,
                ModernButton => AdaptiveRowPanelAddControlType.Button,
                RoundedNumericTextBox => AdaptiveRowPanelAddControlType.NumericTextBox,
                RoundedTextBox => AdaptiveRowPanelAddControlType.TextBox,
                RoundedMultiSelectComboBox => AdaptiveRowPanelAddControlType.MultiSelectComboBox,
                // MUST precede RoundedComboBox — RoundedDropdownButton derives from it, so the
                // base-type arm would swallow it and every dropdown button would type as a combo.
                RoundedDropdownButton => AdaptiveRowPanelAddControlType.DropdownButton,
                RoundedComboBox => AdaptiveRowPanelAddControlType.ComboBox,
                RoundedCheckBox => AdaptiveRowPanelAddControlType.CheckBox,
                RoundedToggleSwitch => AdaptiveRowPanelAddControlType.ToggleSwitch,
                RoundedTrackBar => AdaptiveRowPanelAddControlType.TrackBar,
                AdaptiveLineBreak => AdaptiveRowPanelAddControlType.LineBreak,
                RoundedProgressBar => AdaptiveRowPanelAddControlType.ProgressBar,
                AspectPictureBox => AdaptiveRowPanelAddControlType.PictureBox,
                ListView => AdaptiveRowPanelAddControlType.ListView,
                DateTimePicker => AdaptiveRowPanelAddControlType.DateTimePicker,
                _ => AdaptiveRowPanelAddControlType.Spacer
            };
        }

        // True when the existing child already satisfies the rule's requested type.
        // Uses the same subclass-aware identification as GetControlTypeId (so a
        // HeaderButton child is not mistaken for a plain Button, etc.).
        private static bool RuleMatchesExistingChild(AdaptiveRowPanelAddControlType ruleType, Control child)
        {
            AdaptiveRowPanelAddControlType childType = GetControlTypeId(child);
            if (childType == ruleType)
            {
                return true;
            }

            // Legacy compatibility: RoundedDropdownButton derives from RoundedComboBox, so before
            // the DropdownButton type existed every dropdown button was serialised as ComboBox.
            // Those older designer files must still count as a match — otherwise the panel decides
            // the rule and child disagree and REPLACES the live control with a plain combo box,
            // losing its images and size.
            return ruleType == AdaptiveRowPanelAddControlType.ComboBox
                && childType == AdaptiveRowPanelAddControlType.DropdownButton;
        }

        private static void ApplyStandardFont(Control child)
        {
            FontStyle style = child.Font?.Style ?? FontStyle.Regular;
            child.Font = new Font("Segoe UI", 10F, style, GraphicsUnit.Point);
        }

        private static bool IsValidComponentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            {
                return false;
            }

            if (!(char.IsLetter(name[0]) || name[0] == '_'))
            {
                return false;
            }

            return name.All(c => char.IsLetterOrDigit(c) || c == '_');
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdaptiveRowItemRule : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _controlName = string.Empty;
        private AdaptiveRowPanelAddControlType _controlType = AdaptiveRowPanelAddControlType.Spacer;
        private string _text = string.Empty;
        private int _width = 100;
        private int _marginLeft = 4;
        private int _marginRight = 4;
        private bool _isSpring;

        public event PropertyChangedEventHandler? PropertyChanged;

        [DefaultValue("")]
        [Category("Control")]
        [Description("Stable design-time id for matching this rule to its child control. New ARP ids are stored in the child Tag as arp:<guid>.")]
        public string Id
        {
            get => _id;
            set => SetField(ref _id, value ?? string.Empty, nameof(Id));
        }

        [Category("Control")]
        [Description("Name of the child control this rule describes.")]
        public string ControlName
        {
            get => _controlName;
            set
            {
                string newValue = value ?? string.Empty;
                if (string.Equals(_controlName, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                _controlName = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ControlName)));
            }
        }

        [DefaultValue(AdaptiveRowPanelAddControlType.Spacer)]
        [Category("Control")]
        [Description("Type of child control described by this rule. Changing it in the collection editor morphs the matching child.")]
        public AdaptiveRowPanelAddControlType ControlType
        {
            get => _controlType;
            set => SetField(ref _controlType, value, nameof(ControlType));
        }

        [DefaultValue("")]
        [Category("Control")]
        public string Text
        {
            get => _text;
            set => SetField(ref _text, value ?? string.Empty, nameof(Text));
        }

        [DefaultValue(100)]
        [Category("Layout")]
        public int Width
        {
            get => _width;
            set => SetField(ref _width, Math.Max(1, value), nameof(Width));
        }

        [DefaultValue(4)]
        [Category("Layout")]
        public int MarginLeft
        {
            get => _marginLeft;
            set => SetField(ref _marginLeft, Math.Max(0, value), nameof(MarginLeft));
        }

        [DefaultValue(4)]
        [Category("Layout")]
        public int MarginRight
        {
            get => _marginRight;
            set => SetField(ref _marginRight, Math.Max(0, value), nameof(MarginRight));
        }

        [DefaultValue(false)]
        [Category("Layout")]
        [Description("Makes this child the row spring control. Only one item rule can be spring.")]
        public bool IsSpring
        {
            get => _isSpring;
            set => SetField(ref _isSpring, value, nameof(IsSpring));
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(ControlName) ? "(item rule)" : ControlName;
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AdaptiveRowItemRuleCollection : Collection<AdaptiveRowItemRule>
    {
        private readonly Dictionary<AdaptiveRowItemRule, string> _knownControlNames = new();

        internal AdaptiveRowPanel? Owner { get; set; }

        protected override void InsertItem(int index, AdaptiveRowItemRule item)
        {
            Owner?.PrepareNewItemRule(item);

            if (FindRuleByControlName(item.ControlName) is AdaptiveRowItemRule existingItem)
            {
                CopyRuleValues(item, existingItem);
                NotifyChanged();
                return;
            }

            base.InsertItem(index, item);
            Subscribe(item);
            _knownControlNames[item] = item.ControlName;
            NotifyChanged();
        }

        protected override void SetItem(int index, AdaptiveRowItemRule item)
        {
            AdaptiveRowItemRule oldItem = this[index];
            Owner?.PrepareNewItemRule(item);

            if (FindRuleByControlName(item.ControlName, oldItem) is AdaptiveRowItemRule existingItem)
            {
                CopyRuleValues(item, existingItem);
                RemoveItem(index);
                NotifyChanged();
                return;
            }

            Unsubscribe(oldItem);
            _knownControlNames.Remove(oldItem);
            base.SetItem(index, item);
            Subscribe(item);
            _knownControlNames[item] = item.ControlName;
            NotifyChanged();
        }

        protected override void RemoveItem(int index)
        {
            AdaptiveRowItemRule item = this[index];
            Unsubscribe(item);
            _knownControlNames.Remove(item);
            base.RemoveItem(index);
            Owner?.QueueItemRuleChildRemoval(item);
            NotifyChanged();
        }

        protected override void ClearItems()
        {
            AdaptiveRowItemRule[] items = this.ToArray();
            foreach (AdaptiveRowItemRule item in this)
            {
                Unsubscribe(item);
            }

            _knownControlNames.Clear();
            base.ClearItems();
            foreach (AdaptiveRowItemRule item in items)
            {
                Owner?.QueueItemRuleChildRemoval(item);
            }

            NotifyChanged();
        }

        private void Subscribe(AdaptiveRowItemRule item)
        {
            item.PropertyChanged += Item_PropertyChanged;
            _knownControlNames[item] = item.ControlName;
        }

        private void Unsubscribe(AdaptiveRowItemRule item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is AdaptiveRowItemRule rule && e.PropertyName == nameof(AdaptiveRowItemRule.ControlName))
            {
                _knownControlNames.TryGetValue(rule, out string? oldControlName);
                string newControlName = rule.ControlName;
                _knownControlNames[rule] = newControlName;
                Owner?.OnItemRuleNameChanged(rule, oldControlName ?? string.Empty, newControlName);
                return;
            }

            if (sender is AdaptiveRowItemRule springRule && e.PropertyName == nameof(AdaptiveRowItemRule.IsSpring))
            {
                Owner?.OnItemRuleSpringChanged(springRule);
                return;
            }

            if (sender is AdaptiveRowItemRule typeRule && e.PropertyName == nameof(AdaptiveRowItemRule.ControlType))
            {
                Owner?.OnItemRuleControlTypeChanged(typeRule);
                return;
            }

            NotifyChanged();
        }

        private void NotifyChanged()
        {
            Owner?.OnItemRulesChanged();
        }

        private AdaptiveRowItemRule? FindRuleByControlName(string controlName, AdaptiveRowItemRule? excludedRule = null)
        {
            string normalizedName = controlName.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return null;
            }

            return this.FirstOrDefault(rule =>
                !ReferenceEquals(rule, excludedRule)
                && string.Equals(rule.ControlName.Trim(), normalizedName, StringComparison.Ordinal));
        }

        private static void CopyRuleValues(AdaptiveRowItemRule source, AdaptiveRowItemRule target)
        {
            target.Id = source.Id;
            target.ControlType = source.ControlType;
            target.Text = source.Text;
            target.Width = source.Width;
            target.MarginLeft = source.MarginLeft;
            target.MarginRight = source.MarginRight;
            target.IsSpring = source.IsSpring;
        }
    }

    internal readonly record struct ModernButtonAppearanceSnapshot(
        Color BackgroundColor,
        Color TextColor,
        Color HoverColor,
        Color PressedColor,
        Color BorderColor,
        int BorderSize,
        int BorderRadius,
        Color HoverBorderColor,
        int HoverBorderSize,
        Color PressedBorderColor,
        int PressedBorderSize)
    {
        public static ModernButtonAppearanceSnapshot From(ModernButton button)
        {
            return new ModernButtonAppearanceSnapshot(
                button.BackgroundColor,
                button.TextColor,
                button.HoverColor,
                button.PressedColor,
                button.BorderColor,
                button.BorderSize,
                button.BorderRadius,
                button.HoverBorderColor,
                button.HoverBorderSize,
                button.PressedBorderColor,
                button.PressedBorderSize);
        }

        public void ApplyTo(ModernButton button)
        {
            button.BackgroundColor = BackgroundColor;
            button.TextColor = TextColor;
            button.HoverColor = HoverColor;
            button.PressedColor = PressedColor;
            button.BorderColor = BorderColor;
            button.BorderSize = BorderSize;
            button.BorderRadius = BorderRadius;
            button.HoverBorderColor = HoverBorderColor;
            button.HoverBorderSize = HoverBorderSize;
            button.PressedBorderColor = PressedBorderColor;
            button.PressedBorderSize = PressedBorderSize;
        }
    }
}
