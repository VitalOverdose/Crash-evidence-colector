using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using Microsoft.DotNet.DesignTools.Designers;

namespace ProfessorSnowsVideoDownloader.CustomControls;

[Editor(typeof(HybridPanelItemsEditor), typeof(UITypeEditor))]
[TypeConverter(typeof(AdaptivePanelItemCollectionConverter))]
public sealed class AdaptivePanelItemCollection : IReadOnlyList<Control>
{
    private readonly VirtualizingAdaptiveRowPanel _owner;

    internal AdaptivePanelItemCollection(VirtualizingAdaptiveRowPanel owner) => _owner = owner;

    public int Count => Ordered().Length;

    public Control this[int index] => Ordered()[index];

    public void Add(Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var ordered = Ordered().Where(control => !ReferenceEquals(control, item)).ToList();
        _owner.Controls.Add(item);
        ordered.Add(item);
        SetOrder(ordered);
    }

    public void Insert(int index, Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Control[] controls = Ordered();
        index = Math.Clamp(index, 0, controls.Length);
        _owner.Controls.Add(item);
        var ordered = controls.ToList();
        ordered.Insert(index, item);
        SetOrder(ordered);
    }

    public bool Remove(Control item)
    {
        if (!_owner.Controls.Contains(item)) return false;
        _owner.Controls.Remove(item);
        SetOrder(Ordered());
        return true;
    }

    public void Move(int oldIndex, int newIndex)
    {
        var ordered = Ordered().ToList();
        if ((uint)oldIndex >= (uint)ordered.Count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        newIndex = Math.Clamp(newIndex, 0, ordered.Count - 1);
        Control item = ordered[oldIndex];
        ordered.RemoveAt(oldIndex);
        ordered.Insert(newIndex, item);
        SetOrder(ordered);
    }

    public IEnumerator<Control> GetEnumerator() => ((IEnumerable<Control>)Ordered()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal Control[] Ordered() => _owner.Controls.Cast<Control>()
        .OrderBy(control => control.TabIndex)
        .ThenBy(control => _owner.Controls.GetChildIndex(control))
        .ToArray();

    internal void SetOrder(IEnumerable<Control> controls)
    {
        Control[] ordered = controls.ToArray();
        int index = 0;
        foreach (Control control in ordered)
            control.TabIndex = index++;
        _owner.DesignerItems.MatchControlOrder(ordered);
        _owner.PerformLayout();
        _owner.Invalidate();
    }

    public override string ToString() => Count == 1 ? "1 item" : $"{Count} items";
}

internal sealed class AdaptivePanelItemCollectionConverter : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context,
        System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is AdaptivePanelItemCollection items
            ? items.ToString()
            : base.ConvertTo(context, culture, value, destinationType);

    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => false;
}

public sealed class HybridPanelItemsEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => UITypeEditorEditStyle.Modal;

    public override object? EditValue(ITypeDescriptorContext? context,
        IServiceProvider provider, object? value)
    {
        VirtualizingAdaptiveRowPanel? panel = context?.Instance as VirtualizingAdaptiveRowPanel;
        if (panel == null && value is AdaptivePanelItemCollection items)
            panel = items.Ordered().FirstOrDefault()?.Parent as VirtualizingAdaptiveRowPanel;
        if (panel != null)
            EditPanel(panel, provider);
        return value;
    }

    internal static void EditPanel(VirtualizingAdaptiveRowPanel panel, IServiceProvider provider)
    {
        var editorService = (IWindowsFormsEditorService?)provider.GetService(typeof(IWindowsFormsEditorService));
        var host = (IDesignerHost?)provider.GetService(typeof(IDesignerHost));
        var changes = (IComponentChangeService?)provider.GetService(typeof(IComponentChangeService));

        using var dialog = new HybridPanelItemsEditorForm(panel.Items.Ordered());
        if (editorService == null)
            return;

        DialogResult result = editorService.ShowDialog(dialog);
        if (result != DialogResult.OK) return;

        PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(panel)["Controls"];
        try
        {
            changes?.OnComponentChanging(panel, controlsProperty);
        }
        catch (CheckoutException exception) when (exception == CheckoutException.Canceled)
        {
            return;
        }

        using DesignerTransaction? transaction = host?.CreateTransaction("Edit hybrid panel items");
        Control[] original = panel.Items.Ordered();
        var finalControls = new List<Control>();

        foreach (HybridPanelItemEntry entry in dialog.Entries)
        {
            Control control;
            if (entry.Control != null)
            {
                control = entry.Control;
            }
            else
            {
                control = CreateControl(entry.ControlType!, host);
                ApplyUsefulDefaults(control, panel);
            }
            if (control.Parent != panel)
                panel.Controls.Add(control);
            finalControls.Add(control);
        }

        foreach (Control removed in original.Except(finalControls).ToArray())
        {
            if (host != null && removed.Site != null)
                host.DestroyComponent(removed);
            else
            {
                panel.Controls.Remove(removed);
                removed.Dispose();
            }
        }

        panel.Items.SetOrder(finalControls);
        changes?.OnComponentChanged(panel, controlsProperty, null, null);
        transaction?.Commit();
    }

