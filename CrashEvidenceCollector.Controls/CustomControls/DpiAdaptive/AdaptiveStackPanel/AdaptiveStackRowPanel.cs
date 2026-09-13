using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    [Designer(typeof(AdaptiveStackRowPanelDesigner))]
    public class AdaptiveStackRowPanel : Panel
    {
        private static readonly Padding DefaultRowPadding = new Padding(4, 3, 4, 3);

        private int _rowHeight = 36;
        private int _collapsedHeight;
        private int _gapTop;
        private int _gapBottom = 4;
        private bool _startsCollapsed;
        private int _itemGap = 6;
        private int _defaultChildHorizontalMargin = 4;
        private int _minimumChildWidth = 24;
        private int _springControlTabIndex = -1;
        private AdaptiveRowSpringMode _springExpands = AdaptiveRowSpringMode.Both;
        private bool _usePaddingFromItems = true;
        private bool _useTabIndexForOrder = true;
        private bool _stretchChildHeight = true;
        private AdaptiveRowContentAlignment _contentAlignment = AdaptiveRowContentAlignment.Left;
        private bool _debugLayout;
        private readonly Dictionary<Control, int> _baseWidths = new();
        private readonly Dictionary<Control, int> _baseLefts = new();
        private readonly Dictionary<Control, float> _baseFontSizes = new();
        private bool _isApplyingLayout;
        private string _lastLayoutReport = string.Empty;
        private readonly AdaptiveStackRowItemRuleCollection _itemRules = new();
        private bool _isSyncingItemRules;
        private bool _itemRulesSyncPending;
        private readonly HashSet<string> _pendingItemRuleChildRemovals = new(StringComparer.Ordinal);
        private AdaptiveRowItemRule? _preferredSpringRule;

        public event EventHandler? LayoutReportChanged;

        // The owning AdaptiveStackPanel computes every row's position via LayoutRows, so the row's
        // pixel Location is never authoritative. Suppressing its serialization (the way FlowLayoutPanel
        // / TableLayoutPanel children behave) stops stale saved Y positions in the .Designer.cs from
        // fighting RowOrder and painting the rows in their old order on the designer canvas.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Point Location
        {
            get => base.Location;
            set => base.Location = value;
        }

        public AdaptiveStackRowPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = false;
            Padding = DefaultRowPadding;
            Size = new Size(330, 36);
            _itemRules.Owner = this;
        }

        private bool ShouldSerializePadding()
        {
            return Padding != DefaultRowPadding;
        }

        private void ResetPadding()
        {
            Padding = DefaultRowPadding;
        }

        [DefaultValue(36)]
        [Category("Adaptive Stack Row")]
        public int RowHeight
        {
            get => _rowHeight;
            set
            {
                int newValue = Math.Max(0, value);
                if (_rowHeight == newValue)
                {
                    return;
                }

                _rowHeight = newValue;
                Height = newValue;
                NotifyHostLayout();
            }
        }

        [DefaultValue(0)]
        [Category("Adaptive Stack Row")]
        [Description("Height used while this row is collapsed. Use 0 to hide the row completely.")]
        public int CollapsedHeight
        {
            get => _collapsedHeight;
            set
            {
                int newValue = Math.Max(0, value);
                if (_collapsedHeight == newValue)
                {
                    return;
                }

                _collapsedHeight = newValue;
                NotifyHostLayout();
            }
        }

        [DefaultValue(0)]
        [Category("Adaptive Stack Row")]
        public int GapTop
        {
            get => _gapTop;
            set
            {
                int newValue = Math.Max(0, value);
                if (_gapTop == newValue)
                {
                    return;
                }

                _gapTop = newValue;
                NotifyHostLayout();
            }
        }

        [DefaultValue(4)]
        [Category("Adaptive Stack Row")]
        public int GapBottom
        {
            get => _gapBottom;
            set
            {
                int newValue = Math.Max(0, value);
                if (_gapBottom == newValue)
                {
                    return;
                }

                _gapBottom = newValue;
                NotifyHostLayout();
            }
        }

        [DefaultValue(false)]
        [Category("Adaptive Stack Row")]
        public bool StartsCollapsed
        {
            get => _startsCollapsed;
            set
            {
                if (_startsCollapsed == value)
                {
                    return;
                }

                _startsCollapsed = value;
                NotifyHostLayout();
            }
        }

        [DefaultValue(6)]
        [Category("Adaptive Stack Row")]
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
        [Category("Adaptive Stack Row")]
        public int DefaultChildHorizontalMargin
        {
            get => _defaultChildHorizontalMargin;
            set => _defaultChildHorizontalMargin = Math.Max(0, value);
        }

        [DefaultValue(24)]
        [Category("Adaptive Stack Row")]
        public int MinimumChildWidth
        {
            get => _minimumChildWidth;
            set
            {
                int newValue = Math.Max(0, value);
                if (_minimumChildWidth == newValue)
                {
                    return;
                }

                _minimumChildWidth = newValue;
                PerformLayout();
            }
        }

        [DefaultValue(-1)]
        [Category("Adaptive Stack Row")]
        public int SpringControlTabIndex
        {
            get => _springControlTabIndex;
            set
            {
                if (_springControlTabIndex == value)
                {
                    return;
                }

                _springControlTabIndex = value;
                PerformLayout();
            }
        }

        [DefaultValue(AdaptiveRowSpringMode.Both)]
        [Category("Adaptive Stack Row")]
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

        [DefaultValue(true)]
        [Category("Adaptive Stack Row")]
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
        [Category("Adaptive Stack Row")]
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

        [DefaultValue(true)]
        [Category("Adaptive Stack Row")]
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
                NotifyHostLayout();
            }
        }

        [DefaultValue(AdaptiveRowContentAlignment.Left)]
        [Category("Adaptive Stack Row")]
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
        [Category("Adaptive Stack Row")]
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
        [Description("Experimental plain-data item rules. These create and sync real child controls without owning them.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdaptiveStackRowItemRuleCollection ItemRules => _itemRules;

        public void CaptureCurrentLayoutAsBaseline()
        {
            foreach (Control control in Controls.Cast<Control>())
            {
                CaptureControlBaseline(control);
            }
            PerformLayout();
        }

        public void ForceRefreshLayout()
        {
            if (IsInDesignMode())
            {
                NormalizeDesignTimeBaselines();
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
            if (IsInDesignMode())
            {
                CaptureControlBaseline(e.Control);
            }

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
            PerformLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutChildren();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!IsInDesignMode())
            {
                return;
            }

            using var pen = new Pen(Color.Gray);
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void ChildLayoutChanged(object? sender, EventArgs e)
        {
            if (_isApplyingLayout)
            {
                return;
            }

            if (sender is Control child && IsInDesignMode())
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
            Control[] visibleControls = Controls
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
                    int height = _stretchChildHeight ? Math.Max(1, availableHeight) : child.Height;
                    int y = _stretchChildHeight
                        ? topInset
                        : topInset + Math.Max(0, (availableHeight - height) / 2);
                    Rectangle newBounds = new Rectangle(x, y, widths[i], height);

                    if (child.Bounds != newBounds)
                    {
                        child.Bounds = newBounds;
                    }

                    if (debugParts != null)
                    {
                        string gapText = i < gaps.Length ? $",g={gaps[i]}" : string.Empty;
                        debugParts.Add($"{child.Name}:{GetBaseWidth(child)}->{widths[i]}{gapText}");
                    }

                    if (i < visibleControls.Length - 1)
                    {
                        x += widths[i] + gaps[i];
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
                Debug.WriteLine($"[AdaptiveStackRowPanel:{Name}] {report}");
                if (!string.Equals(_lastLayoutReport, report, StringComparison.Ordinal))
                {
                    _lastLayoutReport = report;
                    LayoutReportChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private int GetAlignedStartX(int[] widths, int[] gaps, int availableWidth, int leadingSpacing, int trailingSpacing)
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

        private void FitRowToAvailableWidth(Control[] controls, int[] widths, int[] gaps, int availableWidth, int leadingSpacing, int trailingSpacing)
        {
            int totalWidth = leadingSpacing + trailingSpacing + widths.Sum() + gaps.Sum();
            if (totalWidth <= availableWidth)
            {
                if (CanSpringExpand())
                {
                    ExpandSpringToFillAvailableWidth(controls, widths, availableWidth - totalWidth);
                }
                return;
            }

            int overflow = totalWidth - availableWidth;
            int[] minimumWidths = controls.Select(GetMinimumWidth).ToArray();

            if (CanSpringContract())
            {
                ContractSpringFirst(controls, widths, minimumWidths, ref overflow);
            }

            if (overflow <= 0)
            {
                return;
            }

            ReduceGapsForAlignment(gaps, overflow);

            totalWidth = leadingSpacing + trailingSpacing + widths.Sum() + gaps.Sum();
            if (totalWidth <= availableWidth)
            {
                if (CanSpringExpand())
                {
                    ExpandSpringToFillAvailableWidth(controls, widths, availableWidth - totalWidth);
                }
                return;
            }

            overflow = totalWidth - availableWidth;
            ReduceWidthsForAlignment(widths, minimumWidths, overflow);
        }

        private bool CanSpringExpand()
        {
            return _springExpands is AdaptiveRowSpringMode.Expands or AdaptiveRowSpringMode.Both;
        }

        private bool CanSpringContract()
        {
            return _springExpands is AdaptiveRowSpringMode.Contracts or AdaptiveRowSpringMode.Both;
        }

        private void ContractSpringFirst(Control[] controls, int[] widths, int[] minimumWidths, ref int overflow)
        {
            Control? spring = FindSpringControl(controls);
            if (spring == null)
            {
                return;
            }

            int springIndex = Array.FindIndex(controls, c => ReferenceEquals(c, spring));
            if (springIndex < 0)
            {
                return;
            }

            int canReduce = Math.Max(0, widths[springIndex] - minimumWidths[springIndex]);
            int reduce = Math.Min(canReduce, overflow);
            widths[springIndex] -= reduce;
            overflow -= reduce;
        }

        private void ExpandSpringToFillAvailableWidth(Control[] controls, int[] widths, int spareWidth)
        {
            if (spareWidth <= 0)
            {
                return;
            }

            Control? spring = FindSpringControl(controls);
            if (spring == null)
            {
                return;
            }

            int springIndex = Array.FindIndex(controls, c => ReferenceEquals(c, spring));
            if (springIndex >= 0)
            {
                widths[springIndex] += spareWidth;
            }
        }

        internal Control? FindSpringControl(Control[]? orderedControls = null)
        {
            Control[] controls = orderedControls ?? GetOrderedChildren().ToArray();
            if (_springControlTabIndex >= 0)
            {
                Control? configured = controls.FirstOrDefault(c => c.TabIndex == _springControlTabIndex);
                if (configured != null)
                {
                    return configured;
                }
            }

            return null;
        }

        internal IReadOnlyList<Control> GetOrderedChildren()
        {
            return Controls
                .Cast<Control>()
                .Where(c => c.Visible)
                .OrderBy(GetLayoutOrder)
                .ThenBy(c => c.Top)
                .ToArray();
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

        private void ReduceGapsForAlignment(int[] gaps, int reductionNeeded)
        {
            switch (_contentAlignment)
            {
                case AdaptiveRowContentAlignment.Right:
                    ReduceValuesLeftToRight(gaps, reductionNeeded);
                    break;
                case AdaptiveRowContentAlignment.Center:
                    ReduceValuesEdgesIn(gaps, reductionNeeded);
                    break;
                default:
                    ReduceValuesRightToLeft(gaps, reductionNeeded);
                    break;
            }
        }

        private void ReduceWidthsForAlignment(int[] widths, int[] minimumWidths, int reductionNeeded)
        {
            switch (_contentAlignment)
            {
                case AdaptiveRowContentAlignment.Right:
                    ReduceWidthsLeftToRight(widths, minimumWidths, reductionNeeded);
                    break;
                case AdaptiveRowContentAlignment.Center:
                    ReduceWidthsEvenly(widths, minimumWidths, reductionNeeded);
                    break;
                default:
                    ReduceWidthsRightToLeft(widths, minimumWidths, reductionNeeded);
                    break;
            }
        }

        private static void ReduceValuesLeftToRight(int[] values, int reductionNeeded)
        {
            for (int i = 0; i < values.Length && reductionNeeded > 0; i++)
            {
                int reduce = Math.Min(values[i], reductionNeeded);
                values[i] -= reduce;
                reductionNeeded -= reduce;
            }
        }

        private static void ReduceValuesRightToLeft(int[] values, int reductionNeeded)
        {
            for (int i = values.Length - 1; i >= 0 && reductionNeeded > 0; i--)
            {
                int reduce = Math.Min(values[i], reductionNeeded);
                values[i] -= reduce;
                reductionNeeded -= reduce;
            }
        }

        private static void ReduceValuesEdgesIn(int[] values, int reductionNeeded)
        {
            int left = 0;
            int right = values.Length - 1;
            while (left <= right && reductionNeeded > 0)
            {
                int reduceRight = Math.Min(values[right], reductionNeeded);
                values[right] -= reduceRight;
                reductionNeeded -= reduceRight;
                if (reductionNeeded <= 0 || left == right)
                {
                    break;
                }

                int reduceLeft = Math.Min(values[left], reductionNeeded);
                values[left] -= reduceLeft;
                reductionNeeded -= reduceLeft;
                left++;
                right--;
            }
        }

        private static void ReduceWidthsLeftToRight(int[] widths, int[] minimumWidths, int reductionNeeded)
        {
            for (int i = 0; i < widths.Length && reductionNeeded > 0; i++)
            {
                int canReduce = Math.Max(0, widths[i] - minimumWidths[i]);
                int reduce = Math.Min(canReduce, reductionNeeded);
                widths[i] -= reduce;
                reductionNeeded -= reduce;
            }
        }

        private static void ReduceWidthsRightToLeft(int[] widths, int[] minimumWidths, int reductionNeeded)
        {
            for (int i = widths.Length - 1; i >= 0 && reductionNeeded > 0; i--)
            {
                int canReduce = Math.Max(0, widths[i] - minimumWidths[i]);
                int reduce = Math.Min(canReduce, reductionNeeded);
                widths[i] -= reduce;
                reductionNeeded -= reduce;
            }
        }

        private static void ReduceWidthsEvenly(int[] widths, int[] minimumWidths, int reductionNeeded)
        {
            while (reductionNeeded > 0)
            {
                int[] reducibleIndexes = Enumerable.Range(0, widths.Length)
                    .Where(i => widths[i] > minimumWidths[i])
                    .ToArray();

                if (reducibleIndexes.Length == 0)
                {
                    break;
                }

                int slice = Math.Max(1, (int)Math.Ceiling(reductionNeeded / (double)reducibleIndexes.Length));
                foreach (int i in reducibleIndexes)
                {
                    if (reductionNeeded <= 0)
                    {
                        break;
                    }

                    int canReduce = Math.Max(0, widths[i] - minimumWidths[i]);
                    int reduce = Math.Min(canReduce, Math.Min(slice, reductionNeeded));
                    widths[i] -= reduce;
                    reductionNeeded -= reduce;
                }
            }
        }

        private int GetEffectiveWidth(Control control)
        {
            int baseWidth = GetBaseWidth(control);
            if (IsInDesignMode())
            {
                return Math.Max(GetMinimumWidth(control), baseWidth);
            }

            return Math.Max(GetMinimumWidth(control), baseWidth);
        }

        private int GetMinimumWidth(Control control)
        {
            return Math.Max(GetContentMinimumWidth(control), Math.Max(control.MinimumSize.Width, Math.Max(1, _minimumChildWidth)));
        }

        private static int GetContentMinimumWidth(Control control)
        {
            return control is ModernButton button
                ? button.GetPreferredContentWidth()
                : 0;
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
            return _usePaddingFromItems ? Math.Max(0, control.Margin.Left) : 0;
        }

        private int GetTrailingSpacing(Control control)
        {
            return _usePaddingFromItems ? Math.Max(0, control.Margin.Right) : 0;
        }

        private int GetInterItemSpacing(Control current, Control next)
        {
            int spacing = Math.Max(0, current.Margin.Right) + Math.Max(0, next.Margin.Left);
            return spacing > 0 ? spacing : _itemGap;
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
                if (i < orderedControls.Length - 1)
                {
                    x += control.Width + GetInterItemSpacing(control, orderedControls[i + 1]);
                }
            }
        }

        private int GetLayoutOrder(Control control)
        {
            return _useTabIndexForOrder ? control.TabIndex : GetBaseLeft(control);
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
        [Description("When true, this row steps its child fonts down 2pt at 150%+ DPI even if the global StepDownHighDpiFonts setting is off. Default off; the global setting normally controls it.")]
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

        private void ApplyDefaultChildMargin(Control control)
        {
            if (!IsInDesignMode())
            {
                return;
            }

            Padding margin = control.Margin;
            bool hasUntouchedDefaultMargin = margin.Left == 3 && margin.Top == 3 && margin.Right == 3 && margin.Bottom == 3;
            if (!hasUntouchedDefaultMargin)
            {
                return;
            }

            control.Margin = new Padding(_defaultChildHorizontalMargin, margin.Top, _defaultChildHorizontalMargin, margin.Bottom);
        }

        private void NotifyHostLayout()
        {
            if (Parent is AdaptiveStackPanel host)
            {
                host.SyncRuleFromRow(this);
                host.PerformStackLayout();
            }
            else
            {
                PerformLayout();
            }
        }

        private bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime
                || Site?.DesignMode == true
                || Parent?.Site?.DesignMode == true;
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

            if (rule.IsSpring)
            {
                _preferredSpringRule = rule;
            }
            else if (ReferenceEquals(_preferredSpringRule, rule))
            {
                _preferredSpringRule = null;
            }

            RequestItemRulesSync();
        }

        internal void OnItemRuleControlTypeChanged(AdaptiveRowItemRule rule)
        {
            if (!IsInDesignMode() || _isSyncingItemRules)
            {
                return;
            }

            string controlName = rule.ControlName.Trim();
            if (IsSupportedItemRule(rule)
                && IsValidComponentName(controlName)
                && FindChild(controlName) is Control child
                && !RuleMatchesExistingChild(rule.ControlType, child)
                && IsItemRuleManagedChild(child))
            {
                _isSyncingItemRules = true;
                try
                {
                    ReplaceDesignerChild(child, rule.ControlType, controlName);
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

            if (!string.IsNullOrWhiteSpace(oldControlName)
                && IsValidComponentName(newControlName)
                && FindChild(oldControlName) is Control oldChild
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

            AdaptiveRowItemRule? rule = _itemRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.ControlName, child.Name, StringComparison.Ordinal));

            if (rule == null)
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? itemRulesProperty = TypeDescriptor.GetProperties(this)[nameof(ItemRules)];

            changeService?.OnComponentChanging(this, itemRulesProperty);
            _isSyncingItemRules = true;
            try
            {
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

            changeService?.OnComponentChanged(this, itemRulesProperty, null, _itemRules);
        }

        internal void AdoptExternalChildIntoItemRules(Control child)
        {
            if (!IsInDesignMode() || _isSyncingItemRules || string.IsNullOrWhiteSpace(child.Name))
            {
                return;
            }

            AdaptiveRowPanelAddControlType controlType = GetControlTypeId(child);
            if (!IsSupportedItemRuleType(controlType)
                || _itemRules.Any(rule => string.Equals(rule.ControlName, child.Name, StringComparison.Ordinal)))
            {
                return;
            }

            var newRule = new AdaptiveRowItemRule
            {
                ControlName = child.Name,
                ControlType = controlType,
                Text = child.Text,
                Width = Math.Max(1, child.Width),
                MarginLeft = Math.Max(0, child.Margin.Left),
                MarginRight = Math.Max(0, child.Margin.Right),
                IsSpring = child.TabIndex == SpringControlTabIndex
            };

            int insertIndex = FindRuleInsertIndexByTabIndex(child);

            _isSyncingItemRules = true;
            try
            {
                if (insertIndex >= 0 && insertIndex < _itemRules.Count)
                    _itemRules.Insert(insertIndex, newRule);
                else
                    _itemRules.Add(newRule);
            }
            finally
            {
                _isSyncingItemRules = false;
            }
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
            if (!IsInDesignMode() || _isSyncingItemRules || string.IsNullOrWhiteSpace(child.Name))
                return;

            AdaptiveRowItemRule? rule = _itemRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.ControlName, child.Name, StringComparison.Ordinal));

            if (rule == null)
                return;
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? itemRulesProperty = TypeDescriptor.GetProperties(this)[nameof(ItemRules)];

            changeService?.OnComponentChanging(this, itemRulesProperty);
            _isSyncingItemRules = true;
            try
            {
                _itemRules.Remove(rule);
            }
            finally
            {
                _isSyncingItemRules = false;
            }

            changeService?.OnComponentChanged(this, itemRulesProperty, null, _itemRules);
            CaptureCurrentLayoutAsBaseline();
            ForceRefreshLayout();

            if (Parent is AdaptiveStackPanel stack)
            {
                stack.PerformStackLayout();
            }
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
            else
            {
                SyncChildrenFromRules();
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
                foreach (AdaptiveRowItemRule rule in _itemRules)
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

                    Control? child = FindChild(controlName);

                    if (child == null && !IsDesignerComponentNameAvailable(controlName))
                    {
                        continue;
                    }

                    if (child != null && !RuleMatchesExistingChild(rule.ControlType, child))
                    {
                        rule.ControlType = GetControlTypeId(child);
                    }

                    bool createdChild = child == null;
                    child ??= CreateDesignerChild(rule.ControlType, controlName);
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

                if (Parent is AdaptiveStackPanel stack)
                {
                    stack.PerformStackLayout();
                }
            }
            finally
            {
                _isSyncingItemRules = false;
            }
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
                AdaptiveRowPanelAddControlType.Label => "label",
                AdaptiveRowPanelAddControlType.TextBox => "textBox",
                AdaptiveRowPanelAddControlType.ComboBox => "comboBox",
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
            return controlType is AdaptiveRowPanelAddControlType.TextBox
                or AdaptiveRowPanelAddControlType.NumericTextBox
                or AdaptiveRowPanelAddControlType.ComboBox
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

        internal void QueueItemRuleChildRemoval(AdaptiveRowItemRule rule)
        {
            if (!IsSupportedItemRule(rule))
            {
                return;
            }

            string controlName = rule.ControlName.Trim();
            if (IsValidComponentName(controlName))
            {
                _pendingItemRuleChildRemovals.Add(controlName);
            }

            RequestItemRulesSync();
        }

        private void RemovePendingItemRuleChildren()
        {
            if (_pendingItemRuleChildRemovals.Count == 0)
            {
                return;
            }

            HashSet<string> activeRuleNames = _itemRules
                .Select(rule => rule.ControlName.Trim())
                .Where(IsValidComponentName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string controlName in _pendingItemRuleChildRemovals.ToArray())
            {
                if (activeRuleNames.Contains(controlName))
                {
                    continue;
                }

                if (FindChild(controlName) is Control child)
                {
                    DestroyDesignerChild(child);
                }
            }

            _pendingItemRuleChildRemovals.Clear();
        }

        internal void RemoveItemRuleChild(AdaptiveRowItemRule rule)
        {
            if (!IsInDesignMode() || _isSyncingItemRules || !IsSupportedItemRule(rule))
            {
                return;
            }

            string controlName = rule.ControlName.Trim();
            if (!IsValidComponentName(controlName) || FindChild(controlName) is not Control child)
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

            if (Parent is AdaptiveStackPanel stack)
            {
                stack.PerformStackLayout();
            }
        }

        private Control CreateDesignerChild(AdaptiveRowPanelAddControlType controlType, string controlName)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];
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

            changeService?.OnComponentChanging(this, controlsProperty);
            Controls.Add(child);
            changeService?.OnComponentChanged(this, controlsProperty, null, child);

            return child;
        }

        private Control ReplaceDesignerChild(Control oldChild, AdaptiveRowPanelAddControlType controlType, string controlName)
        {
            int tabIndex = oldChild.TabIndex;
            Padding margin = oldChild.Margin;
            DestroyDesignerChild(oldChild);

            Control newChild = CreateDesignerChild(controlType, controlName);
            newChild.TabIndex = tabIndex;
            newChild.Margin = margin;
            return newChild;
        }

        private void DestroyDesignerChild(Control child)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];

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

        private bool IsDesignerComponentNameAvailable(string componentName, IComponent? allowedComponent = null)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            if (designerHost?.Container == null)
            {
                return true;
            }

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

        private Control? FindChild(string controlName)
        {
            return Controls
                .Cast<Control>()
                .FirstOrDefault(child => string.Equals(child.Name, controlName, StringComparison.Ordinal));
        }

        private void ApplyRuleToChild(AdaptiveRowItemRule rule, Control child, bool createdFromRule = false)
        {
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
            HashSet<string> existingNames = Controls
                .Cast<Control>()
                .Where(child => IsValidComponentName(child.Name))
                .Select(child => child.Name)
                .ToHashSet(StringComparer.Ordinal);

            Control[] orderedControls = _itemRules
                .Select(rule => rule.ControlName.Trim())
                .Where(name => IsValidComponentName(name) && existingNames.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .Select(name => FindChild(name))
                .Where(child => child != null)
                .Cast<Control>()
                .Concat(GetOrderedChildren().Where(child => _itemRules.All(rule =>
                    !string.Equals(rule.ControlName.Trim(), child.Name, StringComparison.Ordinal))))
                .ToArray();

            for (int i = 0; i < orderedControls.Length; i++)
            {
                orderedControls[i].TabIndex = i * 2;
            }
        }

        private void ApplyItemRuleSpringState()
        {
            AdaptiveRowItemRule? springRule = null;
            int springTabIndex = -1;

            if (_preferredSpringRule is { IsSpring: true } preferredRule)
            {
                string preferredControlName = preferredRule.ControlName.Trim();
                Control? preferredChild = IsSupportedItemRule(preferredRule) && IsValidComponentName(preferredControlName)
                    ? FindChild(preferredControlName)
                    : null;

                if (preferredChild != null)
                {
                    springRule = preferredRule;
                    springTabIndex = preferredChild.TabIndex;
                }
            }

            foreach (AdaptiveRowItemRule rule in _itemRules)
            {
                if (!rule.IsSpring)
                {
                    continue;
                }

                string controlName = rule.ControlName.Trim();
                Control? child = IsSupportedItemRule(rule) && IsValidComponentName(controlName)
                    ? FindChild(controlName)
                    : null;

                if (springRule == null && child != null)
                {
                    springRule = rule;
                    springTabIndex = child.TabIndex;
                    continue;
                }

                if (!ReferenceEquals(rule, springRule))
                {
                    rule.IsSpring = false;
                }
            }

            SpringControlTabIndex = springRule == null ? -1 : springTabIndex;
            _preferredSpringRule = springRule;
        }

        private void ConfigureNewRuleChild(Control child, AdaptiveRowPanelAddControlType controlType)
        {
            ApplyStandardFont(child);
            child.Margin = new Padding(_defaultChildHorizontalMargin, 0, _defaultChildHorizontalMargin, 0);
            child.TabIndex = Math.Max(0, Controls.Count * 2);
            child.Height = Math.Max(18, RowHeight - Padding.Top - Padding.Bottom);

            switch (child)
            {
                case AdaptiveRowSpacer spacer:
                    spacer.Size = new Size(80, Math.Max(18, child.Height));
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
                AdaptiveRowPanelAddControlType.TextBox => typeof(RoundedTextBox),
                AdaptiveRowPanelAddControlType.ComboBox => typeof(RoundedComboBox),
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
                ModernButton => AdaptiveRowPanelAddControlType.Button,
                RoundedNumericTextBox => AdaptiveRowPanelAddControlType.NumericTextBox,
                RoundedTextBox => AdaptiveRowPanelAddControlType.TextBox,
                RoundedMultiSelectComboBox => AdaptiveRowPanelAddControlType.MultiSelectComboBox,
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
            return GetControlTypeId(child) == ruleType;
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

    public class AdaptiveStackRowItemRuleCollection : Collection<AdaptiveRowItemRule>
    {
        private readonly Dictionary<AdaptiveRowItemRule, string> _knownControlNames = new();

        internal AdaptiveStackRowPanel? Owner { get; set; }

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
            Owner?.QueueItemRuleChildRemoval(item);
            base.RemoveItem(index);
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
            foreach (AdaptiveRowItemRule item in items)
            {
                Owner?.QueueItemRuleChildRemoval(item);
            }
            base.ClearItems();
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
            target.ControlType = source.ControlType;
            target.Text = source.Text;
            target.Width = source.Width;
            target.MarginLeft = source.MarginLeft;
            target.MarginRight = source.MarginRight;
            target.IsSpring = source.IsSpring;
        }
    }
}
