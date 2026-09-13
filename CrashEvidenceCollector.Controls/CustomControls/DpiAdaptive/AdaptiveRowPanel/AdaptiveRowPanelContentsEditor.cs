using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum AdaptiveRowPanelAddControlType
    {
        Spacer,
        Label,
        Button,
        IconButton,
        TextBox,
        ComboBox,
        DropdownButton,
        MultiSelectComboBox,
        NumericTextBox,
        CheckBox,
        ToggleSwitch,
        TrackBar,
        LineBreak,
        HeaderButton,
        ProgressBar,
        ListView,
        PictureBox,
        DateTimePicker
    }

    public class AdaptiveRowPanelDesigner : ParentControlDesigner
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
            if (Component is AdaptiveRowPanel panel && e.Component is Control child && child.Parent == panel)
            {
                panel.AdoptExternalChildIntoItemRules(child);
                panel.SyncRuleFromChild(child);
                panel.ForceRefreshLayout();
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (Component is AdaptiveRowPanel panel && e.Component is Control child)
            {
                panel.RemoveExternalChildFromItemRules(child);
                panel.ForceRefreshLayout();
            }
        }

        private void OnComponentChanged(object? sender, ComponentChangedEventArgs e)
        {
            if (Component is not AdaptiveRowPanel panel) return;
            if (e.Component is Control c && c.Parent == panel)
            {
                panel.AdoptExternalChildIntoItemRules(c);
                panel.SyncRuleFromChild(c);
                panel.ForceRefreshLayout();
                return;
            }

            if (e.Component == panel)
            {
                panel.ForceRefreshLayout();
            }
        }

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    _actionLists.Add(new AdaptiveRowPanelActionList(Component));
                    _actionLists.AddRange(base.ActionLists);
                }
                return _actionLists;
            }
        }

        public override DesignerVerbCollection Verbs =>
            new DesignerVerbCollection { new DesignerVerb("Test Layout", (s, e) => { }) };
    }

    public class AdaptiveRowPanelActionList : DesignerActionList
    {
        public AdaptiveRowPanelActionList(IComponent component) : base(component) { }

        private AdaptiveRowPanel? Panel => Component as AdaptiveRowPanel;

        private IComponentChangeService? ChangeService =>
            (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

        private IDesignerHost? DesignerHost =>
            (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

        private static PropertyDescriptor? MarginProp(Control c) =>
            TypeDescriptor.GetProperties(c)["Margin"];

        private static void SetMargin(Control child, Padding newMargin, IComponentChangeService? svc)
        {
            var prop = MarginProp(child);
            if (prop == null) return;
            var old = child.Margin;
            if (old == newMargin) return;
            svc?.OnComponentChanging(child, prop);
            prop.SetValue(child, newMargin);
            svc?.OnComponentChanged(child, prop, old, newMargin);
        }

        private int _horizontalGapAdjustment = 4;
        private int _gapValue = 6;
        private int _verticalGap = 2;
        private int _controlCount = 1;
        private AdaptiveRowPanelAddControlType _controlToAdd = AdaptiveRowPanelAddControlType.Spacer;
        private System.Drawing.Font? _standardFont;
        private int _standardHeight = 30;
        private Padding _standardMargin = new Padding(3);

        public int VerticalGap
        {
            get => _verticalGap;
            set => _verticalGap = Math.Max(0, value);
        }

        public System.Drawing.Font StandardFont
        {
            get => _standardFont ?? Panel?.Font ?? Control.DefaultFont;
            set => _standardFont = value;
        }

        public int StandardHeight
        {
            get => _standardHeight;
            set => _standardHeight = Math.Max(1, value);
        }

        public Padding StandardMargin
        {
            get => _standardMargin;
            set => _standardMargin = value;
        }

        public void FitChildrenHeightsToRow()
        {
            if (Panel is not { } panel) return;
            int target = Math.Max(18, panel.Height - _verticalGap * 2);
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Fit Children Heights to Row");
            foreach (Control child in panel.Controls)
            {
                if (child is AdaptiveRowLabel label)
                {
                    var prop = TypeDescriptor.GetProperties(label)["LabelHeight"];
                    int old = label.LabelHeight;
                    svc?.OnComponentChanging(label, prop);
                    if (prop != null) prop.SetValue(label, target); else label.LabelHeight = target;
                    svc?.OnComponentChanged(label, prop, old, target);
                }
                else
                {
                    var prop = TypeDescriptor.GetProperties(child)["Height"];
                    int old = child.Height;
                    if (old == target) continue;
                    svc?.OnComponentChanging(child, prop);
                    if (prop != null) prop.SetValue(child, target); else child.Height = target;
                    svc?.OnComponentChanged(child, prop, old, target);
                }
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void FitChildrenWidthsToText()
        {
            if (Panel is not { } panel) return;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Fit Children Widths to Text");
            foreach (Control child in panel.Controls)
            {
                if (!TryGetTextSafeWidth(child, out int newWidth))
                    continue;

                if (child.Width == newWidth) continue;
                var prop = TypeDescriptor.GetProperties(child)["Width"];
                int old = child.Width;
                svc?.OnComponentChanging(child, prop);
                if (prop != null) prop.SetValue(child, newWidth); else child.Width = newWidth;
                svc?.OnComponentChanged(child, prop, old, newWidth);
            }
            panel.CaptureCurrentLayoutAsBaseline();
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        internal static bool TryGetTextSafeWidth(Control child, out int width)
        {
            width = 0;

            if (child is not ModernButton button)
            {
                return false;
            }

            width = Math.Max(child.MinimumSize.Width, button.GetPreferredContentWidth(1.5f));
            return true;
        }

        public int HorizontalGapAdjustment
        {
            get => _horizontalGapAdjustment;
            set => _horizontalGapAdjustment = value;
        }

        // Target gap (N) for the one-shot Set Gap action. Design-time only — not a live property.
        public int GapValue
        {
            get => _gapValue;
            set => _gapValue = Math.Max(0, value);
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

        public void RefreshLayout()
        {
            if (Panel is not { } panel) return;
            var svc = ChangeService;
            var prop = TypeDescriptor.GetProperties(panel)["Controls"];
            using var tx = DesignerHost?.CreateTransaction("Refresh Layout");
            svc?.OnComponentChanging(panel, prop);
            panel.ForceRefreshLayout();
            svc?.OnComponentChanged(panel, prop, null, null);
            tx?.Commit();
        }

        public void AssignMissingRuleIds()
        {
            if (Panel is not { } panel) return;
            using var tx = DesignerHost?.CreateTransaction("Assign ARP Rule IDs");
            panel.AssignMissingItemRuleIds();
            tx?.Commit();
        }

        public void ReindexRules()
        {
            if (Panel is not { } panel) return;
            using var tx = DesignerHost?.CreateTransaction("Reindex ARP Rules");
            panel.ReindexItemRules();
            tx?.Commit();
        }

        public void StandardizeFont()
        {
            if (Panel is not { } panel) return;
            var font = StandardFont;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Standardize Font");
            foreach (Control child in panel.Controls)
            {
                var prop = TypeDescriptor.GetProperties(child)["Font"];
                var old = child.Font;
                if (Equals(old, font)) continue;
                svc?.OnComponentChanging(child, prop);
                if (prop != null) prop.SetValue(child, font); else child.Font = font;
                svc?.OnComponentChanged(child, prop, old, font);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void StandardizeHeight()
        {
            if (Panel is not { } panel) return;
            int target = StandardHeight;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Standardize Height");
            foreach (Control child in panel.Controls)
            {
                // AdaptiveRowLabel drives its own height via LabelHeight; everything else via Height.
                if (child is AdaptiveRowLabel label)
                {
                    var prop = TypeDescriptor.GetProperties(label)["LabelHeight"];
                    int old = label.LabelHeight;
                    if (old == target) continue;
                    svc?.OnComponentChanging(label, prop);
                    if (prop != null) prop.SetValue(label, target); else label.LabelHeight = target;
                    svc?.OnComponentChanged(label, prop, old, target);
                }
                else
                {
                    var prop = TypeDescriptor.GetProperties(child)["Height"];
                    int old = child.Height;
                    if (old == target) continue;
                    svc?.OnComponentChanging(child, prop);
                    if (prop != null) prop.SetValue(child, target); else child.Height = target;
                    svc?.OnComponentChanged(child, prop, old, target);
                }
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void StandardizeMargin()
        {
            if (Panel is not { } panel) return;
            Padding m = StandardMargin;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Standardize Margin");
            foreach (Control child in panel.Controls)
            {
                SetMargin(child, m, svc);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void InheritParentBackColor()
        {
            if (Panel is not { } panel || panel.Parent == null) return;
            var svc = ChangeService;
            var prop = TypeDescriptor.GetProperties(panel)["BackColor"];
            var oldColor = panel.BackColor;
            var newColor = GetActualBackgroundColor(panel);
            if (oldColor == newColor) return;
            using var tx = DesignerHost?.CreateTransaction("Inherit Parent BackColor");
            svc?.OnComponentChanging(panel, prop);
            if (prop != null) prop.SetValue(panel, newColor); else panel.BackColor = newColor;
            svc?.OnComponentChanged(panel, prop, oldColor, newColor);
            tx?.Commit();
        }

        private static Color GetActualBackgroundColor(Control panel)
        {
            Control child = panel;
            Control? parent = panel.Parent;
            while (parent != null)
            {
                // RoundedPanelFaster paints InnerColor — BackColor is Transparent
                if (parent is RoundedPanelFaster faster)
                    return faster.InnerColor;

                // RoundedForm paints separate bar zones over FormBackColor
                if (parent is RoundedForm form)
                {
                    if (form.TopBarVisible && form.TopBarHeight > 0 && child.Top < form.TopBarHeight)
                        return form.TopBarColor;
                    if (form.BottomBarVisible && form.BottomBarHeight > 0 &&
                        child.Bottom > form.Height - form.BottomBarHeight)
                        return form.BottomBarColor;
                    return form.FormBackColor;
                }

                if (parent.BackColor != Color.Transparent)
                    return parent.BackColor;

                child = parent;
                parent = parent.Parent;
            }
            return SystemColors.Control;
        }

        public void AddControl()
        {
            if (Panel is not { } panel || DesignerHost == null) return;

            int count = Math.Max(1, _controlCount);
            Type controlType = GetControlType(_controlToAdd);
            var svc = ChangeService;
            var controlsProp = TypeDescriptor.GetProperties(panel)["Controls"];

            using var tx = DesignerHost.CreateTransaction($"Add {count}x {_controlToAdd} to AdaptiveRowPanel");
            for (int i = 0; i < count; i++)
            {
                if (DesignerHost.CreateComponent(controlType) is not Control child) continue;
                ConfigureAddedControl(child, _controlToAdd, panel);
                svc?.OnComponentChanging(panel, controlsProp);
                panel.Controls.Add(child);
                svc?.OnComponentChanged(panel, controlsProp, null, child);
            }
            panel.CaptureCurrentLayoutAsBaseline();
            tx?.Commit();
        }

        public void AddSpacer()
        {
            AdaptiveRowPanelAddControlType previous = _controlToAdd;
            _controlToAdd = AdaptiveRowPanelAddControlType.Spacer;
            AddControl();
            _controlToAdd = previous;
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

        private static void ConfigureAddedControl(
            Control child,
            AdaptiveRowPanelAddControlType controlType,
            AdaptiveRowPanel panel)
        {
            ApplyStandardFont(child);
            child.Margin = new Padding(panel.DefaultChildHorizontalMargin, 3, panel.DefaultChildHorizontalMargin, 3);
            child.TabIndex = Math.Max(0, panel.Controls.Count * 2);
            child.Height = Math.Max(30, panel.Height - panel.Padding.Top - panel.Padding.Bottom);

            switch (child)
            {
                case AdaptiveRowSpacer spacer:
                    spacer.Size = new System.Drawing.Size(80, Math.Max(18, child.Height));
                    spacer.Margin = new Padding(panel.DefaultChildHorizontalMargin, 0, panel.DefaultChildHorizontalMargin, 0);
                    break;
                case AdaptiveRowLabel label:
                    label.Text = "Label";
                    label.LabelHeight = Math.Max(18, child.Height);
                    label.Size = new System.Drawing.Size(80, label.LabelHeight);
                    break;
                case HeaderButton headerBtn:
                    headerBtn.Text = "Header";
                    headerBtn.Size = new System.Drawing.Size(120, child.Height);
                    break;
                case ModernButton button:
                    button.Text = "Button";
                    button.Size = new System.Drawing.Size(100, 30);
                    break;
                case IconButton iconButton:
                    iconButton.Size = new System.Drawing.Size(44, Math.Max(30, child.Height));
                    break;
                case RoundedComboBox comboBox:
                    comboBox.Size = new System.Drawing.Size(140, 30);
                    break;
                case RoundedMultiSelectComboBox multiSelectComboBox:
                    multiSelectComboBox.PlaceholderText = "Search Engine";
                    multiSelectComboBox.Size = new System.Drawing.Size(140, 30);
                    break;
                case RoundedNumericTextBox numericTextBox:
                    numericTextBox.Size = new System.Drawing.Size(120, 30);
                    break;
                case RoundedTextBox textBox:
                    textBox.PlaceholderText = "Text...";
                    textBox.Size = new System.Drawing.Size(160, 30);
                    break;
                case RoundedCheckBox checkBox:
                    checkBox.Text = "CheckBox";
                    checkBox.Size = new System.Drawing.Size(140, 30);
                    break;
                case RoundedToggleSwitch toggle:
                    toggle.Size = new System.Drawing.Size(60, 30);
                    break;
                case RoundedTrackBar trackBar:
                    trackBar.Size = new System.Drawing.Size(180, 34);
                    break;
                case AdaptiveLineBreak lineBreak:
                    lineBreak.Size = new System.Drawing.Size(10, child.Height);
                    lineBreak.Margin = new Padding(2, 0, 2, 0);
                    break;
                case RoundedProgressBar progressBar:
                    progressBar.Size = new System.Drawing.Size(200, 12);
                    progressBar.Margin = new Padding(panel.DefaultChildHorizontalMargin, 0, panel.DefaultChildHorizontalMargin, 0);
                    break;
                case ListView listView:
                    listView.View = View.Details;
                    listView.Size = new System.Drawing.Size(220, Math.Max(60, child.Height));
                    break;
                case DateTimePicker dateTimePicker:
                    dateTimePicker.Format = DateTimePickerFormat.Custom;
                    dateTimePicker.CustomFormat = "dd/MM/yyyy  HH:mm";
                    dateTimePicker.ShowUpDown = true;
                    dateTimePicker.Size = new System.Drawing.Size(184, 30);
                    break;
                default:
                    child.Size = new System.Drawing.Size(100, 30);
                    break;
            }
        }

        private static void ApplyStandardFont(Control child)
        {
            FontStyle style = child.Font?.Style ?? FontStyle.Regular;
            child.Font = new System.Drawing.Font("Segoe UI", 10F, style, GraphicsUnit.Point);
        }

        public void ExpandSpring()
        {
            if (Panel is not { } panel) return;

            var controls = panel.Controls.Cast<Control>()
                .Where(c => c.Visible)
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            if (controls.Length == 0) return;

            Control? spring = FindSpringControl(panel, controls);
            if (spring == null) return;

            // Use Width instead of ClientSize.Width in the OOP designer. ClientSize can report 0.
            int available = Math.Max(0, panel.Width - panel.Padding.Left - panel.Padding.Right);
            int leading = panel.UsePaddingFromItems ? Math.Max(0, controls[0].Margin.Left) : 0;
            int totalWidth = leading + controls.Sum(c => c.Width);

            for (int i = 0; i < controls.Length - 1; i++)
            {
                totalWidth += GetDesignerGap(panel, controls[i], controls[i + 1]);
            }

            int spare = available - totalWidth;
            if (spare <= 0) return;

            var widthProp = TypeDescriptor.GetProperties(spring)["Width"];
            int oldWidth = spring.Width;
            int newWidth = oldWidth + spare;
            var svc = ChangeService;

            using var tx = DesignerHost?.CreateTransaction("Expand Spring");
            svc?.OnComponentChanging(spring, widthProp);
            if (widthProp != null)
                widthProp.SetValue(spring, newWidth);
            else
                spring.Width = newWidth;
            svc?.OnComponentChanged(spring, widthProp, oldWidth, newWidth);
            panel.CaptureCurrentLayoutAsBaseline();
            tx?.Commit();
        }

        private static Control? FindSpringControl(AdaptiveRowPanel panel, Control[] controls)
        {
            // Name-based Spring (with legacy TabIndex fallback) lives on the panel.
            Control? configured = controls.FirstOrDefault(panel.IsSpringControl);
            if (configured != null)
            {
                return configured;
            }

            return controls.OfType<AdaptiveRowSpacer>().FirstOrDefault();
        }

        private static int GetDesignerGap(AdaptiveRowPanel panel, Control current, Control next)
        {
            if (panel.UsePaddingFromItems)
            {
                int spacing = Math.Max(0, current.Margin.Right) + Math.Max(0, next.Margin.Left);
                return spacing > 0 ? spacing : panel.ItemGap;
            }

            int gap = Math.Max(0, next.Left - (current.Left + current.Width));
            return gap > 0 ? gap : panel.ItemGap;
        }

        public void AdjustHorizontalGap()
        {
            if (Panel is not { } panel) return;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Adjust Horizontal Gap");
            foreach (Control child in panel.Controls)
            {
                var m = child.Margin;
                int left = Math.Max(0, m.Left + _horizontalGapAdjustment);
                int right = Math.Max(0, m.Right + _horizontalGapAdjustment);
                SetMargin(child, new Padding(left, m.Top, right, m.Bottom), svc);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        // One-shot absolute pass: write GapValue/2 to every child's left AND right margin, so the
        // gap between any two adjacent children equals GapValue. Sets the spacing outright (unlike
        // AdjustHorizontalGap, which nudges it by ±). Margins are the only spacing source now.
        public void SetGap()
        {
            if (Panel is not { } panel) return;
            int half = _gapValue / 2;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Set Gap");
            foreach (Control child in panel.Controls)
            {
                var m = child.Margin;
                SetMargin(child, new Padding(half, m.Top, half, m.Bottom), svc);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        private void SetPanelProperty(string propertyName, object value, string transactionName)
        {
            if (Panel is not { } panel) return;

            PropertyDescriptor? prop = TypeDescriptor.GetProperties(panel)[propertyName];
            if (prop == null) return;

            object? oldValue = prop.GetValue(panel);
            if (Equals(oldValue, value)) return;

            using var tx = DesignerHost?.CreateTransaction(transactionName);
            ChangeService?.OnComponentChanging(panel, prop);
            prop.SetValue(panel, value);
            ChangeService?.OnComponentChanged(panel, prop, oldValue, value);
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void LineUp()
        {
            if (Panel is not { } panel || panel.Parent == null) return;
            var siblings = panel.Parent.Controls
                .OfType<AdaptiveRowPanel>()
                .OrderBy(p => p.Top)
                .ToList();
            if (siblings.Count == 0) return;

            var rows = siblings
                .Select(p => p.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToArray())
                .ToList();
            int maxCols = rows.Max(r => r.Length);
            var maxWidths = new int[maxCols];
            for (int col = 0; col < maxCols; col++)
                foreach (var row in rows)
                    if (col < row.Length)
                        maxWidths[col] = Math.Max(maxWidths[col], row[col].Width);

            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Line Up");
            foreach (var sibling in siblings)
            {
                int g = sibling.DefaultChildHorizontalMargin;
                var controls = sibling.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToArray();
                for (int i = 0; i < controls.Length; i++)
                {
                    var child = controls[i];
                    var m = child.Margin;
                    int rightPad = Math.Max(0, maxWidths[i] - child.Width);
                    SetMargin(child, new Padding(i == 0 ? g : 0, m.Top, rightPad + g, m.Bottom), svc);
                }
                sibling.ForceRefreshLayout();
            }
            tx?.Commit();
        }

        private void ApplyUniformWidth(AdaptiveRowPanel panel, int targetWidth, string txName)
        {
            var controls = panel.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction(txName);
            foreach (var child in controls)
            {
                if (child.Width == targetWidth) continue;
                var prop = TypeDescriptor.GetProperties(child)["Width"];
                int old = child.Width;
                svc?.OnComponentChanging(child, prop);
                if (prop != null) prop.SetValue(child, targetWidth); else child.Width = targetWidth;
                svc?.OnComponentChanged(child, prop, old, targetWidth);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void MakeSameSizeLargest()
        {
            if (Panel is not { } panel) return;
            var controls = panel.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            ApplyUniformWidth(panel, controls.Max(c => c.Width), "Make Same Size (Largest)");
        }

        public void MakeSameSizeSmallest()
        {
            if (Panel is not { } panel) return;
            var controls = panel.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            ApplyUniformWidth(panel, controls.Min(c => c.Width), "Make Same Size (Smallest)");
        }

        private void ApplyUniformHeight(AdaptiveRowPanel panel, int targetHeight, string txName)
        {
            var controls = panel.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction(txName);
            foreach (var child in controls)
            {
                if (child.Height == targetHeight) continue;
                var prop = TypeDescriptor.GetProperties(child)["Height"];
                int old = child.Height;
                svc?.OnComponentChanging(child, prop);
                if (prop != null) prop.SetValue(child, targetHeight); else child.Height = targetHeight;
                svc?.OnComponentChanged(child, prop, old, targetHeight);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void MakeSameHeightLargest()
        {
            if (Panel is not { } panel) return;
            var controls = panel.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            ApplyUniformHeight(panel, controls.Max(c => c.Height), "Make Same Height (Largest)");
        }

        public void MakeSameHeightSmallest()
        {
            if (Panel is not { } panel) return;
            var controls = panel.Controls.Cast<Control>().ToArray();
            if (controls.Length == 0) return;
            ApplyUniformHeight(panel, controls.Min(c => c.Height), "Make Same Height (Smallest)");
        }

        public void SpaceEqually()
        {
            if (Panel is not { } panel) return;
            var controls = panel.Controls.Cast<Control>()
                .Where(c => c.Visible)
                .OrderBy(c => c.TabIndex)
                .ToArray();
            if (controls.Length == 0) return;
            int available = panel.ClientSize.Width - panel.Padding.Left - panel.Padding.Right;
            int totalW = controls.Sum(c => c.Width);
            int half = Math.Max(0, (available - totalW) / (controls.Length * 2));
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Space Equally");
            foreach (var child in controls)
            {
                var m = child.Margin;
                SetMargin(child, new Padding(half, m.Top, half, m.Bottom), svc);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void Center()
        {
            if (Panel is not { } panel) return;

            var controls = panel.Controls.Cast<Control>()
                .Where(c => c.Visible)
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            if (controls.Length == 0) return;

            int available = Math.Max(0, panel.ClientSize.Width - panel.Padding.Left - panel.Padding.Right);

            // Total width of all controls plus the gaps between them
            int totalWidth = controls.Sum(c => c.Width);
            for (int i = 0; i < controls.Length - 1; i++)
                totalWidth += GetDesignerGap(panel, controls[i], controls[i + 1]);

            // Include trailing margin of last control so centering is visually symmetric
            if (panel.UsePaddingFromItems)
                totalWidth += Math.Max(0, controls[^1].Margin.Right);

            int centerLeft = Math.Max(0, (available - totalWidth) / 2);

            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Center Controls");
            var m = controls[0].Margin;
            SetMargin(controls[0], new Padding(centerLeft, m.Top, m.Right, m.Bottom), svc);
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void RemoveSpacing()
        {
            if (Panel is not { } panel) return;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Remove Spacing");
            foreach (Control child in panel.Controls)
            {
                var m = child.Margin;
                SetMargin(child, new Padding(0, m.Top, 0, m.Bottom), svc);
            }
            panel.ForceRefreshLayout();
            tx?.Commit();
        }

        public void ResequenceTabIndexes()
        {
            if (Panel is not { } panel) return;
            var controls = panel.Controls.Cast<Control>()
                .OrderBy(c => c.TabIndex)
                .ToArray();
            if (controls.Length == 0) return;
            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Resequence Tab Indexes");
            for (int i = 0; i < controls.Length; i++)
            {
                var child = controls[i];
                int newIndex = i * 2;
                if (child.TabIndex == newIndex) continue;
                var prop = TypeDescriptor.GetProperties(child)["TabIndex"];
                int old = child.TabIndex;
                svc?.OnComponentChanging(child, prop);
                if (prop != null) prop.SetValue(child, newIndex); else child.TabIndex = newIndex;
                svc?.OnComponentChanged(child, prop, old, newIndex);
            }
            tx?.Commit();
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();
            items.Add(new DesignerActionHeaderItem("Sizing"));
            items.Add(new DesignerActionPropertyItem(nameof(VerticalGap), "Vertical Gap (px)", "Sizing", "Pixel gap above and below children when fitting to row height"));
            items.Add(new DesignerActionMethodItem(this, nameof(FitChildrenHeightsToRow), "Fit Children Heights to Row", "Sizing", true));
            items.Add(new DesignerActionHeaderItem("Formatting"));
            items.Add(new DesignerActionPropertyItem(nameof(GapValue), "Gap (N)", "Formatting", "Target gap between children for Set Gap. Spacing lives in the margins now — there is no persistent gap property."));
            items.Add(new DesignerActionMethodItem(this, nameof(SetGap), "Set Gap (one-shot)", "Formatting", true));
            items.Add(new DesignerActionPropertyItem(nameof(HorizontalGapAdjustment), "Margin Delta N", "Formatting", "Signed amount added to each child's left/right margin. Use negative values to shrink."));
            items.Add(new DesignerActionMethodItem(this, nameof(AdjustHorizontalGap), "Adjust Child Margins", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(LineUp), "Line Up", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(MakeSameSizeLargest), "Make Same Width (Largest)", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(MakeSameSizeSmallest), "Make Same Width (Smallest)", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(MakeSameHeightLargest), "Make Same Height (Largest)", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(MakeSameHeightSmallest), "Make Same Height (Smallest)", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(SpaceEqually), "Space Equally", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(RemoveSpacing), "Remove Spacing", "Formatting", true));
            items.Add(new DesignerActionMethodItem(this, nameof(Center), "Center", "Formatting", true));
            items.Add(new DesignerActionHeaderItem("Layout"));
            items.Add(new DesignerActionMethodItem(this, nameof(ExpandSpring), "Expand Spring", "Layout", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ResequenceTabIndexes), "Resequence Tab Indexes (0,2,4...)", "Layout", true));
            items.Add(new DesignerActionMethodItem(this, nameof(InheritParentBackColor), "Inherit Parent BackColor", "Layout", true));
            items.Add(new DesignerActionHeaderItem("Standardize"));
            items.Add(new DesignerActionPropertyItem(nameof(StandardFont), "Standard Font", "Standardize", "Font applied to every child by 'Apply Font to All'."));
            items.Add(new DesignerActionMethodItem(this, nameof(StandardizeFont), "Apply Font to All", "Standardize", true));
            items.Add(new DesignerActionPropertyItem(nameof(StandardHeight), "Standard Height (px)", "Standardize", "Height applied to every child by 'Apply Height to All'."));
            items.Add(new DesignerActionMethodItem(this, nameof(StandardizeHeight), "Apply Height to All", "Standardize", true));
            items.Add(new DesignerActionPropertyItem(nameof(StandardMargin), "Standard Margin", "Standardize", "Margin (Left/Top/Right/Bottom) applied to every child by 'Apply Margin to All'."));
            items.Add(new DesignerActionMethodItem(this, nameof(StandardizeMargin), "Apply Margin to All", "Standardize", true));
            items.Add(new DesignerActionHeaderItem("Migration"));
            items.Add(new DesignerActionMethodItem(this, nameof(AssignMissingRuleIds), "Assign Missing Rule IDs", "Migration", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ReindexRules), "Reindex Rules (add missing, prune orphans)", "Migration", true));
            return items;
        }
    }

    public sealed class AdaptiveRowPanelContentsEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            AdaptiveRowPanelContents? contents = value as AdaptiveRowPanelContents;
            if (contents == null && context?.Instance is AdaptiveRowPanel panel)
            {
                contents = panel.ContentsProxy;
            }

            if (contents == null)
            {
                return value;
            }

            var editorService = (IWindowsFormsEditorService?)provider.GetService(typeof(IWindowsFormsEditorService));
            var host = (IDesignerHost?)provider.GetService(typeof(IDesignerHost));
            var changeService = (IComponentChangeService?)provider.GetService(typeof(IComponentChangeService));

            using var dialog = new AdaptiveRowPanelContentsEditorForm(contents.Owner);
            if (editorService != null)
            {
                editorService.ShowDialog(dialog);
            }
            else
            {
                dialog.ShowDialog();
            }

            if (dialog.DialogResult != DialogResult.OK)
            {
                return value;
            }

            using var transaction = host?.CreateTransaction("Reorder AdaptiveRowPanel children");
            changeService?.OnComponentChanging(contents.Owner, null);

            var orderedControls = dialog.GetOrderedControls();
            for (int i = 0; i < orderedControls.Count; i++)
            {
                var control = orderedControls[i];
                var tabIndexProperty = TypeDescriptor.GetProperties(control)["TabIndex"];
                if (tabIndexProperty == null)
                {
                    continue;
                }

                int oldValue = control.TabIndex;
                int newValue = i * 2;
                tabIndexProperty.SetValue(control, newValue);
                changeService?.OnComponentChanged(control, tabIndexProperty, oldValue, newValue);
            }

            contents.Owner.CaptureCurrentLayoutAsBaseline();
            changeService?.OnComponentChanged(contents.Owner, null, null, null);
            transaction?.Commit();

            return value;
        }
    }
}