    private static Control CreateControl(Type type, IDesignerHost? host)
    {
        if (host?.CreateComponent(type) is Control designedControl)
            return designedControl;
        return (Control)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create {type.Name}."));
    }

    private static void ApplyUsefulDefaults(Control control, VirtualizingAdaptiveRowPanel panel)
    {
        if (control is VirtualIconButton iconButton && iconButton.Size == default)
            iconButton.Size = new Size(44, 44);
        else if (control is VirtualAdaptiveRowSpacer spacer)
            spacer.Size = spacer.Size.IsEmpty ? new Size(48, 30) : spacer.Size;
        else if (control is VirtualAdaptiveLineBreak line)
            line.Size = line.Size.IsEmpty ? new Size(12, 42) : line.Size;
        else if (control is TextBox textBox)
        {
            textBox.Width = Math.Max(180, textBox.Width);
            textBox.Font = panel.Font;
        }
    }
}

internal sealed class HybridPanelItemsEditorForm : Form
{
    private static readonly (string Name, Type Type)[] Choices =
    {
        ("Virtual icon button", typeof(VirtualIconButton)),
        ("Virtual spacer", typeof(VirtualAdaptiveRowSpacer)),
        ("Virtual line break", typeof(VirtualAdaptiveLineBreak)),
        ("Hosted textbox", typeof(TextBox)),
    };

    private readonly ComboBox _typePicker;
    private readonly ListBox _items;
    private readonly Button _up;
    private readonly Button _down;
    private readonly Button _remove;

    public HybridPanelItemsEditorForm(IEnumerable<Control> controls)
    {
        Text = "Hybrid Panel Items";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 360);

        _typePicker = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _typePicker.Items.AddRange(Choices.Select(choice => (object)choice.Name).ToArray());
        _typePicker.SelectedIndex = 0;
        var add = new Button { Text = "Add", Dock = DockStyle.Right, Width = 82 };
        add.Click += (_, _) => AddItem();
        var addRow = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(8, 5, 8, 3) };
        addRow.Controls.Add(_typePicker);
        addRow.Controls.Add(add);

        _items = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (Control control in controls)
            _items.Items.Add(new HybridPanelItemEntry(control));
        _items.SelectedIndexChanged += (_, _) => UpdateButtons();

        _up = new Button { Text = "Up", Width = 84 };
        _down = new Button { Text = "Down", Width = 84 };
        _remove = new Button { Text = "Remove", Width = 84 };
        _up.Click += (_, _) => MoveSelected(-1);
        _down.Click += (_, _) => MoveSelected(1);
        _remove.Click += (_, _) => RemoveSelected();
        var side = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 100,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(6),
            WrapContents = false
        };
        side.Controls.AddRange(new Control[] { _up, _down, _remove });

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 84 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 84 };
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(ok);

        var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        center.Controls.Add(_items);
        center.Controls.Add(side);
        Controls.Add(center);
        Controls.Add(bottom);
        Controls.Add(addRow);
        AcceptButton = ok;
        CancelButton = cancel;
        if (_items.Items.Count > 0) _items.SelectedIndex = 0;
        UpdateButtons();
    }

    public IReadOnlyList<HybridPanelItemEntry> Entries
        => _items.Items.Cast<HybridPanelItemEntry>().ToArray();

    private void AddItem()
    {
        int choiceIndex = Math.Max(0, _typePicker.SelectedIndex);
        var entry = new HybridPanelItemEntry(Choices[choiceIndex].Type);
        int index = _items.SelectedIndex < 0 ? _items.Items.Count : _items.SelectedIndex + 1;
        _items.Items.Insert(index, entry);
        _items.SelectedIndex = index;
    }

    private void MoveSelected(int direction)
    {
        int index = _items.SelectedIndex;
        int destination = index + direction;
        if (index < 0 || destination < 0 || destination >= _items.Items.Count) return;
        object item = _items.Items[index];
        _items.Items.RemoveAt(index);
        _items.Items.Insert(destination, item);
        _items.SelectedIndex = destination;
    }

    private void RemoveSelected()
    {
        int index = _items.SelectedIndex;
        if (index < 0) return;
        _items.Items.RemoveAt(index);
        if (_items.Items.Count > 0)
            _items.SelectedIndex = Math.Min(index, _items.Items.Count - 1);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int index = _items.SelectedIndex;
        _up.Enabled = index > 0;
        _down.Enabled = index >= 0 && index < _items.Items.Count - 1;
        _remove.Enabled = index >= 0;
    }
}

internal sealed class HybridPanelItemEntry
{
    public HybridPanelItemEntry(Control control) => Control = control;
    public HybridPanelItemEntry(Type controlType) => ControlType = controlType;

    public Control? Control { get; }
    public Type? ControlType { get; }

    public override string ToString()
    {
        if (Control != null)
        {
            string name = string.IsNullOrWhiteSpace(Control.Name) ? "(unnamed)" : Control.Name;
            return $"{name} : {Control.GetType().Name}";
        }
        return $"(new) {ControlType!.Name}";
    }
}
