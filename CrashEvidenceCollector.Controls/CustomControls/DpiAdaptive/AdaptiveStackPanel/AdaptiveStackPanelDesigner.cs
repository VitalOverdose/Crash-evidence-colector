using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public class AdaptiveStackPanelDesigner : ParentControlDesigner
    {
        private IComponentChangeService? _changeService;
        private DesignerActionListCollection? _actionLists;

        public override void Initialize(IComponent component)
        {
            base.Initialize(component);
            _changeService = (IComponentChangeService?)GetService(typeof(IComponentChangeService));
            if (_changeService != null)
            {
                _changeService.ComponentAdded += OnComponentAdded;
                _changeService.ComponentChanged += OnComponentChanged;
                _changeService.ComponentRemoved += OnComponentRemoved;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _changeService != null)
            {
                _changeService.ComponentAdded -= OnComponentAdded;
                _changeService.ComponentChanged -= OnComponentChanged;
                _changeService.ComponentRemoved -= OnComponentRemoved;
            }
            base.Dispose(disposing);
        }

        private void OnComponentAdded(object? sender, ComponentEventArgs e)
        {
            if (Component is not AdaptiveStackPanel stack)
                return;

            if (e.Component is AdaptiveStackRowPanel row && row.Parent == stack)
            {
                stack.AdoptExternalRowIntoRowRules(row);
                stack.SyncRuleFromRow(row);
                stack.PerformStackLayout();
                return;
            }

            if (e.Component is RoundedAdaptiveStackPanel nestedStack
                && nestedStack.Parent == stack
                && !ReferenceEquals(nestedStack, stack))
            {
                stack.AdoptExternalNestedStackIntoRowRules(nestedStack);
                stack.SyncRuleFromNestedStack(nestedStack);
                stack.PerformStackLayout();
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (Component is not AdaptiveStackPanel stack)
                return;

            if (e.Component is AdaptiveStackRowPanel row)
            {
                stack.RemoveExternalRowFromRowRules(row);
                stack.PerformStackLayout();
                return;
            }

            if (e.Component is RoundedAdaptiveStackPanel nestedStack && !ReferenceEquals(nestedStack, stack))
            {
                stack.RemoveExternalNestedStackFromRowRules(nestedStack);
                stack.PerformStackLayout();
            }
        }

        private void OnComponentChanged(object? sender, ComponentChangedEventArgs e)
        {
            if (Component is not AdaptiveStackPanel stack)
                return;

            if (e.Component is AdaptiveStackRowPanel row && row.Parent == stack)
            {
                stack.AdoptExternalRowIntoRowRules(row);
                stack.SyncRuleFromRow(row);
                stack.PerformStackLayout();
                return;
            }

            if (e.Component is RoundedAdaptiveStackPanel nestedStack
                && nestedStack.Parent == stack
                && !ReferenceEquals(nestedStack, stack))
            {
                stack.AdoptExternalNestedStackIntoRowRules(nestedStack);
                stack.SyncRuleFromNestedStack(nestedStack);
                stack.PerformStackLayout();
                return;
            }

            if (e.Component == stack)
            {
                stack.PerformStackLayout();
            }
        }

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    _actionLists.Add(new AdaptiveStackPanelActionList(Component));
                    _actionLists.AddRange(base.ActionLists);
                }
                return _actionLists;
            }
        }
    }

    public sealed class AdaptiveStackPanelActionList : DesignerActionList
    {
        private int _standardGap = 4;
        private string _rowStateTarget = "0";

        public AdaptiveStackPanelActionList(IComponent component) : base(component) { }

        private AdaptiveStackPanel? Stack => Component as AdaptiveStackPanel;

        private IComponentChangeService? ChangeService =>
            (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

        private IDesignerHost? DesignerHost =>
            (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

        public int DefaultRowHeight
        {
            get => Stack?.DefaultRowHeight ?? 36;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.DefaultRowHeight), Math.Max(1, value), "Set Default Row Height");
        }

        public int StandardGap
        {
            get => _standardGap;
            set => _standardGap = Math.Max(0, value);
        }

        public int RowInset
        {
            get => Stack?.RowInset ?? 1;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.RowInset), Math.Max(0, value), "Set Row Inset");
        }

        public bool DpiAware
        {
            get => Stack?.DpiAware ?? false;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.DpiAware), value, "Set DPI Aware");
        }

        public int DpiGrowthPercent
        {
            get => Stack?.DpiGrowthPercent ?? 25;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.DpiGrowthPercent), Math.Clamp(value, 0, 100), "Set DPI Growth");
        }

        public bool StretchChildHeight
        {
            get => Stack?.StretchChildHeight ?? true;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.StretchChildHeight), value, "Set Stretch Child Height");
        }

        public bool RequestHostFormDpiHeightExpansion
        {
            get => Stack?.RequestHostFormDpiHeightExpansion ?? false;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.RequestHostFormDpiHeightExpansion), value, "Set Host DPI Height Expansion");
        }

        public bool DragHostForm
        {
            get => Stack?.DragHostForm ?? false;
            set => SetProperty(Stack, nameof(AdaptiveStackPanel.DragHostForm), value, "Set Drag Host Form");
        }

        public string RowStateTarget
        {
            get => _rowStateTarget;
            set => _rowStateTarget = value ?? string.Empty;
        }

        public void AddRow()
        {
            if (Stack is not { } stack || DesignerHost == null)
            {
                return;
            }

            var controlsProp = TypeDescriptor.GetProperties(stack)["Controls"];
            using var tx = DesignerHost.CreateTransaction("Add Adaptive Stack Row");
            if (DesignerHost.CreateComponent(typeof(AdaptiveStackRowPanel)) is not AdaptiveStackRowPanel row)
            {
                return;
            }

            row.RowHeight = stack.DefaultRowHeight;
            row.GapBottom = stack.DefaultGapBottom;
            row.StretchChildHeight = stack.StretchChildHeight;
            row.TabIndex = stack.GetRows().Count * 2;
            row.Width = Math.Max(1, stack.Width - stack.Padding.Left - stack.Padding.Right);
            row.Height = Math.Max(1, stack.DefaultRowHeight);
            row.Margin = Padding.Empty;

            ChangeService?.OnComponentChanging(stack, controlsProp);
            stack.Controls.Add(row);
            ChangeService?.OnComponentChanged(stack, controlsProp, null, row);
            stack.PerformStackLayout();
            tx?.Commit();
        }

        public void ApplyStandardSpacing()
        {
            if (Stack is not { } stack)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Apply Adaptive Stack Standard Spacing");
            foreach (AdaptiveStackRowPanel row in stack.GetRows())
            {
                SetProperty(row, nameof(AdaptiveStackRowPanel.GapTop), 0, null);
                SetProperty(row, nameof(AdaptiveStackRowPanel.GapBottom), _standardGap, null);
            }
            stack.PerformStackLayout();
            tx?.Commit();
        }

        public void RefreshLayout()
        {
            if (Stack is not { } stack)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Refresh Adaptive Stack Layout");
            stack.ResequenceRows();
            stack.PerformStackLayout();
            tx?.Commit();
        }

        public void ToggleRowState()
        {
            if (Stack is not { } stack)
            {
                return;
            }

            string target = _rowStateTarget.Trim();
            if (target.Length == 0)
            {
                return;
            }

            bool changed = int.TryParse(target, out int index)
                ? stack.Toggle(index)
                : stack.Toggle(target);

            if (changed)
            {
                stack.PerformStackLayout();
            }
        }

        public void ExpandAllRows()
        {
            if (Stack is not { } stack)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Expand All Adaptive Stack Rows");
            foreach (AdaptiveStackRowPanel row in stack.GetRows())
            {
                SetProperty(row, nameof(AdaptiveStackRowPanel.StartsCollapsed), false, null);
            }
            stack.PerformStackLayout();
            tx?.Commit();
        }

        public void CollapseMarkedRows()
        {
            if (Stack is not { } stack)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Collapse Marked Adaptive Stack Rows");
            foreach (AdaptiveStackRowPanel row in stack.GetRows().Where(r => r.Tag is string tag && tag.Contains("collapsed", StringComparison.OrdinalIgnoreCase)))
            {
                SetProperty(row, nameof(AdaptiveStackRowPanel.StartsCollapsed), true, null);
            }
            stack.PerformStackLayout();
            tx?.Commit();
        }

        public void LineUp()
        {
            if (Stack is not { } stack)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Line Up Adaptive Stack Rows");
            int width = Math.Max(1, stack.Width - stack.Padding.Left - stack.Padding.Right);
            foreach (AdaptiveStackRowPanel row in stack.GetRows())
            {
                SetProperty(row, nameof(Control.Width), width, null);
            }
            stack.PerformStackLayout();
            tx?.Commit();
        }

        private void SetProperty(object? component, string propertyName, object value, string? transactionName)
        {
            if (component == null)
            {
                return;
            }

            PropertyDescriptor? prop = TypeDescriptor.GetProperties(component)[propertyName];
            if (prop == null)
            {
                return;
            }

            object? oldValue = prop.GetValue(component);
            if (Equals(oldValue, value))
            {
                return;
            }

            using var tx = transactionName == null ? null : DesignerHost?.CreateTransaction(transactionName);
            ChangeService?.OnComponentChanging(component, prop);
            prop.SetValue(component, value);
            ChangeService?.OnComponentChanged(component, prop, oldValue, value);
            tx?.Commit();
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();
            items.Add(new DesignerActionHeaderItem("Rows"));
            items.Add(new DesignerActionMethodItem(this, nameof(AddRow), "Add Row", "Rows", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ApplyStandardSpacing), "Apply Standard Spacing", "Rows", true));
            items.Add(new DesignerActionMethodItem(this, nameof(LineUp), "Line Up", "Rows", true));
            items.Add(new DesignerActionHeaderItem("Row State"));
            items.Add(new DesignerActionPropertyItem(nameof(RowStateTarget), "Row Name / Number", "Row State", "Row name or zero-based row number."));
            items.Add(new DesignerActionMethodItem(this, nameof(ToggleRowState), "Toggle Row State", "Row State", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ExpandAllRows), "Expand All Rows", "Row State", true));
            items.Add(new DesignerActionMethodItem(this, nameof(CollapseMarkedRows), "Collapse Marked Rows", "Row State", true));
            items.Add(new DesignerActionHeaderItem("Defaults"));
            items.Add(new DesignerActionPropertyItem(nameof(DefaultRowHeight), "Default Row Height", "Defaults", "Height for newly added rows."));
            items.Add(new DesignerActionPropertyItem(nameof(StandardGap), "Standard Gap", "Defaults", "Gap applied by Apply Standard Spacing."));
            items.Add(new DesignerActionPropertyItem(nameof(RowInset), "Row Inset", "Defaults", "Horizontal inset so the host panel remains selectable."));
            items.Add(new DesignerActionHeaderItem("DPI"));
            items.Add(new DesignerActionPropertyItem(nameof(DpiAware), "DPI Aware", "DPI", "Apply partial DPI growth to row heights at runtime."));
            items.Add(new DesignerActionPropertyItem(nameof(DpiGrowthPercent), "DPI Growth %", "DPI", "Percent of DPI growth applied to row heights."));
            items.Add(new DesignerActionPropertyItem(nameof(StretchChildHeight), "Stretch Child Height", "DPI", "Stretch row children to available row height."));
            items.Add(new DesignerActionPropertyItem(nameof(RequestHostFormDpiHeightExpansion), "Host Height Expansion", "DPI", "Ask a RoundedForm host to apply matching DPI height breathing room."));
            items.Add(new DesignerActionPropertyItem(nameof(DragHostForm), "Drag Host Form", "DPI", "Let the stack surface and row backgrounds drag the parent RoundedForm."));
            return items;
        }
    }

    public class AdaptiveStackRowPanelDesigner : ParentControlDesigner
    {
        private IComponentChangeService? _changeService;
        private DesignerActionListCollection? _actionLists;

        public override void Initialize(IComponent component)
        {
            base.Initialize(component);
            _changeService = (IComponentChangeService?)GetService(typeof(IComponentChangeService));
            if (_changeService != null)
            {
                _changeService.ComponentAdded += OnComponentAdded;
                _changeService.ComponentChanged += OnComponentChanged;
                _changeService.ComponentRemoved += OnComponentRemoved;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _changeService != null)
            {
                _changeService.ComponentAdded -= OnComponentAdded;
                _changeService.ComponentChanged -= OnComponentChanged;
                _changeService.ComponentRemoved -= OnComponentRemoved;
            }
            base.Dispose(disposing);
        }

        private void OnComponentAdded(object? sender, ComponentEventArgs e)
        {
            if (Component is not AdaptiveStackRowPanel row)
                return;

            if (e.Component is Control child && child.Parent == row)
            {
                row.AdoptExternalChildIntoItemRules(child);
                row.SyncRuleFromChild(child);
                row.ForceRefreshLayout();
                if (row.Parent is AdaptiveStackPanel stack)
                    stack.PerformStackLayout();
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (Component is not AdaptiveStackRowPanel row)
                return;

            if (e.Component is Control child)
            {
                row.RemoveExternalChildFromItemRules(child);
                row.ForceRefreshLayout();
                if (row.Parent is AdaptiveStackPanel stack)
                    stack.PerformStackLayout();
            }
        }

        private void OnComponentChanged(object? sender, ComponentChangedEventArgs e)
        {
            if (Component is not AdaptiveStackRowPanel row)
                return;

            if (e.Component == row || e.Component is Control c && c.Parent == row)
            {
                if (e.Component is Control child && child.Parent == row)
                {
                    row.AdoptExternalChildIntoItemRules(child);
                    row.SyncRuleFromChild(child);
                }

                row.ForceRefreshLayout();
                if (row.Parent is AdaptiveStackPanel stack)
                    stack.PerformStackLayout();
            }
        }

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    _actionLists.Add(new AdaptiveStackRowPanelActionList(Component));
                    _actionLists.AddRange(base.ActionLists);
                }
                return _actionLists;
            }
        }
    }

    public sealed class AdaptiveStackRowPanelActionList : DesignerActionList
    {
        private int _controlCount = 1;
        private int _horizontalGap = 4;
        private int _setHeight = 36;
        private AdaptiveRowPanelAddControlType _controlToAdd = AdaptiveRowPanelAddControlType.Spacer;

        public AdaptiveStackRowPanelActionList(IComponent component) : base(component) { }

        private AdaptiveStackRowPanel? Row => Component as AdaptiveStackRowPanel;

        private AdaptiveStackPanel? Host => Row?.Parent as AdaptiveStackPanel;

        private IComponentChangeService? ChangeService =>
            (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

        private IDesignerHost? DesignerHost =>
            (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

        public int RowHeight
        {
            get => Row?.RowHeight ?? 36;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.RowHeight), Math.Max(0, value), "Set Row Height");
        }

        public int CollapsedHeight
        {
            get => Row?.CollapsedHeight ?? 0;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.CollapsedHeight), Math.Max(0, value), "Set Collapsed Height");
        }

        public int GapTop
        {
            get => Row?.GapTop ?? 0;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.GapTop), Math.Max(0, value), "Set Row Gap Top");
        }

        public int GapBottom
        {
            get => Row?.GapBottom ?? 4;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.GapBottom), Math.Max(0, value), "Set Row Gap Bottom");
        }

        public bool StartsCollapsed
        {
            get => Row?.StartsCollapsed ?? false;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.StartsCollapsed), value, "Set Starts Collapsed");
        }

        public AdaptiveRowContentAlignment ControlPositioning
        {
            get => Row?.ContentAlignment ?? AdaptiveRowContentAlignment.Left;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.ContentAlignment), value, "Set Control Positioning");
        }

        public bool StretchChildHeight
        {
            get => Row?.StretchChildHeight ?? true;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.StretchChildHeight), value, "Set Stretch Child Height");
        }

        public int ItemGap
        {
            get => Row?.ItemGap ?? 6;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.ItemGap), Math.Max(0, value), "Set Item Gap");
        }

        public int SpringIndex
        {
            get => Row?.SpringControlTabIndex ?? -1;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.SpringControlTabIndex), value, "Set Spring Index");
        }

        public AdaptiveRowSpringMode SpringExpands
        {
            get => Row?.SpringExpands ?? AdaptiveRowSpringMode.Both;
            set => SetProperty(Row, nameof(AdaptiveStackRowPanel.SpringExpands), value, "Set Spring Expands");
        }

        public int HorizontalGap
        {
            get => _horizontalGap;
            set => _horizontalGap = value;
        }

        public int SetHeightValue
        {
            get => _setHeight;
            set => _setHeight = Math.Max(1, value);
        }

        public int ControlCount
        {
            get => _controlCount;
            set => _controlCount = Math.Max(1, value);
        }

        public AdaptiveRowPanelAddControlType ControlToAdd
        {
            get => _controlToAdd;
            set => _controlToAdd = value;
        }

        public void MoveUp()
        {
            Host?.MoveRow(Row!, -1);
        }

        public void MoveDown()
        {
            Host?.MoveRow(Row!, 1);
        }

        public void DeleteRow()
        {
            if (Row is not { } row || Host is not { } host)
            {
                return;
            }

            var controlsProp = TypeDescriptor.GetProperties(host)["Controls"];
            using var tx = DesignerHost?.CreateTransaction("Delete Adaptive Stack Row");
            ChangeService?.OnComponentChanging(host, controlsProp);
            host.Controls.Remove(row);
            if (DesignerHost != null)
            {
                DesignerHost.DestroyComponent(row);
            }
            else
            {
                row.Dispose();
            }
            ChangeService?.OnComponentChanged(host, controlsProp, row, null);
            host.ResequenceRows();
            host.PerformStackLayout();
            tx?.Commit();
        }

        public void AddControl()
        {
            if (Row is not { } row || DesignerHost == null)
            {
                return;
            }

            Type controlType = GetControlType(_controlToAdd);
            var controlsProp = TypeDescriptor.GetProperties(row)["Controls"];
            using var tx = DesignerHost.CreateTransaction($"Add {_controlToAdd} to Adaptive Stack Row");

            for (int i = 0; i < Math.Max(1, _controlCount); i++)
            {
                if (DesignerHost.CreateComponent(controlType) is not Control child)
                {
                    continue;
                }

                ConfigureAddedControl(child, _controlToAdd, row);
                ChangeService?.OnComponentChanging(row, controlsProp);
                row.Controls.Add(child);
                ChangeService?.OnComponentChanged(row, controlsProp, null, child);
            }

            row.CaptureCurrentLayoutAsBaseline();
            tx?.Commit();
        }

        public void AddSpacer()
        {
            AdaptiveRowPanelAddControlType previous = _controlToAdd;
            _controlToAdd = AdaptiveRowPanelAddControlType.Spacer;
            AddControl();
            _controlToAdd = previous;
        }

        public void ExpandSpring()
        {
            if (Row is not { } row)
            {
                return;
            }

            Control[] controls = row.GetOrderedChildren().ToArray();
            if (controls.Length == 0)
            {
                return;
            }

            Control? spring = row.FindSpringControl(controls);
            if (spring == null)
            {
                return;
            }

            int available = Math.Max(0, row.Width - row.Padding.Left - row.Padding.Right);
            int totalWidth = controls.Sum(c => c.Width);
            for (int i = 0; i < controls.Length - 1; i++)
            {
                totalWidth += GetDesignerGap(row, controls[i], controls[i + 1]);
            }

            int spare = available - totalWidth;
            if (spare <= 0)
            {
                return;
            }

            SetProperty(spring, nameof(Control.Width), spring.Width + spare, "Expand Spring");
            row.CaptureCurrentLayoutAsBaseline();
        }

        public void AdjustHorizontalGap()
        {
            AdjustHorizontalGapBy(_horizontalGap);
        }

        public void SetHeight()
        {
            ApplySetHeight(_setHeight);
        }

        private void ApplySetHeight(int height)
        {
            if (Row is not { } row)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Set Adaptive Stack Row Height");
            SetProperty(row, nameof(AdaptiveStackRowPanel.RowHeight), height, null);
            foreach (Control child in row.Controls)
            {
                SetProperty(child, nameof(Control.Height), Math.Max(1, height - row.Padding.Top - row.Padding.Bottom), null);
            }
            row.CaptureCurrentLayoutAsBaseline();
            Host?.PerformStackLayout();
            tx?.Commit();
        }

        public void InheritParentBackColor()
        {
            if (Row is not { } row || row.Parent == null)
            {
                return;
            }

            Color color = GetActualBackgroundColor(row);
            SetProperty(row, nameof(Control.BackColor), color, "Inherit Parent BackColor");
        }

        public void ResequenceTabIndexes()
        {
            if (Row is not { } row) return;
            var controls = row.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToArray();
            if (controls.Length == 0) return;
            using var tx = DesignerHost?.CreateTransaction("Resequence Tab Indexes");
            for (int i = 0; i < controls.Length; i++)
            {
                int newIndex = i * 2;
                if (controls[i].TabIndex != newIndex)
                    SetProperty(controls[i], nameof(Control.TabIndex), newIndex, null);
            }
            tx?.Commit();
        }

        public void MakeChildrenWidthDpiSafe()
        {
            if (Row is not { } row) return;
            using var tx = DesignerHost?.CreateTransaction("Make Children Width DPI Safe");
            foreach (Control child in row.Controls)
            {
                if (child is AdaptiveRowPanel nestedRow)
                {
                    ApplyTextSafeWidths(nestedRow);
                    nestedRow.CaptureCurrentLayoutAsBaseline();
                    nestedRow.ForceRefreshLayout();
                    continue;
                }

                if (!AdaptiveRowPanelActionList.TryGetTextSafeWidth(child, out int newWidth))
                    continue;

                if (child.Width == newWidth) continue;
                SetProperty(child, nameof(Control.Width), newWidth, null);
            }
            row.CaptureCurrentLayoutAsBaseline();
            row.ForceRefreshLayout();
            Host?.PerformStackLayout();
            tx?.Commit();
        }

        private void ApplyTextSafeWidths(AdaptiveRowPanel panel)
        {
            foreach (Control child in panel.Controls)
            {
                if (!AdaptiveRowPanelActionList.TryGetTextSafeWidth(child, out int newWidth))
                {
                    continue;
                }

                SetProperty(child, nameof(Control.Width), newWidth, null);
            }
        }

        public void RefreshLayout()
        {
            if (Row is not { } row)
            {
                return;
            }

            row.ForceRefreshLayout();
            Host?.PerformStackLayout();
        }

        private void AdjustHorizontalGapBy(int delta)
        {
            if (Row is not { } row)
            {
                return;
            }

            using var tx = DesignerHost?.CreateTransaction("Adjust Horizontal Gap");
            foreach (Control child in row.Controls)
            {
                Padding margin = child.Margin;
                int left = Math.Max(0, margin.Left + delta);
                int right = Math.Max(0, margin.Right + delta);
                SetProperty(child, nameof(Control.Margin), new Padding(left, margin.Top, right, margin.Bottom), null);
            }
            row.CaptureCurrentLayoutAsBaseline();
            row.ForceRefreshLayout();
            Host?.PerformStackLayout();
            tx?.Commit();
        }

        private void SetProperty(object? component, string propertyName, object value, string? transactionName)
        {
            if (component == null)
            {
                return;
            }

            PropertyDescriptor? prop = TypeDescriptor.GetProperties(component)[propertyName];
            if (prop == null)
            {
                return;
            }

            object? oldValue = prop.GetValue(component);
            if (Equals(oldValue, value))
            {
                return;
            }

            using var tx = transactionName == null ? null : DesignerHost?.CreateTransaction(transactionName);
            ChangeService?.OnComponentChanging(component, prop);
            prop.SetValue(component, value);
            ChangeService?.OnComponentChanged(component, prop, oldValue, value);
            tx?.Commit();

            if (component is AdaptiveStackRowPanel row)
            {
                row.ForceRefreshLayout();
                if (row.Parent is AdaptiveStackPanel stack)
                {
                    stack.PerformStackLayout();
                }
            }
        }

        private static int GetDesignerGap(AdaptiveStackRowPanel row, Control current, Control next)
        {
            if (row.UsePaddingFromItems)
            {
                int spacing = Math.Max(0, current.Margin.Right) + Math.Max(0, next.Margin.Left);
                return spacing > 0 ? spacing : row.ItemGap;
            }

            int gap = Math.Max(0, next.Left - (current.Left + current.Width));
            return gap > 0 ? gap : row.ItemGap;
        }

        private static Color GetActualBackgroundColor(Control row)
        {
            Control child = row;
            Control? parent = row.Parent;
            while (parent != null)
            {
                if (parent is RoundedPanelFaster faster)
                {
                    return faster.InnerColor;
                }

                if (parent is RoundedForm form)
                {
                    if (form.TopBarVisible && form.TopBarHeight > 0 && child.Top < form.TopBarHeight)
                    {
                        return form.TopBarColor;
                    }

                    if (form.BottomBarVisible && form.BottomBarHeight > 0 && child.Bottom > form.Height - form.BottomBarHeight)
                    {
                        return form.BottomBarColor;
                    }

                    return form.FormBackColor;
                }

                if (parent.BackColor != Color.Transparent)
                {
                    return parent.BackColor;
                }

                child = parent;
                parent = parent.Parent;
            }

            return SystemColors.Control;
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
                _ => typeof(AdaptiveRowSpacer)
            };
        }

        private static void ConfigureAddedControl(Control child, AdaptiveRowPanelAddControlType controlType, AdaptiveStackRowPanel row)
        {
            int childHeight = Math.Max(18, row.RowHeight - row.Padding.Top - row.Padding.Bottom);
            ApplyStandardFont(child);
            child.Margin = new Padding(row.DefaultChildHorizontalMargin, 0, row.DefaultChildHorizontalMargin, 0);
            child.TabIndex = Math.Max(0, row.Controls.Count * 2);
            child.Height = childHeight;

            switch (child)
            {
                case AdaptiveRowSpacer spacer:
                    spacer.Size = new Size(80, childHeight);
                    break;
                case AdaptiveRowLabel label:
                    label.Text = "Label";
                    label.LabelHeight = childHeight;
                    label.Size = new Size(80, childHeight);
                    break;
                case HeaderButton headerBtn:
                    headerBtn.Text = "Header";
                    headerBtn.Size = new Size(120, childHeight);
                    break;
                case ModernButton button:
                    button.Text = "Button";
                    button.Size = new Size(100, childHeight);
                    break;
                case RoundedComboBox comboBox:
                    comboBox.Size = new Size(140, childHeight);
                    break;
                case RoundedMultiSelectComboBox multiSelectComboBox:
                    multiSelectComboBox.PlaceholderText = "Search Engine";
                    multiSelectComboBox.Size = new Size(140, childHeight);
                    break;
                case RoundedNumericTextBox numericTextBox:
                    numericTextBox.Size = new Size(120, childHeight);
                    break;
                case RoundedTextBox textBox:
                    textBox.PlaceholderText = "Text...";
                    textBox.Size = new Size(160, childHeight);
                    break;
                case RoundedCheckBox checkBox:
                    checkBox.Text = "CheckBox";
                    checkBox.Size = new Size(140, childHeight);
                    break;
                case RoundedToggleSwitch toggle:
                    toggle.Text = "Toggle";
                    toggle.Size = new Size(120, childHeight);
                    break;
                case RoundedTrackBar trackBar:
                    trackBar.Size = new Size(180, Math.Max(34, childHeight));
                    break;
                case AdaptiveLineBreak lineBreak:
                    lineBreak.Size = new Size(10, childHeight);
                    lineBreak.Margin = new Padding(2, 0, 2, 0);
                    break;
                case RoundedProgressBar progressBar:
                    progressBar.Size = new Size(200, 12);
                    progressBar.Margin = new Padding(row.DefaultChildHorizontalMargin, 0, row.DefaultChildHorizontalMargin, 0);
                    break;
                case ListView listView:
                    listView.View = View.Details;
                    listView.Size = new Size(220, Math.Max(60, childHeight));
                    break;
                default:
                    child.Size = new Size(100, childHeight);
                    break;
            }
        }

        private static void ApplyStandardFont(Control child)
        {
            FontStyle style = child.Font?.Style ?? FontStyle.Regular;
            child.Font = new Font("Segoe UI", 10F, style, GraphicsUnit.Point);
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();
            items.Add(new DesignerActionHeaderItem("Row"));
            items.Add(new DesignerActionMethodItem(this, nameof(MoveUp), "Move Up", "Row", true));
            items.Add(new DesignerActionMethodItem(this, nameof(MoveDown), "Move Down", "Row", true));
            items.Add(new DesignerActionMethodItem(this, nameof(DeleteRow), "Delete Row", "Row", true));
            items.Add(new DesignerActionPropertyItem(nameof(RowHeight), "Row Height", "Row", "Authored row height."));
            items.Add(new DesignerActionPropertyItem(nameof(CollapsedHeight), "Collapsed Height", "Row", "Height used while this row is collapsed. Use 0 to hide it completely."));
            items.Add(new DesignerActionPropertyItem(nameof(GapTop), "Gap Top", "Row", "Space above this row."));
            items.Add(new DesignerActionPropertyItem(nameof(GapBottom), "Gap Bottom", "Row", "Space below this row."));
            items.Add(new DesignerActionPropertyItem(nameof(StartsCollapsed), "Starts Collapsed", "Row", "Collapse this row to its collapsed height."));
            items.Add(new DesignerActionHeaderItem("Child Layout"));
            items.Add(new DesignerActionPropertyItem(nameof(ControlPositioning), "Control Positioning", "Child Layout", "Left, center, or right align the child group."));
            items.Add(new DesignerActionPropertyItem(nameof(StretchChildHeight), "Stretch Child Height", "Child Layout", "Stretch children to fit row height."));
            items.Add(new DesignerActionPropertyItem(nameof(ItemGap), "Item Gap", "Child Layout", "Actual fallback gap between children when adjacent margins are zero."));
            items.Add(new DesignerActionPropertyItem(nameof(SpringExpands), "Spring Mode", "Child Layout", "Controls whether the spring expands, contracts, or does both."));
            items.Add(new DesignerActionHeaderItem("One Time Adjustments"));
            items.Add(new DesignerActionPropertyItem(nameof(HorizontalGap), "Margin Delta N", "One Time Adjustments", "Signed amount added to each child's left/right margin. Use negative values to shrink."));
            items.Add(new DesignerActionMethodItem(this, nameof(AdjustHorizontalGap), "Adjust Child Margins", "One Time Adjustments", true));
            items.Add(new DesignerActionPropertyItem(nameof(SetHeightValue), "Height N", "One Time Adjustments", "Height used by Set Height."));
            items.Add(new DesignerActionMethodItem(this, nameof(SetHeight), "Set Height", "One Time Adjustments", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ExpandSpring), "Expand Spring", "One Time Adjustments", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ResequenceTabIndexes), "Resequence Tab Indexes (0,2,4...)", "One Time Adjustments", true));
            items.Add(new DesignerActionMethodItem(this, nameof(InheritParentBackColor), "Use Parent Color", "One Time Adjustments", true));
            return items;
        }
    }
}
