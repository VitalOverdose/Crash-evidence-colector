using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Editors;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// Controls whether a stack row can absorb spare vertical space or give space back when the stack is short on height.
    /// Multiple spring rows share the available expansion or contraction evenly.
    /// </summary>
    public enum AdaptiveStackRowSpringMode
    {
        /// <summary>
        /// The row keeps its authored height and does not participate in vertical spring layout.
        /// </summary>
        False,

        /// <summary>
        /// The row can expand when spare height is available and contract when the stack is short on height.
        /// </summary>
        True,

        /// <summary>
        /// The row can expand when spare height is available, but it will not contract below its authored height.
        /// </summary>
        ExpandOnly,

        /// <summary>
        /// The row can contract when the stack is short on height, but it will not expand above its authored height.
        /// </summary>
        ContractOnly
    }

    public enum RoundedAdaptiveStackRowType
    {
        ARP,
        RAS
    }

    [Designer(typeof(AdaptiveStackPanelDesigner))]
    public class AdaptiveStackPanel : Panel
    {
        internal const int NestedRoundedStackHeightMultiplier = 3;
        internal const int LegacyNestedRoundedStackHeight = 160;

        private int _defaultRowHeight = 36;
        private int _defaultGapBottom = 4;
        private bool _dpiAware;
        private int _dpiGrowthPercent = 25;
        private int _rowInset = 1;
        private bool _stretchChildHeight = true;
        private bool _requestHostFormDpiHeightExpansion;
        private bool _enableRowDragReorder;
        private bool _dragHostForm;
        private bool _isApplyingLayout;
        private AdaptiveStackRowPanel? _dragRow;
        private Point _dragStartScreenPoint;
        private bool _isRowDragActive;
        private int _lastHostSizeHintContentHeight = int.MinValue;
        private Rectangle _lastHostSizeHintBounds = Rectangle.Empty;
        private readonly List<string> _rowOrder = new();
        private readonly Dictionary<AdaptiveStackRowPanel, string> _knownRowNames = new();
        private readonly AdaptiveStackRowRuleCollection _rowRules = new();
        private bool _isSyncingRowRules;
        private bool _rowRulesSyncPending;
        private int _lastNonCollapsedDesignWidth;

        public event EventHandler? HostSizeHintChanged;
        public event EventHandler? RowDragReordered;

        internal bool IsApplyingStackLayout => _isApplyingLayout;

        internal static int GetNestedStackHeightForRowHeight(int rowHeight)
        {
            return Math.Max(1, rowHeight * NestedRoundedStackHeightMultiplier);
        }

        internal int GetDefaultNestedStackHeight()
        {
            return GetNestedStackHeightForRowHeight(_defaultRowHeight);
        }

        public AdaptiveStackPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Padding = new Padding(0);
            _rowRules.Owner = this;
        }

        [DefaultValue(36)]
        [Category("Adaptive Stack")]
        public int DefaultRowHeight
        {
            get => _defaultRowHeight;
            set => _defaultRowHeight = Math.Max(1, value);
        }

        [DefaultValue(4)]
        [Category("Adaptive Stack")]
        public int DefaultGapBottom
        {
            get => _defaultGapBottom;
            set => _defaultGapBottom = Math.Max(0, value);
        }

        [DefaultValue(false)]
        [Category("Adaptive Stack")]
        public bool DpiAware
        {
            get => _dpiAware;
            set
            {
                if (_dpiAware == value)
                {
                    return;
                }

                _dpiAware = value;
                PerformStackLayout();
            }
        }

        [DefaultValue(25)]
        [Category("Adaptive Stack")]
        [Description("Percent of DPI growth applied to row heights. 25 means 150% DPI becomes roughly 112.5% row height.")]
        public int DpiGrowthPercent
        {
            get => _dpiGrowthPercent;
            set
            {
                int newValue = Math.Clamp(value, 0, 100);
                if (_dpiGrowthPercent == newValue)
                {
                    return;
                }

                _dpiGrowthPercent = newValue;
                PerformStackLayout();
            }
        }

        [DefaultValue(1)]
        [Category("Adaptive Stack")]
        [Description("Horizontal inset applied to every row so the host panel remains selectable in the designer.")]
        public int RowInset
        {
            get => _rowInset;
            set
            {
                int newValue = Math.Max(0, value);
                if (_rowInset == newValue)
                {
                    return;
                }

                _rowInset = newValue;
                PerformStackLayout();
            }
        }

        [DefaultValue(true)]
        [Category("Adaptive Stack")]
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
                foreach (AdaptiveStackRowPanel row in GetRows())
                {
                    row.StretchChildHeight = value;
                }
                PerformStackLayout();
            }
        }

        [DefaultValue(false)]
        [Category("Adaptive Stack")]
        [Description("When true, a RoundedForm host may add DPI height breathing room using this stack's DPI growth settings.")]
        public bool RequestHostFormDpiHeightExpansion
        {
            get => _requestHostFormDpiHeightExpansion;
            set
            {
                if (_requestHostFormDpiHeightExpansion == value)
                {
                    return;
                }

                _requestHostFormDpiHeightExpansion = value;
                NotifyHostSizeHintChanged(force: true);
            }
        }

        [DefaultValue(false)]
        [Category("Adaptive Stack")]
        [Description("When true, rows can be reordered by dragging them vertically.")]
        public bool EnableRowDragReorder
        {
            get => _enableRowDragReorder;
            set
            {
                if (_enableRowDragReorder == value)
                {
                    return;
                }

                _enableRowDragReorder = value;
                foreach (AdaptiveStackRowPanel row in Controls.OfType<AdaptiveStackRowPanel>())
                {
                    SetRowDragHandlers(row, value);
                }
            }
        }

        [DefaultValue(false)]
        [Category("Adaptive Stack")]
        [Description("When true, dragging the stack surface or row backgrounds moves the parent RoundedForm.")]
        public bool DragHostForm
        {
            get => _dragHostForm;
            set
            {
                if (_dragHostForm == value)
                {
                    return;
                }

                _dragHostForm = value;
                SetHostDragHandlers(value);
            }
        }

        [Browsable(false)]
        public IReadOnlyList<AdaptiveStackRowPanel> Rows => GetRows();

        [Browsable(false)]
        public IReadOnlyList<Control> StackItems => GetStackItems();

        [Category("Adaptive Stack")]
        [Description("Experimental plain-data row rules. These do not own or place controls.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(AdaptiveStackRuleCollectionEditor), typeof(CollectionEditor))]
        public AdaptiveStackRowRuleCollection RowRules => _rowRules;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string RowOrder
        {
            get => string.Join("|", _rowOrder);
            set
            {
                _rowOrder.Clear();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _rowOrder.AddRange(value
                        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.Ordinal));
                }

                PerformStackLayout();
            }
        }

        public IReadOnlyList<AdaptiveStackRowPanel> GetRows()
        {
            return GetStackItems().OfType<AdaptiveStackRowPanel>().ToArray();
        }

        public IReadOnlyList<Control> GetStackItems()
        {
            AdaptiveStackRowPanel[] rows = Controls
                .OfType<AdaptiveStackRowPanel>()
                .ToArray();
            Control[] items = Controls
                .Cast<Control>()
                .Where(IsStackLayoutItem)
                .ToArray();

            bool hasRowRules = HasUsableRowRules();

            if (IsInDesignMode())
            {
                TrackRowNamesAndRenames(rows);
            }

            if (IsInDesignMode() && !hasRowRules && !_isSyncingRowRules)
            {
                SyncRowOrder(rows);
            }

            Dictionary<string, Control> itemsByName = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            List<Control> orderedItems = new();
            IEnumerable<string> orderNames = _rowOrder.Count > 0 || !hasRowRules
                ? _rowOrder
                : _rowRules
                    .Select(rule => rule.RowName.Trim())
                    .Where(IsValidComponentName)
                    .Distinct(StringComparer.Ordinal);

            foreach (string rowName in orderNames)
            {
                if (itemsByName.TryGetValue(rowName, out Control? item))
                {
                    orderedItems.Add(item);
                    itemsByName.Remove(rowName);
                }
            }

            orderedItems.AddRange(items
                .Where(item => !orderedItems.Contains(item))
                .OrderBy(item => item.TabIndex)
                .ThenBy(item => item.Top));

            return orderedItems.ToArray();
        }

        public void PerformStackLayout()
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            LayoutRows();
        }

        public int GetVisibleContentHeight()
        {
            Control[] items = GetStackItems().ToArray();
            if (items.Length == 0)
            {
                return 0;
            }

            Padding layoutPadding = GetEffectiveLayoutPadding();
            int y = layoutPadding.Top;
            foreach (Control item in items)
            {
                y += GetEffectiveLayoutItemGapTop(item);
                int itemHeight = GetEffectiveLayoutItemHeight(item);
                y += itemHeight + GetEffectiveLayoutItemGapBottom(item);
            }

            return y + layoutPadding.Bottom;
        }

        public bool Open(string rowName)
        {
            return SetRowCollapsed(rowName, collapsed: false);
        }

        public bool Open(int rowIndex)
        {
            return SetRowCollapsed(rowIndex, collapsed: false);
        }

        public bool Close(string rowName)
        {
            return SetRowCollapsed(rowName, collapsed: true);
        }

        public bool Close(int rowIndex)
        {
            return SetRowCollapsed(rowIndex, collapsed: true);
        }

        public bool Toggle(string rowName)
        {
            AdaptiveStackRowPanel? row = FindRow(rowName);
            if (row == null)
            {
                return false;
            }

            row.StartsCollapsed = !row.StartsCollapsed;
            PerformStackLayout();
            return true;
        }

        public bool Toggle(int rowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(rowIndex);
            if (row == null)
            {
                return false;
            }

            row.StartsCollapsed = !row.StartsCollapsed;
            PerformStackLayout();
            return true;
        }

        public bool Swap(string firstRowName, string secondRowName)
        {
            AdaptiveStackRowPanel? first = FindRow(firstRowName);
            AdaptiveStackRowPanel? second = FindRow(secondRowName);
            return SwapRows(first, second);
        }

        public bool Swap(int firstRowIndex, int secondRowIndex)
        {
            AdaptiveStackRowPanel? first = FindRow(firstRowIndex);
            AdaptiveStackRowPanel? second = FindRow(secondRowIndex);
            return SwapRows(first, second);
        }

        public bool MoveUp(string rowName)
        {
            AdaptiveStackRowPanel? row = FindRow(rowName);
            return MoveRowByApi(row, -1);
        }

        public bool MoveUp(int rowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(rowIndex);
            return MoveRowByApi(row, -1);
        }

        public bool MoveDown(string rowName)
        {
            AdaptiveStackRowPanel? row = FindRow(rowName);
            return MoveRowByApi(row, 1);
        }

        public bool MoveDown(int rowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(rowIndex);
            return MoveRowByApi(row, 1);
        }

        public bool MoveTo(string rowName, int targetRowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(rowName);
            return MoveRowTo(row, targetRowIndex);
        }

        public bool MoveTo(int sourceRowIndex, int targetRowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(sourceRowIndex);
            return MoveRowTo(row, targetRowIndex);
        }

        public bool InsertRow(AdaptiveStackRowPanel row, int targetRowIndex)
        {
            if (row == null || row.IsDisposed || row.Disposing)
            {
                return false;
            }

            string rowName = EnsureRowName(row);
            bool alreadyHosted = ReferenceEquals(row.Parent, this);
            if (!alreadyHosted)
            {
                row.Parent?.Controls.Remove(row);
                Controls.Add(row);
            }

            row.StretchChildHeight = _stretchChildHeight;
            if (row.RowHeight <= 0)
            {
                row.RowHeight = _defaultRowHeight;
            }

            _rowOrder.Remove(rowName);
            int insertIndex = Math.Clamp(targetRowIndex, 0, _rowOrder.Count);
            _rowOrder.Insert(insertIndex, rowName);
            ApplyRowOrderToDesigner();
            PerformStackLayout();
            return true;
        }

        /// <summary>
        /// Finds the row with the supplied control name, creates or updates its row rule, then applies the rule back to the live row.
        /// Use this when changing rule-only layout data such as vertical spring mode, or when code needs to keep the row rule and row control in sync.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="update">Callback that modifies the matching row rule.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowRule(string rowName, Action<AdaptiveStackRowRule> update)
        {
            if (update == null)
            {
                return false;
            }

            AdaptiveStackRowPanel? row = FindRow(rowName);
            return SetRowRule(row, update);
        }

        /// <summary>
        /// Finds the row at the current visual index, creates or updates its row rule, then applies the rule back to the live row.
        /// Numeric indexes use the current stack order, including rows that are collapsed.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="update">Callback that modifies the matching row rule.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowRule(int rowIndex, Action<AdaptiveStackRowRule> update)
        {
            if (update == null)
            {
                return false;
            }

            AdaptiveStackRowPanel? row = FindRow(rowIndex);
            return SetRowRule(row, update);
        }

        /// <summary>
        /// Sets the authored height for a row and keeps the matching row rule synchronized.
        /// The stack may still scale this value for DPI, and vertical spring rows may grow or shrink during layout.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="rowHeight">The authored row height in pixels. Values below 1 are clamped to 1.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowHeight(string rowName, int rowHeight)
        {
            return SetRowRule(rowName, rule => rule.RowHeight = Math.Max(1, rowHeight));
        }

        /// <summary>
        /// Sets the authored height for a row at the current visual index and keeps the matching row rule synchronized.
        /// The stack may still scale this value for DPI, and vertical spring rows may grow or shrink during layout.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="rowHeight">The authored row height in pixels. Values below 1 are clamped to 1.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowHeight(int rowIndex, int rowHeight)
        {
            return SetRowRule(rowIndex, rule => rule.RowHeight = Math.Max(1, rowHeight));
        }

        /// <summary>
        /// Sets the authored height used while a row is collapsed and keeps the matching row rule synchronized.
        /// Use 0 to keep the existing fully-hidden collapsed behavior.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="collapsedHeight">The authored collapsed row height in pixels. Values below 0 are clamped to 0.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowCollapsedHeight(string rowName, int collapsedHeight)
        {
            return SetRowRule(rowName, rule => rule.CollapsedHeight = Math.Max(0, collapsedHeight));
        }

        /// <summary>
        /// Sets the authored height used while a row at the current visual index is collapsed and keeps the matching row rule synchronized.
        /// Use 0 to keep the existing fully-hidden collapsed behavior.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="collapsedHeight">The authored collapsed row height in pixels. Values below 0 are clamped to 0.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowCollapsedHeight(int rowIndex, int collapsedHeight)
        {
            return SetRowRule(rowIndex, rule => rule.CollapsedHeight = Math.Max(0, collapsedHeight));
        }

        /// <summary>
        /// Sets whether a row starts collapsed and keeps the matching row rule synchronized.
        /// Collapsed rows stay in the stack order and use their configured collapsed height.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="collapsed">Use <c>true</c> to collapse the row; use <c>false</c> to open it.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowCollapsedState(string rowName, bool collapsed)
        {
            return SetRowRule(rowName, rule => rule.StartsCollapsed = collapsed);
        }

        /// <summary>
        /// Sets whether a row at the current visual index starts collapsed and keeps the matching row rule synchronized.
        /// Collapsed rows stay in the stack order and use their configured collapsed height.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="collapsed">Use <c>true</c> to collapse the row; use <c>false</c> to open it.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowCollapsedState(int rowIndex, bool collapsed)
        {
            return SetRowRule(rowIndex, rule => rule.StartsCollapsed = collapsed);
        }

        /// <summary>
        /// Sets the vertical spring mode for a row and keeps the matching row rule synchronized.
        /// Spring rows share spare vertical space, or contract when space is short, according to the selected mode.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="springMode">How the row should absorb vertical expansion or contraction.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowSpringMode(string rowName, AdaptiveStackRowSpringMode springMode)
        {
            return SetRowRule(rowName, rule => rule.SpringMode = springMode);
        }

        /// <summary>
        /// Sets the vertical spring mode for a row at the current visual index and keeps the matching row rule synchronized.
        /// Spring rows share spare vertical space, or contract when space is short, according to the selected mode.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="springMode">How the row should absorb vertical expansion or contraction.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowSpringMode(int rowIndex, AdaptiveStackRowSpringMode springMode)
        {
            return SetRowRule(rowIndex, rule => rule.SpringMode = springMode);
        }

        /// <summary>
        /// Sets whether the controls inside a row stretch to the row height and keeps the matching row rule synchronized.
        /// This is useful for fixed-height progress/status rows where controls should remain centered instead of filling the row.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="stretchChildHeight">Use <c>true</c> to stretch child controls to the row height; use <c>false</c> to keep their authored heights.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowStretchChildHeight(string rowName, bool stretchChildHeight)
        {
            return SetRowRule(rowName, rule => rule.StretchChildHeight = stretchChildHeight);
        }

        /// <summary>
        /// Sets whether the controls inside a row at the current visual index stretch to the row height and keeps the matching row rule synchronized.
        /// This is useful for fixed-height progress/status rows where controls should remain centered instead of filling the row.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="stretchChildHeight">Use <c>true</c> to stretch child controls to the row height; use <c>false</c> to keep their authored heights.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowStretchChildHeight(int rowIndex, bool stretchChildHeight)
        {
            return SetRowRule(rowIndex, rule => rule.StretchChildHeight = stretchChildHeight);
        }

        /// <summary>
        /// Sets the vertical gaps around a row and keeps the matching row rule synchronized.
        /// Gaps belong to the stack layout, not to the controls inside the row.
        /// </summary>
        /// <param name="rowName">The <see cref="Control.Name"/> of the row to update.</param>
        /// <param name="gapTop">Space above the row in pixels. Values below 0 are clamped to 0.</param>
        /// <param name="gapBottom">Space below the row in pixels. Values below 0 are clamped to 0.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowGaps(string rowName, int gapTop, int gapBottom)
        {
            return SetRowRule(rowName, rule =>
            {
                rule.GapTop = Math.Max(0, gapTop);
                rule.GapBottom = Math.Max(0, gapBottom);
            });
        }

        /// <summary>
        /// Sets the vertical gaps around a row at the current visual index and keeps the matching row rule synchronized.
        /// Gaps belong to the stack layout, not to the controls inside the row.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index in the current stack order.</param>
        /// <param name="gapTop">Space above the row in pixels. Values below 0 are clamped to 0.</param>
        /// <param name="gapBottom">Space below the row in pixels. Values below 0 are clamped to 0.</param>
        /// <returns><c>true</c> when the row was found and updated; otherwise <c>false</c>.</returns>
        public bool SetRowGaps(int rowIndex, int gapTop, int gapBottom)
        {
            return SetRowRule(rowIndex, rule =>
            {
                rule.GapTop = Math.Max(0, gapTop);
                rule.GapBottom = Math.Max(0, gapBottom);
            });
        }

        public AdaptiveStackRowPanel? FindRow(string rowName)
        {
            if (string.IsNullOrWhiteSpace(rowName))
            {
                return null;
            }

            return GetRows().FirstOrDefault(row => string.Equals(row.Name, rowName, StringComparison.Ordinal));
        }

        public AdaptiveStackRowPanel? FindRow(int rowIndex)
        {
            IReadOnlyList<AdaptiveStackRowPanel> rows = GetRows();
            return rowIndex >= 0 && rowIndex < rows.Count ? rows[rowIndex] : null;
        }

        private bool SetRowRule(AdaptiveStackRowPanel? row, Action<AdaptiveStackRowRule> update)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Name))
            {
                return false;
            }

            AdaptiveStackRowRule rule = FindOrCreateRowRule(row);

            _isSyncingRowRules = true;
            try
            {
                update(rule);
                ApplyRuleToRow(rule, row);
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            PerformStackLayout();
            return true;
        }

        private AdaptiveStackRowRule FindOrCreateRowRule(AdaptiveStackRowPanel row)
        {
            AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.RowName.Trim(), row.Name, StringComparison.Ordinal));

            if (rule != null)
            {
                return rule;
            }

            rule = new AdaptiveStackRowRule
            {
                RowName = row.Name,
                RowHeight = Math.Max(1, row.RowHeight),
                CollapsedHeight = Math.Max(0, row.CollapsedHeight),
                GapTop = Math.Max(0, row.GapTop),
                GapBottom = Math.Max(0, row.GapBottom),
                StartsCollapsed = row.StartsCollapsed,
                StretchChildHeight = row.StretchChildHeight
            };

            _isSyncingRowRules = true;
            try
            {
                _rowRules.Add(rule);
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            return rule;
        }

        private bool SwapRows(AdaptiveStackRowPanel? first, AdaptiveStackRowPanel? second)
        {
            if (first == null || second == null || ReferenceEquals(first, second))
            {
                return false;
            }

            AdaptiveStackRowPanel[] rows = GetRows().ToArray();
            int firstIndex = Array.IndexOf(rows, first);
            int secondIndex = Array.IndexOf(rows, second);
            if (firstIndex < 0 || secondIndex < 0 || firstIndex >= _rowOrder.Count || secondIndex >= _rowOrder.Count)
            {
                return false;
            }

            (_rowOrder[firstIndex], _rowOrder[secondIndex]) = (_rowOrder[secondIndex], _rowOrder[firstIndex]);
            ApplyRowOrderToDesigner();
            PerformStackLayout();
            return true;
        }

        private bool MoveRowByApi(AdaptiveStackRowPanel? row, int direction)
        {
            if (row == null)
            {
                return false;
            }

            AdaptiveStackRowPanel[] rows = GetRows().ToArray();
            int index = Array.IndexOf(rows, row);
            int newIndex = index + direction;
            if (index < 0 || newIndex < 0 || newIndex >= rows.Length)
            {
                return false;
            }

            return SwapRows(row, rows[newIndex]);
        }

        private bool MoveRowTo(AdaptiveStackRowPanel? row, int targetRowIndex)
        {
            if (row == null)
            {
                return false;
            }

            AdaptiveStackRowPanel[] rows = GetRows().ToArray();
            int currentIndex = Array.IndexOf(rows, row);
            if (currentIndex < 0 || currentIndex >= _rowOrder.Count || _rowOrder.Count == 0)
            {
                return false;
            }

            int clampedTarget = Math.Clamp(targetRowIndex, 0, _rowOrder.Count - 1);
            if (currentIndex == clampedTarget)
            {
                return true;
            }

            string rowName = _rowOrder[currentIndex];
            _rowOrder.RemoveAt(currentIndex);
            _rowOrder.Insert(clampedTarget, rowName);
            ApplyRowOrderToDesigner();
            PerformStackLayout();
            return true;
        }

        private bool SetRowCollapsed(string rowName, bool collapsed)
        {
            AdaptiveStackRowPanel? row = FindRow(rowName);
            if (row == null)
            {
                return false;
            }

            row.StartsCollapsed = collapsed;
            PerformStackLayout();
            return true;
        }

        private bool SetRowCollapsed(int rowIndex, bool collapsed)
        {
            AdaptiveStackRowPanel? row = FindRow(rowIndex);
            if (row == null)
            {
                return false;
            }

            row.StartsCollapsed = collapsed;
            PerformStackLayout();
            return true;
        }

        internal int GetScaledRowHeight(int authoredHeight)
        {
            if (!_dpiAware || IsInDesignMode())
            {
                return Math.Max(0, authoredHeight);
            }

            int dpi = DeviceDpi;
            float scale = dpi > 0 ? dpi / 96f : 1f;
            float growth = Math.Max(0f, scale - 1f);
            float adjustedScale = 1f + growth * (_dpiGrowthPercent / 100f);
            return Math.Max(0, (int)Math.Round(authoredHeight * adjustedScale));
        }

        private int GetEffectiveRowHeight(AdaptiveStackRowPanel row)
        {
            int authoredHeight = row.StartsCollapsed ? row.CollapsedHeight : row.RowHeight;
            return GetScaledRowHeight(authoredHeight);
        }

        internal void MoveRow(AdaptiveStackRowPanel row, int direction)
        {
            AdaptiveStackRowPanel[] rows = GetRows().ToArray();
            int index = Array.IndexOf(rows, row);
            int newIndex = index + direction;
            if (index < 0 || newIndex < 0 || newIndex >= rows.Length || index >= _rowOrder.Count || newIndex >= _rowOrder.Count)
            {
                return;
            }

            MoveRowTo(row, newIndex);
        }

        internal void RemoveRow(AdaptiveStackRowPanel row)
        {
            Controls.Remove(row);
            row.Dispose();
            ResequenceRows();
            PerformStackLayout();
        }

        public bool DragRowTo(string rowName, int targetRowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(rowName);
            return MoveRowTo(row, targetRowIndex);
        }

        public bool DragRowTo(int sourceRowIndex, int targetRowIndex)
        {
            AdaptiveStackRowPanel? row = FindRow(sourceRowIndex);
            return MoveRowTo(row, targetRowIndex);
        }

        public void ResequenceRows()
        {
            AdaptiveStackRowPanel[] rows = Controls
                .OfType<AdaptiveStackRowPanel>()
                .OrderBy(r => r.Top)
                .ThenBy(r => r.TabIndex)
                .ToArray();

            _rowOrder.Clear();
            foreach (AdaptiveStackRowPanel row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.Name))
                {
                    _rowOrder.Add(row.Name);
                }
            }

            ApplyRowOrderToDesigner();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control is AdaptiveStackRowPanel row)
            {
                TrackRowName(row);

                if (row.RowHeight <= 0)
                {
                    row.RowHeight = _defaultRowHeight;
                }

                row.StretchChildHeight = _stretchChildHeight;
                SetRowDragHandlers(row, _enableRowDragReorder);
                SetRowHostDragHandler(row, _dragHostForm);
                AdoptExternalRowIntoRowRules(row);
            }

            PerformStackLayout();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            if (e.Control is AdaptiveStackRowPanel row)
            {
                SetRowDragHandlers(row, enabled: false);
                SetRowHostDragHandler(row, enabled: false);
                UntrackRowName(row);
                RemoveExternalRowFromRowRules(row);
            }

            base.OnControlRemoved(e);
            PerformStackLayout();
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (IsInDesignMode())
            {
                if (Width > 0)
                {
                    _lastNonCollapsedDesignWidth = Width;
                }

                if (width <= 0)
                {
                    width = GetDesignTimeFallbackWidth();
                }
            }

            base.SetBoundsCore(x, y, width, height, specified);

            if (IsInDesignMode() && Width > 0)
            {
                _lastNonCollapsedDesignWidth = Width;
            }
        }

        private int GetDesignTimeFallbackWidth()
        {
            if (_lastNonCollapsedDesignWidth > 0)
            {
                return _lastNonCollapsedDesignWidth;
            }

            if (Width > 0)
            {
                return Width;
            }

            int rowWidth = Controls
                .OfType<AdaptiveStackRowPanel>()
                .Select(row => row.Width)
                .DefaultIfEmpty(0)
                .Max();

            Padding layoutPadding = GetEffectiveLayoutPadding();
            return Math.Max(24, rowWidth + layoutPadding.Left + layoutPadding.Right + (_rowInset * 2));
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutRows();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            PerformStackLayout();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            PerformStackLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!IsInDesignMode())
            {
                return;
            }

            using var pen = new Pen(Color.DarkGray);
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected virtual bool IsStackLayoutItem(Control control)
        {
            return control is AdaptiveStackRowPanel;
        }

        private void LayoutRows()
        {
            if (_isApplyingLayout)
            {
                return;
            }

            Control[] items = GetStackItems().ToArray();
            if (items.Length == 0)
            {
                NotifyHostSizeHintChanged();
                return;
            }

            Padding layoutPadding = GetEffectiveLayoutPadding();
            int availableWidth = Math.Max(1, ClientSize.Width - layoutPadding.Left - layoutPadding.Right - (_rowInset * 2));
            int availableHeight = Math.Max(0, ClientSize.Height - layoutPadding.Top - layoutPadding.Bottom);
            Dictionary<Control, int> springDeltas = GetVerticalSpringDeltas(items, availableHeight);
            int y = layoutPadding.Top;

            _isApplyingLayout = true;
            try
            {
                foreach (Control item in items)
                {
                    y += GetEffectiveLayoutItemGapTop(item);
                    int itemHeight = GetEffectiveLayoutItemHeight(item);
                    if (springDeltas.TryGetValue(item, out int springDelta))
                    {
                        itemHeight = Math.Max(0, itemHeight + springDelta);
                    }

                    Rectangle targetBounds = new Rectangle(layoutPadding.Left + _rowInset, y, availableWidth, itemHeight);
                    if (item.Bounds != targetBounds)
                    {
                        item.Bounds = targetBounds;
                    }

                    item.PerformLayout();
                    y += itemHeight + GetEffectiveLayoutItemGapBottom(item);
                }
            }
            finally
            {
                _isApplyingLayout = false;
            }

            NotifyHostSizeHintChanged();
        }

        protected virtual Padding GetEffectiveLayoutPadding()
        {
            return Padding;
        }

        protected virtual int GetEffectiveLayoutItemHeight(Control item)
        {
            return item switch
            {
                AdaptiveStackRowPanel row => GetEffectiveRowHeight(row),
                RoundedAdaptiveStackPanel stack => GetScaledRowHeight(stack.StartsCollapsed ? stack.CollapsedHeight : stack.StackHeight),
                _ => Math.Max(0, item.Height)
            };
        }

        protected virtual int GetEffectiveLayoutItemGapTop(Control item)
        {
            return item switch
            {
                AdaptiveStackRowPanel row => GetEffectiveGapTop(row),
                RoundedAdaptiveStackPanel stack => stack.GapTop,
                _ => Math.Max(0, item.Margin.Top)
            };
        }

        protected virtual int GetEffectiveLayoutItemGapBottom(Control item)
        {
            return item switch
            {
                AdaptiveStackRowPanel row => GetEffectiveGapBottom(row),
                RoundedAdaptiveStackPanel stack => stack.StartsCollapsed && stack.CollapsedHeight <= 0 ? 0 : stack.GapBottom,
                _ => Math.Max(0, item.Margin.Bottom)
            };
        }

        protected virtual bool IsLayoutItemCollapsed(Control item)
        {
            return item switch
            {
                AdaptiveStackRowPanel row => row.StartsCollapsed,
                RoundedAdaptiveStackPanel stack => stack.StartsCollapsed,
                _ => false
            };
        }

        protected virtual AdaptiveStackRowSpringMode GetLayoutItemSpringMode(Control item)
        {
            if (item is AdaptiveStackRowPanel row)
            {
                AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                    string.Equals(existingRule.RowName.Trim(), row.Name, StringComparison.Ordinal));
                return rule?.SpringMode ?? AdaptiveStackRowSpringMode.False;
            }

            if (item is RoundedAdaptiveStackPanel stack)
            {
                return stack.StackSpringMode;
            }

            return AdaptiveStackRowSpringMode.False;
        }

        private int GetEffectiveGapTop(AdaptiveStackRowPanel row)
        {
            return row.GapTop;
        }

        private int GetEffectiveGapBottom(AdaptiveStackRowPanel row)
        {
            return row.StartsCollapsed && row.CollapsedHeight <= 0 ? 0 : row.GapBottom;
        }

        private Dictionary<Control, int> GetVerticalSpringDeltas(
            IReadOnlyList<Control> items,
            int availableHeight)
        {
            Dictionary<Control, int> deltas = new();

            int usedHeight = 0;
            foreach (Control item in items)
            {
                usedHeight += GetEffectiveLayoutItemGapTop(item);
                usedHeight += GetEffectiveLayoutItemHeight(item);
                usedHeight += GetEffectiveLayoutItemGapBottom(item);
            }

            int delta = availableHeight - usedHeight;
            if (delta == 0)
            {
                return deltas;
            }

            List<Control> eligibleItems = items
                .Where(item => !IsLayoutItemCollapsed(item))
                .Where(item => CanSpringInDirection(GetLayoutItemSpringMode(item), delta))
                .ToList();

            if (eligibleItems.Count == 0)
            {
                return deltas;
            }

            DistributeVerticalSpringDelta(delta, eligibleItems, deltas);
            return deltas;
        }

        private static bool CanSpringInDirection(AdaptiveStackRowSpringMode springMode, int delta)
        {
            return delta > 0
                ? springMode is AdaptiveStackRowSpringMode.True or AdaptiveStackRowSpringMode.ExpandOnly
                : springMode is AdaptiveStackRowSpringMode.True or AdaptiveStackRowSpringMode.ContractOnly;
        }

        private void DistributeVerticalSpringDelta(
            int delta,
            IReadOnlyList<Control> eligibleItems,
            Dictionary<Control, int> deltas)
        {
            int direction = Math.Sign(delta);
            int remaining = Math.Abs(delta);
            int itemCount = eligibleItems.Count;
            if (itemCount == 0 || remaining == 0)
            {
                return;
            }

            int evenShare = remaining / itemCount;
            int remainder = remaining % itemCount;
            foreach (Control item in eligibleItems)
            {
                int share = evenShare + (remainder > 0 ? 1 : 0);
                if (remainder > 0)
                {
                    remainder--;
                }

                if (direction < 0)
                {
                    int currentHeight = GetEffectiveLayoutItemHeight(item);
                    share = Math.Min(share, currentHeight);
                }

                if (share > 0)
                {
                    deltas[item] = share * direction;
                }
            }
        }

        protected bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime
                || Site?.DesignMode == true
                || Parent?.Site?.DesignMode == true;
        }

        private bool ShouldSerializeRowOrder()
        {
            return _rowOrder.Count > 0;
        }

        private void ResetRowOrder()
        {
            _rowOrder.Clear();
        }

        private void SetRowDragHandlers(AdaptiveStackRowPanel row, bool enabled)
        {
            row.MouseDown -= RowDrag_MouseDown;
            row.MouseMove -= RowDrag_MouseMove;
            row.MouseUp -= RowDrag_MouseUp;
            row.ControlAdded -= RowDrag_ControlAdded;
            row.ControlRemoved -= RowDrag_ControlRemoved;

            foreach (Control child in row.Controls)
            {
                SetChildDragHandlers(child, enabled);
            }

            if (!enabled)
            {
                return;
            }

            row.MouseDown += RowDrag_MouseDown;
            row.MouseMove += RowDrag_MouseMove;
            row.MouseUp += RowDrag_MouseUp;
            row.ControlAdded += RowDrag_ControlAdded;
            row.ControlRemoved += RowDrag_ControlRemoved;
        }

        private void SetHostDragHandlers(bool enabled)
        {
            MouseDown -= HostDrag_MouseDown;
            foreach (AdaptiveStackRowPanel row in Controls.OfType<AdaptiveStackRowPanel>())
            {
                SetRowHostDragHandler(row, enabled);
            }

            if (enabled)
            {
                MouseDown += HostDrag_MouseDown;
            }
        }

        private void SetRowHostDragHandler(AdaptiveStackRowPanel row, bool enabled)
        {
            row.MouseDown -= HostDrag_MouseDown;

            if (enabled)
            {
                row.MouseDown += HostDrag_MouseDown;
            }
        }

        private void HostDrag_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_dragHostForm || _enableRowDragReorder || e.Button != MouseButtons.Left || IsInDesignMode())
            {
                return;
            }

            if (FindForm() is RoundedForm roundedForm)
            {
                roundedForm.BeginFormDrag();
            }
        }

        private void SetChildDragHandlers(Control child, bool enabled)
        {
            child.MouseDown -= RowDrag_MouseDown;
            child.MouseMove -= RowDrag_MouseMove;
            child.MouseUp -= RowDrag_MouseUp;

            if (!enabled)
            {
                return;
            }

            child.MouseDown += RowDrag_MouseDown;
            child.MouseMove += RowDrag_MouseMove;
            child.MouseUp += RowDrag_MouseUp;
        }

        private void RowDrag_ControlAdded(object? sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                SetChildDragHandlers(e.Control, _enableRowDragReorder);
            }
        }

        private void RowDrag_ControlRemoved(object? sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                SetChildDragHandlers(e.Control, enabled: false);
            }
        }

        private void RowDrag_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_enableRowDragReorder || e.Button != MouseButtons.Left || IsInDesignMode())
            {
                return;
            }

            _dragRow = FindRowFromDragSender(sender);
            _dragStartScreenPoint = Control.MousePosition;
            _isRowDragActive = false;
        }

        private void RowDrag_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_enableRowDragReorder || _dragRow == null || e.Button != MouseButtons.Left || IsInDesignMode())
            {
                return;
            }

            Point mouse = Control.MousePosition;
            Size dragSize = SystemInformation.DragSize;
            Rectangle dragBounds = new Rectangle(
                _dragStartScreenPoint.X - dragSize.Width / 2,
                _dragStartScreenPoint.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);

            if (!_isRowDragActive && dragBounds.Contains(mouse))
            {
                return;
            }

            _isRowDragActive = true;
            Cursor.Current = Cursors.SizeAll;

            Point clientPoint = PointToClient(mouse);
            int targetIndex = GetRowDropIndex(clientPoint.Y);
            int currentIndex = GetRows().ToList().IndexOf(_dragRow);
            if (targetIndex < 0 || currentIndex < 0 || targetIndex == currentIndex)
            {
                return;
            }

            if (MoveRowTo(_dragRow, targetIndex))
            {
                RowDragReordered?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RowDrag_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_dragRow != null)
            {
                Cursor.Current = Cursors.Default;
            }

            _dragRow = null;
            _isRowDragActive = false;
        }

        private AdaptiveStackRowPanel? FindRowFromDragSender(object? sender)
        {
            Control? control = sender as Control;
            while (control != null && !ReferenceEquals(control, this))
            {
                if (control is AdaptiveStackRowPanel row && ReferenceEquals(row.Parent, this))
                {
                    return row;
                }

                control = control.Parent;
            }

            return null;
        }

        private int GetRowDropIndex(int clientY)
        {
            AdaptiveStackRowPanel[] rows = GetRows().ToArray();
            if (rows.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                AdaptiveStackRowPanel row = rows[i];
                int midpoint = row.Top + row.Height / 2;
                if (clientY < midpoint)
                {
                    return i;
                }
            }

            return rows.Length - 1;
        }

        private void NotifyHostSizeHintChanged(bool force = false)
        {
            if (!_requestHostFormDpiHeightExpansion || IsInDesignMode() || IsDisposed || Disposing)
            {
                return;
            }

            int contentHeight = GetVisibleContentHeight();
            Rectangle currentBounds = Bounds;
            if (!force
                && contentHeight == _lastHostSizeHintContentHeight
                && currentBounds == _lastHostSizeHintBounds)
            {
                return;
            }

            _lastHostSizeHintContentHeight = contentHeight;
            _lastHostSizeHintBounds = currentBounds;
            HostSizeHintChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool HasUsableRowRules()
        {
            return _rowRules.Any(rule => IsValidComponentName(rule.RowName.Trim()));
        }

        private void TrackRowNamesAndRenames(AdaptiveStackRowPanel[] rows)
        {
            if (rows.Length == 0)
            {
                return;
            }

            AdaptiveStackRowPanel[] visualRows = rows
                .OrderBy(row => row.TabIndex)
                .ThenBy(row => row.Top)
                .ToArray();

            foreach (AdaptiveStackRowPanel row in visualRows)
            {
                if (!_knownRowNames.TryGetValue(row, out string? oldName))
                {
                    _knownRowNames[row] = row.Name;
                    continue;
                }

                string newName = row.Name;
                if (string.Equals(oldName, newName, StringComparison.Ordinal))
                {
                    continue;
                }

                ReplaceRowNameInOrder(row, oldName, newName, visualRows);
                RenameRowRuleForExternalRowRename(oldName, newName);
                _knownRowNames[row] = newName;
            }
        }

        private void SyncRowOrder(AdaptiveStackRowPanel[] rows)
        {
            if (rows.Length == 0)
            {
                return;
            }

            TrackRowNamesAndRenames(rows);

            AdaptiveStackRowPanel[] visualRows = rows
                .OrderBy(row => row.TabIndex)
                .ThenBy(row => row.Top)
                .ToArray();

            HashSet<string> rowNames = rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .Select(row => row.Name)
                .ToHashSet(StringComparer.Ordinal);

            _rowOrder.RemoveAll(rowName => !rowNames.Contains(rowName));

            foreach (AdaptiveStackRowPanel row in visualRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Name) && !_rowOrder.Contains(row.Name, StringComparer.Ordinal))
                {
                    _rowOrder.Add(row.Name);
                }
            }
        }

        private void RenameRowRuleForExternalRowRename(string? oldName, string? newName)
        {
            if (_isSyncingRowRules
                || string.IsNullOrWhiteSpace(oldName)
                || string.IsNullOrWhiteSpace(newName)
                || !IsValidComponentName(newName))
            {
                return;
            }

            AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.RowName.Trim(), oldName, StringComparison.Ordinal));

            if (rule == null)
            {
                return;
            }

            bool newNameAlreadyHasRule = _rowRules.Any(existingRule =>
                !ReferenceEquals(existingRule, rule)
                && string.Equals(existingRule.RowName.Trim(), newName, StringComparison.Ordinal));

            if (newNameAlreadyHasRule)
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                rule.RowName = newName;
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, oldName, newName);
        }

        private void ApplyRowOrderToTabIndexes()
        {
            Control[] items = GetStackItems().ToArray();
            Dictionary<string, Control> itemsByName = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            for (int i = 0; i < _rowOrder.Count; i++)
            {
                if (itemsByName.TryGetValue(_rowOrder[i], out Control? item))
                {
                    int target = i * 2;
                    if (item.TabIndex != target)
                    {
                        // Set through the property descriptor so the designer serializes the new
                        // TabIndex. A direct "row.TabIndex = ..." can repaint in memory and revert later.
                        IComponentChangeService? changeService =
                            (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
                        PropertyDescriptor? tabIndexProp = TypeDescriptor.GetProperties(item)["TabIndex"];
                        int oldValue = item.TabIndex;

                        changeService?.OnComponentChanging(item, tabIndexProp);
                        if (tabIndexProp != null)
                        {
                            tabIndexProp.SetValue(item, target);
                        }
                        else
                        {
                            item.TabIndex = target;
                        }

                        changeService?.OnComponentChanged(item, tabIndexProp, oldValue, target);
                    }
                }
            }
        }

        private string EnsureRowName(AdaptiveStackRowPanel row)
        {
            if (!string.IsNullOrWhiteSpace(row.Name))
            {
                _knownRowNames[row] = row.Name;
                return row.Name;
            }

            string baseName = "adaptiveStackRowPanel";
            HashSet<string> names = GetDesignerComponentNames();
            foreach (string name in Controls
                .OfType<AdaptiveStackRowPanel>()
                .Where(existingRow => !string.IsNullOrWhiteSpace(existingRow.Name))
                .Select(existingRow => existingRow.Name))
            {
                names.Add(name);
            }

            int index = names.Count + 1;
            string candidate;
            do
            {
                candidate = $"{baseName}{index++}";
            }
            while (names.Contains(candidate));

            row.Name = candidate;
            _knownRowNames[row] = candidate;
            return candidate;
        }

        private void ReplaceRowNameInOrder(AdaptiveStackRowPanel row, string? oldName, string newName, AdaptiveStackRowPanel[] visualRows)
        {
            if (!string.IsNullOrWhiteSpace(oldName))
            {
                int index = _rowOrder.FindIndex(name => string.Equals(name, oldName, StringComparison.Ordinal));
                if (index >= 0)
                {
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        _rowOrder.RemoveAt(index);
                    }
                    else if (!_rowOrder.Contains(newName, StringComparer.Ordinal))
                    {
                        _rowOrder[index] = newName;
                    }
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(newName) || _rowOrder.Contains(newName, StringComparer.Ordinal))
            {
                return;
            }

            int visualIndex = Array.IndexOf(visualRows, row);
            int insertIndex = Math.Clamp(visualIndex, 0, _rowOrder.Count);
            _rowOrder.Insert(insertIndex, newName);
        }

        private void ReplaceItemNameInOrder(Control item, string? oldName, string newName, Control[] visualItems)
        {
            if (!string.IsNullOrWhiteSpace(oldName))
            {
                int index = _rowOrder.FindIndex(name => string.Equals(name, oldName, StringComparison.Ordinal));
                if (index >= 0)
                {
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        _rowOrder.RemoveAt(index);
                    }
                    else if (!_rowOrder.Contains(newName, StringComparer.Ordinal))
                    {
                        _rowOrder[index] = newName;
                    }
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(newName) || _rowOrder.Contains(newName, StringComparer.Ordinal))
            {
                return;
            }

            int visualIndex = Array.IndexOf(visualItems, item);
            int insertIndex = Math.Clamp(visualIndex, 0, _rowOrder.Count);
            _rowOrder.Insert(insertIndex, newName);
        }

        private void TrackRowName(AdaptiveStackRowPanel row)
        {
            _knownRowNames[row] = row.Name;
        }

        private void UntrackRowName(AdaptiveStackRowPanel row)
        {
            _knownRowNames.Remove(row);
        }

        internal void OnRowRulesChanged()
        {
            RequestRowRulesSync();
        }

        internal virtual AdaptiveStackRowRule NormalizeRowRuleForCollection(AdaptiveStackRowRule rule)
        {
            return rule;
        }

        private static bool RuleCreatesRoundedStack(AdaptiveStackRowRule rule)
        {
            return rule.RowType == RoundedAdaptiveStackRowType.RAS;
        }

        private static bool StackItemMatchesRule(AdaptiveStackRowRule rule, Control item)
        {
            return RuleCreatesRoundedStack(rule)
                ? item is RoundedAdaptiveStackPanel
                : item is AdaptiveStackRowPanel;
        }

        internal void PrepareNewRowRule(AdaptiveStackRowRule rule)
        {
            if (this is not RoundedAdaptiveStackPanel && rule.RowType != RoundedAdaptiveStackRowType.ARP)
            {
                rule.RowType = RoundedAdaptiveStackRowType.ARP;
            }

            if (string.IsNullOrWhiteSpace(rule.RowName))
            {
                string prefix = RuleCreatesRoundedStack(rule) ? "stack" : "row";
                rule.RowName = GetNextRowRuleName(prefix, rule);
            }

            if (rule.RowHeight <= 0)
            {
                rule.RowHeight = RuleCreatesRoundedStack(rule) ? GetDefaultNestedStackHeight() : _defaultRowHeight;
            }

            if (RuleCreatesRoundedStack(rule) && (rule.RowHeight == 36 || rule.RowHeight == LegacyNestedRoundedStackHeight))
            {
                rule.RowHeight = GetDefaultNestedStackHeight();
            }

            if (rule.GapBottom == 4 && _defaultGapBottom != 4)
            {
                rule.GapBottom = _defaultGapBottom;
            }
        }

        internal void OnRowRuleNameChanged(AdaptiveStackRowRule rule, string oldRowName, string newRowName)
        {
            if (!IsInDesignMode() || _isSyncingRowRules)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(oldRowName)
                && IsValidComponentName(newRowName)
                && FindStackItem(oldRowName) is Control oldItem
                && FindStackItem(newRowName) == null
                && IsDesignerComponentNameAvailable(newRowName, oldItem))
            {
                RenameDesignerStackItem(oldItem, newRowName);
                ApplyRuleToStackItem(rule, oldItem);
                PerformStackLayout();
                return;
            }

            RequestRowRulesSync();
        }

        internal void RemoveRowRuleChild(AdaptiveStackRowRule rule)
        {
            if (!IsInDesignMode() || _isSyncingRowRules)
            {
                return;
            }

            string itemName = rule.RowName.Trim();
            if (!IsValidComponentName(itemName) || FindStackItem(itemName) is not Control item)
            {
                return;
            }

            _isSyncingRowRules = true;
            try
            {
                DestroyDesignerStackItem(item);
                _rowOrder.Remove(itemName);
                PerformStackLayout();
            }
            finally
            {
                _isSyncingRowRules = false;
            }
        }

        internal void SyncRuleFromRow(AdaptiveStackRowPanel row)
        {
            if (!IsInDesignMode() || _isSyncingRowRules || string.IsNullOrWhiteSpace(row.Name))
            {
                return;
            }

            AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.RowName, row.Name, StringComparison.Ordinal));

            if (rule == null)
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                rule.RowHeight = row.RowHeight;
                rule.CollapsedHeight = row.CollapsedHeight;
                rule.GapTop = row.GapTop;
                rule.GapBottom = row.GapBottom;
                rule.StartsCollapsed = row.StartsCollapsed;
                rule.StretchChildHeight = row.StretchChildHeight;
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, null, _rowRules);
        }

        internal void SyncRuleFromNestedStack(RoundedAdaptiveStackPanel stack)
        {
            if (!IsInDesignMode() || _isSyncingRowRules || string.IsNullOrWhiteSpace(stack.Name))
            {
                return;
            }

            AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.RowName, stack.Name, StringComparison.Ordinal));

            if (rule == null)
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                if (rule is RoundedAdaptiveStackRowRule roundedRule)
                {
                    roundedRule.RowType = RoundedAdaptiveStackRowType.RAS;
                }

                rule.RowHeight = stack.StackHeight;
                rule.CollapsedHeight = stack.CollapsedHeight;
                rule.GapTop = stack.GapTop;
                rule.GapBottom = stack.GapBottom;
                rule.StartsCollapsed = stack.StartsCollapsed;
                rule.SpringMode = stack.StackSpringMode;
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, null, _rowRules);
        }

        internal void AdoptExternalRowIntoRowRules(AdaptiveStackRowPanel row)
        {
            if (!IsInDesignMode() || _isSyncingRowRules || string.IsNullOrWhiteSpace(row.Name))
            {
                return;
            }

            if (_rowRules.Any(rule => string.Equals(rule.RowName, row.Name, StringComparison.Ordinal)))
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                AdaptiveStackRowRule rule = this is RoundedAdaptiveStackPanel
                    ? new RoundedAdaptiveStackRowRule { RowType = RoundedAdaptiveStackRowType.ARP }
                    : new AdaptiveStackRowRule();

                rule.RowName = row.Name;
                rule.RowHeight = Math.Max(1, row.RowHeight);
                rule.CollapsedHeight = Math.Max(0, row.CollapsedHeight);
                rule.GapTop = Math.Max(0, row.GapTop);
                rule.GapBottom = Math.Max(0, row.GapBottom);
                rule.StartsCollapsed = row.StartsCollapsed;
                rule.StretchChildHeight = row.StretchChildHeight;

                _rowRules.Add(rule);
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, null, _rowRules);
        }

        internal void RemoveExternalRowFromRowRules(AdaptiveStackRowPanel row)
        {
            if (!IsInDesignMode() || _isSyncingRowRules || string.IsNullOrWhiteSpace(row.Name))
            {
                return;
            }

            AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.RowName, row.Name, StringComparison.Ordinal));

            if (rule == null)
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                _rowRules.Remove(rule);
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, null, _rowRules);
        }

        internal void AdoptExternalNestedStackIntoRowRules(RoundedAdaptiveStackPanel stack)
        {
            if (!IsInDesignMode() || _isSyncingRowRules || string.IsNullOrWhiteSpace(stack.Name))
            {
                return;
            }

            if (_rowRules.Any(rule => string.Equals(rule.RowName, stack.Name, StringComparison.Ordinal)))
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                _rowRules.Add(new RoundedAdaptiveStackRowRule
                {
                    RowType = RoundedAdaptiveStackRowType.RAS,
                    RowName = stack.Name,
                    RowHeight = Math.Max(1, stack.StackHeight),
                    CollapsedHeight = Math.Max(0, stack.CollapsedHeight),
                    GapTop = Math.Max(0, stack.GapTop),
                    GapBottom = Math.Max(0, stack.GapBottom),
                    StartsCollapsed = stack.StartsCollapsed,
                    SpringMode = stack.StackSpringMode
                });
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, null, _rowRules);
        }

        internal void RemoveExternalNestedStackFromRowRules(RoundedAdaptiveStackPanel stack)
        {
            if (!IsInDesignMode() || _isSyncingRowRules || string.IsNullOrWhiteSpace(stack.Name))
            {
                return;
            }

            AdaptiveStackRowRule? rule = _rowRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.RowName, stack.Name, StringComparison.Ordinal));

            if (rule == null)
            {
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowRulesProperty = TypeDescriptor.GetProperties(this)[nameof(RowRules)];

            changeService?.OnComponentChanging(this, rowRulesProperty);
            _isSyncingRowRules = true;
            try
            {
                _rowRules.Remove(rule);
            }
            finally
            {
                _isSyncingRowRules = false;
            }

            changeService?.OnComponentChanged(this, rowRulesProperty, null, _rowRules);
        }

        private void RequestRowRulesSync()
        {
            if (!IsInDesignMode() || _isSyncingRowRules || _rowRulesSyncPending)
            {
                return;
            }

            _rowRulesSyncPending = true;

            if (IsHandleCreated)
            {
                BeginInvoke(new Action(SyncRowsFromRules));
            }
            else
            {
                SyncRowsFromRules();
            }
        }

        private void SyncRowsFromRules()
        {
            if (_isSyncingRowRules)
            {
                return;
            }

            _rowRulesSyncPending = false;

            if (!IsInDesignMode())
            {
                return;
            }

            _isSyncingRowRules = true;
            try
            {
                RemoveDesignerItemsMissingFromRules();

                foreach (AdaptiveStackRowRule rule in _rowRules)
                {
                    string itemName = rule.RowName.Trim();
                    if (!IsValidComponentName(itemName))
                    {
                        continue;
                    }

                    Control? item = FindStackItem(itemName);
                    if (item != null && !StackItemMatchesRule(rule, item))
                    {
                        DestroyDesignerStackItem(item);
                        item = null;
                    }

                    if (item == null && !IsDesignerComponentNameAvailable(itemName))
                    {
                        itemName = GetNextRowRuleName(RuleCreatesRoundedStack(rule) ? "stack" : "row", rule);
                        rule.RowName = itemName;
                    }

                    item ??= CreateDesignerStackItem(rule, itemName);
                    if (item == null)
                    {
                        continue;
                    }

                    // If the designer host assigned a different name (conflict resolved at creation time),
                    // update the rule so the Designer.cs and rule stay in sync.
                    string resolvedItemName = item.Name;
                    if (!string.Equals(itemName, resolvedItemName, StringComparison.Ordinal) && IsValidComponentName(resolvedItemName))
                    {
                        rule.RowName = resolvedItemName;
                        itemName = resolvedItemName;
                    }
                    ApplyRuleToStackItem(rule, item);
                }

                ApplyRowRuleOrderToStackItems();
                PerformStackLayout();
            }
            finally
            {
                _isSyncingRowRules = false;
            }
        }

        private Control? FindStackItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return null;
            }

            return GetStackItems().FirstOrDefault(item =>
                string.Equals(item.Name, itemName, StringComparison.Ordinal));
        }

        private void RemoveDesignerItemsMissingFromRules()
        {
            HashSet<string> ruleNames = _rowRules
                .Select(rule => rule.RowName.Trim())
                .Where(IsValidComponentName)
                .ToHashSet(StringComparer.Ordinal);

            Control[] orphanItems = GetStackItems()
                .Where(item => IsValidComponentName(item.Name) && !ruleNames.Contains(item.Name))
                .ToArray();

            foreach (Control item in orphanItems)
            {
                string itemName = item.Name;
                DestroyDesignerStackItem(item);
                _rowOrder.Remove(itemName);
            }
        }

        private Control? CreateDesignerStackItem(AdaptiveStackRowRule rule, string itemName)
        {
            if (RuleCreatesRoundedStack(rule))
            {
                return this is RoundedAdaptiveStackPanel ? CreateDesignerNestedStack(itemName) : null;
            }

            return CreateDesignerRow(itemName);
        }

        private AdaptiveStackRowPanel CreateDesignerRow(string rowName)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];

            AdaptiveStackRowPanel? row = null;
            if (designerHost != null)
            {
                try
                {
                    row = designerHost.CreateComponent(typeof(AdaptiveStackRowPanel), rowName) as AdaptiveStackRowPanel;
                }
                catch
                {
                    // Name was rejected by the designer naming service; let the host assign a unique name.
                    try { row = designerHost.CreateComponent(typeof(AdaptiveStackRowPanel)) as AdaptiveStackRowPanel; }
                    catch { row = null; }
                }
            }

            row ??= new AdaptiveStackRowPanel { Name = rowName };

            row.RowHeight = _defaultRowHeight;
            row.GapBottom = _defaultGapBottom;
            row.StretchChildHeight = _stretchChildHeight;
            row.Margin = Padding.Empty;
            row.Width = Math.Max(1, Width - Padding.Left - Padding.Right);
            row.Height = Math.Max(1, _defaultRowHeight);
            row.TabIndex = GetRows().Count * 2;

            changeService?.OnComponentChanging(this, controlsProperty);
            Controls.Add(row);
            changeService?.OnComponentChanged(this, controlsProperty, null, row);

            return row;
        }

        private RoundedAdaptiveStackPanel? CreateDesignerNestedStack(string stackName)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];

            RoundedAdaptiveStackPanel? stack = null;
            if (designerHost != null)
            {
                try
                {
                    stack = designerHost.CreateComponent(typeof(RoundedAdaptiveStackPanel), stackName) as RoundedAdaptiveStackPanel;
                }
                catch
                {
                    try { stack = designerHost.CreateComponent(typeof(RoundedAdaptiveStackPanel)) as RoundedAdaptiveStackPanel; }
                    catch { stack = null; }
                }
            }

            stack ??= new RoundedAdaptiveStackPanel { Name = stackName };

            stack.StackHeight = GetDefaultNestedStackHeight();
            stack.CollapsedHeight = 36;
            stack.GapBottom = _defaultGapBottom;
            stack.Margin = Padding.Empty;
            stack.Width = Math.Max(1, Width - Padding.Left - Padding.Right);
            stack.Height = Math.Max(1, stack.StackHeight);
            stack.TabIndex = GetStackItems().Count * 2;

            changeService?.OnComponentChanging(this, controlsProperty);
            Controls.Add(stack);
            changeService?.OnComponentChanged(this, controlsProperty, null, stack);

            return stack;
        }

        private void DestroyDesignerRow(AdaptiveStackRowPanel row)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];

            changeService?.OnComponentChanging(this, controlsProperty);
            Controls.Remove(row);
            if (designerHost != null)
            {
                designerHost.DestroyComponent(row);
            }
            else
            {
                row.Dispose();
            }
            changeService?.OnComponentChanged(this, controlsProperty, row, null);
        }

        private void DestroyDesignerStackItem(Control item)
        {
            if (item is AdaptiveStackRowPanel row)
            {
                DestroyDesignerRow(row);
                return;
            }

            if (item is RoundedAdaptiveStackPanel stack)
            {
                DestroyDesignerNestedStack(stack);
            }
        }

        private void DestroyDesignerNestedStack(RoundedAdaptiveStackPanel stack)
        {
            IDesignerHost? designerHost = (IDesignerHost?)Site?.GetService(typeof(IDesignerHost));
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(this)["Controls"];

            changeService?.OnComponentChanging(this, controlsProperty);
            Controls.Remove(stack);
            if (designerHost != null)
            {
                designerHost.DestroyComponent(stack);
            }
            else
            {
                stack.Dispose();
            }
            changeService?.OnComponentChanged(this, controlsProperty, stack, null);
        }

        private void RenameDesignerRow(AdaptiveStackRowPanel row, string newRowName)
        {
            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? nameProperty = TypeDescriptor.GetProperties(row)[nameof(Control.Name)];
            string oldName = row.Name;

            changeService?.OnComponentChanging(row, nameProperty);
            if (nameProperty != null)
            {
                nameProperty.SetValue(row, newRowName);
            }
            else
            {
                row.Name = newRowName;
            }
            changeService?.OnComponentChanged(row, nameProperty, oldName, newRowName);

            ReplaceRowNameInOrder(row, oldName, newRowName, GetRows().ToArray());
            _knownRowNames[row] = newRowName;
        }

        private void RenameDesignerStackItem(Control item, string newItemName)
        {
            if (item is AdaptiveStackRowPanel row)
            {
                RenameDesignerRow(row, newItemName);
                return;
            }

            IComponentChangeService? changeService = (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? nameProperty = TypeDescriptor.GetProperties(item)[nameof(Control.Name)];
            string oldName = item.Name;

            changeService?.OnComponentChanging(item, nameProperty);
            if (nameProperty != null)
            {
                nameProperty.SetValue(item, newItemName);
            }
            else
            {
                item.Name = newItemName;
            }
            changeService?.OnComponentChanged(item, nameProperty, oldName, newItemName);

            ReplaceItemNameInOrder(item, oldName, newItemName, GetStackItems().ToArray());
        }

        private static void ApplyRuleToRow(AdaptiveStackRowRule rule, AdaptiveStackRowPanel row)
        {
            row.RowHeight = rule.RowHeight;
            row.CollapsedHeight = rule.CollapsedHeight;
            row.GapTop = rule.GapTop;
            row.GapBottom = rule.GapBottom;
            row.StartsCollapsed = rule.StartsCollapsed;
            row.StretchChildHeight = rule.StretchChildHeight;
        }

        private static void ApplyRuleToStackItem(AdaptiveStackRowRule rule, Control item)
        {
            if (item is AdaptiveStackRowPanel row)
            {
                ApplyRuleToRow(rule, row);
                return;
            }

            if (item is RoundedAdaptiveStackPanel stack)
            {
                stack.StackHeight = Math.Max(1, rule.RowHeight);
                stack.CollapsedHeight = Math.Max(0, rule.CollapsedHeight);
                stack.GapTop = Math.Max(0, rule.GapTop);
                stack.GapBottom = Math.Max(0, rule.GapBottom);
                stack.StartsCollapsed = rule.StartsCollapsed;
                stack.StackSpringMode = rule.SpringMode;
            }
        }

        private string GetNextRowRuleName(string namePrefix, AdaptiveStackRowRule currentRule)
        {
            HashSet<string> usedNames = GetDesignerComponentNames();

            foreach (string name in GetStackItems()
                .Select(item => item.Name)
                .Concat(_rowRules
                    .Where(rule => !ReferenceEquals(rule, currentRule))
                    .Select(rule => rule.RowName))
                .Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                usedNames.Add(name);
            }

            for (int index = 1; index < 10000; index++)
            {
                string candidate = $"{namePrefix}{index}";
                if (!usedNames.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{namePrefix}{Controls.Count + _rowRules.Count + 1}";
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

        private void ApplyRowRuleOrderToStackItems()
        {
            Control[] existingItems = GetStackItems()
                .ToArray();

            HashSet<string> existingNames = existingItems
                .Where(item => IsValidComponentName(item.Name))
                .Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);

            List<string> orderedNames = _rowRules
                .Select(rule => rule.RowName.Trim())
                .Where(name => IsValidComponentName(name) && existingNames.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            orderedNames.AddRange(existingItems
                .OrderBy(item => item.TabIndex)
                .ThenBy(item => item.Top)
                .Select(item => item.Name)
                .Where(name => IsValidComponentName(name) && !orderedNames.Contains(name, StringComparer.Ordinal)));

            string oldRowOrder = RowOrder;
            _rowOrder.Clear();
            _rowOrder.AddRange(orderedNames);
            ApplyRowOrderToDesigner(oldRowOrder);
        }

        private void ApplyRowOrderToDesigner(string? oldRowOrder = null)
        {
            ApplyRowOrderToTabIndexes();
            NotifyRowOrderSerialized(oldRowOrder);
        }

        // Marks the RowOrder property dirty so the reordered value is written back to the
        // .Designer.cs. RowOrder is what drives the on-screen order at runtime, so without this a
        // rules reorder updates the panel in memory but reverts on the next rebuild.
        private void NotifyRowOrderSerialized(string? oldRowOrder = null)
        {
            IComponentChangeService? changeService =
                (IComponentChangeService?)Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? rowOrderProperty = TypeDescriptor.GetProperties(this)[nameof(RowOrder)];
            if (changeService == null || rowOrderProperty == null)
            {
                return;
            }

            changeService.OnComponentChanging(this, rowOrderProperty);
            changeService.OnComponentChanged(this, rowOrderProperty, oldRowOrder, RowOrder);
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
    public class AdaptiveStackRowRule : INotifyPropertyChanged
    {
        private string _rowName = string.Empty;
        private int _rowHeight = 36;
        private int _collapsedHeight;
        private int _gapTop;
        private int _gapBottom = 4;
        private bool _startsCollapsed;
        private RoundedAdaptiveStackRowType _rowType = RoundedAdaptiveStackRowType.ARP;
        private bool _stretchChildHeight = true;
        private AdaptiveStackRowSpringMode _springMode = AdaptiveStackRowSpringMode.False;

        public event PropertyChangedEventHandler? PropertyChanged;

        [DefaultValue(RoundedAdaptiveStackRowType.ARP)]
        [Category("Row")]
        [Description("The child control type this rule creates when used by a RoundedAdaptiveStackPanel.")]
        public RoundedAdaptiveStackRowType RowType
        {
            get => _rowType;
            set
            {
                if (_rowType == value)
                {
                    return;
                }

                SetField(ref _rowType, value, nameof(RowType));
                if (value == RoundedAdaptiveStackRowType.RAS && RowHeight == 36)
                {
                    RowHeight = AdaptiveStackPanel.GetNestedStackHeightForRowHeight(36);
                    CollapsedHeight = 36;
                }
                else if (value == RoundedAdaptiveStackRowType.ARP
                    && (RowHeight == AdaptiveStackPanel.GetNestedStackHeightForRowHeight(36)
                        || RowHeight == AdaptiveStackPanel.LegacyNestedRoundedStackHeight))
                {
                    RowHeight = 36;
                }
            }
        }

        [Category("Row")]
        [Description("Name of the AdaptiveStackRowPanel this rule describes.")]
        public string RowName
        {
            get => _rowName;
            set
            {
                string newValue = value ?? string.Empty;
                if (string.Equals(_rowName, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                _rowName = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowName)));
            }
        }

        [DefaultValue(36)]
        [Category("Row")]
        [Description("Authored height for the matching AdaptiveStackRowPanel.")]
        public int RowHeight
        {
            get => _rowHeight;
            set => SetField(ref _rowHeight, Math.Max(0, value), nameof(RowHeight));
        }

        [DefaultValue(0)]
        [Category("Row")]
        [Description("Authored height used when the matching row is collapsed. Use 0 to hide the row completely.")]
        public int CollapsedHeight
        {
            get => _collapsedHeight;
            set => SetField(ref _collapsedHeight, Math.Max(0, value), nameof(CollapsedHeight));
        }

        [DefaultValue(0)]
        [Category("Row")]
        [Description("Space above the matching row.")]
        public int GapTop
        {
            get => _gapTop;
            set => SetField(ref _gapTop, Math.Max(0, value), nameof(GapTop));
        }

        [DefaultValue(4)]
        [Category("Row")]
        [Description("Space below the matching row.")]
        public int GapBottom
        {
            get => _gapBottom;
            set => SetField(ref _gapBottom, Math.Max(0, value), nameof(GapBottom));
        }

        [DefaultValue(false)]
        [Category("Row")]
        [Description("When true, the matching row starts collapsed.")]
        public bool StartsCollapsed
        {
            get => _startsCollapsed;
            set => SetField(ref _startsCollapsed, value, nameof(StartsCollapsed));
        }

        [DefaultValue(true)]
        [Category("Row")]
        [Description("When true, controls in the matching row stretch to the row height.")]
        public bool StretchChildHeight
        {
            get => _stretchChildHeight;
            set => SetField(ref _stretchChildHeight, value, nameof(StretchChildHeight));
        }

        [DefaultValue(AdaptiveStackRowSpringMode.False)]
        [Category("Vertical Spring")]
        [Description("Controls whether this row absorbs available vertical space in the stack.")]
        public AdaptiveStackRowSpringMode SpringMode
        {
            get => _springMode;
            set => SetField(ref _springMode, value, nameof(SpringMode));
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(RowName) ? "(row rule)" : RowName;
        }

        protected void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class RoundedAdaptiveStackRowRule : AdaptiveStackRowRule
    {
        public RoundedAdaptiveStackRowRule()
        {
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(RowName) ? $"({RowType} rule)" : $"{RowName} ({RowType})";
        }
    }

    [Obsolete("Use RoundedAdaptiveStackRowRule with RowType = RAS.")]
    public class RoundedAdaptiveStackRule : RoundedAdaptiveStackRowRule
    {
        public RoundedAdaptiveStackRule()
        {
            RowType = RoundedAdaptiveStackRowType.RAS;
        }
    }

    public class AdaptiveStackRuleCollectionEditor : CollectionEditor
    {
        public AdaptiveStackRuleCollectionEditor(IServiceProvider provider, Type collectionType) : base(provider, collectionType)
        {
        }

        protected override Type[] CreateNewItemTypes()
        {
            return new[] { typeof(AdaptiveStackRowRule) };
        }
    }

    public class RoundedAdaptiveStackRowRuleCollectionEditor : CollectionEditor
    {
        public RoundedAdaptiveStackRowRuleCollectionEditor(IServiceProvider provider, Type collectionType) : base(provider, collectionType)
        {
        }

        protected override object CreateInstance(Type itemType)
        {
            Type actualType = itemType == typeof(AdaptiveStackRowRule)
                ? typeof(RoundedAdaptiveStackRowRule)
                : itemType;

            return base.CreateInstance(actualType);
        }

        protected override Type[] CreateNewItemTypes()
        {
            return new[] { typeof(RoundedAdaptiveStackRowRule) };
        }
    }

    public class AdaptiveStackRowRuleCollection : Collection<AdaptiveStackRowRule>
    {
        private readonly Dictionary<AdaptiveStackRowRule, string> _knownRowNames = new();

        internal AdaptiveStackPanel? Owner { get; set; }

        protected override void InsertItem(int index, AdaptiveStackRowRule item)
        {
            item = Owner?.NormalizeRowRuleForCollection(item) ?? item;
            Owner?.PrepareNewRowRule(item);

            if (FindRuleByRowName(item.RowName) is AdaptiveStackRowRule existingItem)
            {
                CopyRuleValues(item, existingItem);
                NotifyChanged();
                return;
            }

            base.InsertItem(index, item);
            Subscribe(item);
            _knownRowNames[item] = item.RowName;
            NotifyChanged();
        }

        protected override void SetItem(int index, AdaptiveStackRowRule item)
        {
            item = Owner?.NormalizeRowRuleForCollection(item) ?? item;
            AdaptiveStackRowRule oldItem = this[index];
            Owner?.PrepareNewRowRule(item);

            if (FindRuleByRowName(item.RowName, oldItem) is AdaptiveStackRowRule existingItem)
            {
                CopyRuleValues(item, existingItem);
                RemoveItem(index);
                NotifyChanged();
                return;
            }

            Unsubscribe(oldItem);
            _knownRowNames.Remove(oldItem);
            base.SetItem(index, item);
            Subscribe(item);
            _knownRowNames[item] = item.RowName;
            NotifyChanged();
        }

        protected override void RemoveItem(int index)
        {
            AdaptiveStackRowRule item = this[index];
            Unsubscribe(item);
            _knownRowNames.Remove(item);
            Owner?.RemoveRowRuleChild(item);
            base.RemoveItem(index);
            NotifyChanged();
        }

        protected override void ClearItems()
        {
            AdaptiveStackRowRule[] items = this.ToArray();
            foreach (AdaptiveStackRowRule item in items)
            {
                Unsubscribe(item);
            }

            _knownRowNames.Clear();
            base.ClearItems();
            NotifyChanged();
        }

        private void Subscribe(AdaptiveStackRowRule item)
        {
            item.PropertyChanged += Item_PropertyChanged;
            _knownRowNames[item] = item.RowName;
        }

        private void Unsubscribe(AdaptiveStackRowRule item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is AdaptiveStackRowRule rule && e.PropertyName == nameof(AdaptiveStackRowRule.RowName))
            {
                _knownRowNames.TryGetValue(rule, out string? oldRowName);
                string newRowName = rule.RowName;
                _knownRowNames[rule] = newRowName;
                Owner?.OnRowRuleNameChanged(rule, oldRowName ?? string.Empty, newRowName);
                return;
            }

            if (sender is AdaptiveStackRowRule springRule
                && e.PropertyName == nameof(AdaptiveStackRowRule.SpringMode))
            {
                NotifyChanged();
                return;
            }

            NotifyChanged();
        }

        private void NotifyChanged()
        {
            Owner?.OnRowRulesChanged();
        }

        private AdaptiveStackRowRule? FindRuleByRowName(string rowName, AdaptiveStackRowRule? excludedRule = null)
        {
            string normalizedName = rowName.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return null;
            }

            return this.FirstOrDefault(rule =>
                !ReferenceEquals(rule, excludedRule)
                && string.Equals(rule.RowName.Trim(), normalizedName, StringComparison.Ordinal));
        }

        private static void CopyRuleValues(AdaptiveStackRowRule source, AdaptiveStackRowRule target)
        {
            target.RowType = source.RowType;
            target.RowHeight = source.RowHeight;
            target.CollapsedHeight = source.CollapsedHeight;
            target.GapTop = source.GapTop;
            target.GapBottom = source.GapBottom;
            target.StartsCollapsed = source.StartsCollapsed;
            target.StretchChildHeight = source.StretchChildHeight;
            target.SpringMode = source.SpringMode;
        }
    }
}

/*
AdaptiveStackPanel API quick reference
======================================

Core rule:
    Parent owns row order.
    Rows own their child control layout.

Row order:
    RowOrder stores row names in visual order, separated by "|".
    Row control names do not change when rows move.
    Numeric APIs use the current visual/order index.
    Name APIs target one specific row control.

Open / close / toggle:
    stack.Open(0);
    stack.Open("advancedRow");
    stack.Close(0);
    stack.Toggle("advancedRow");

Collapsed rows:
    Collapsed rows stay in RowOrder.
    They occupy 0 height but still count as rows for indexing.

Reorder:
    stack.MoveUp(2);
    stack.MoveDown(0);
    stack.Swap(0, 2);
    stack.MoveTo(0, 3);

MoveTo:
    MoveTo(0, 3) means:
    take the row currently in slot 0 and place it in slot 3.

Insert:
    stack.InsertRow(rowswap, 1);

InsertRow:
    - Moves an existing AdaptiveStackRowPanel into this stack.
    - Removes it from its previous parent if needed.
    - Inserts its name into RowOrder at the requested slot.
    - Does not duplicate rows already in the stack.
    - Gives unnamed rows a generated name.

Rule update helpers:
    stack.SetRowHeight("rowPreview", 120);
    stack.SetRowSpringMode("rowList", AdaptiveStackRowSpringMode.True);
    stack.SetRowStretchChildHeight("rowProgress", false);
    stack.SetRowGaps("rowStatus", 0, 6);

SetRowRule:
    stack.SetRowRule("rowPreview", rule =>
    {
        rule.RowHeight = 120;
        rule.SpringMode = AdaptiveStackRowSpringMode.True;
    });

SetRowRule intent:
    - Finds the live row by name or current visual index.
    - Creates a RowRules entry if one does not already exist.
    - Updates rule-only values such as SpringMode.
    - Applies row-backed values such as RowHeight, gaps, collapsed state, and StretchChildHeight to the live row.
    - Performs stack layout after the update.

Useful parent defaults:
    DefaultRowHeight = 36
    DefaultGapBottom = 4
    RowInset = 1
    DpiAware = true/false
    DpiGrowthPercent = 25
    StretchChildHeight = true/false

Design intent:
    Use AdaptiveStackPanel for vertical stacking, order, collapse, and DPI height.
    Use AdaptiveStackRowPanel for horizontal child layout.
*/
