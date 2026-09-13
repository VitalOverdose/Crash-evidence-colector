using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;

namespace ProfessorSnowsVideoDownloader.CustomControls;

public enum HybridPanelItemType
{
    IconButton,
    ModernButton,
    Label,
    DropdownButton,
    Spacer,
    LineBreak,
    CheckBox,
    TextBox,
    ComboBox,
    InfoButton,

    /// <summary>
    /// Any hosted native the panel can't construct itself (RoundedTextBox, numeric boxes,
    /// combos...). Listed and editable in the collection editor — name, width, margins,
    /// spring — but the sync never destroys or creates one: it can't rebuild what it can't
    /// construct, and hosted natives carry authored state a rebuild would wipe.
    /// </summary>
    Hosted,
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class HybridPanelDesignerItem : INotifyPropertyChanged
{
    private string _controlName = string.Empty;
    private HybridPanelItemType _itemType = HybridPanelItemType.IconButton;
    private string _text = string.Empty;
    private int _width = 44;
    private int _marginLeft = 4;
    private int _marginRight = 4;
    private bool _spring;

    public event PropertyChangedEventHandler? PropertyChanged;

    // The name the item held BEFORE its last ControlName edit. The collection uses it to
    // RENAME the paired control instead of spawning a fresh one — without it, a rename left
    // the old-named control orphaned in the panel and synthesized a duplicate for the new
    // name (one item, two buttons).
    internal string? PreviousControlName { get; private set; }

    [Category("Control")]
    [Description("The name of the real child control represented by this item.")]
    [DefaultValue("")]
    public string ControlName
    {
        get => _controlName;
        set
        {
            string newValue = value ?? string.Empty;
            if (string.Equals(_controlName, newValue, StringComparison.Ordinal)) return;
            PreviousControlName = _controlName;
            SetField(ref _controlName, newValue, nameof(ControlName));
        }
    }

    [Category("Control")]
    [DefaultValue(HybridPanelItemType.IconButton)]
    public HybridPanelItemType ItemType
    {
        get => _itemType;
        set => SetField(ref _itemType, value, nameof(ItemType));
    }

    [Category("Control")]
    [DefaultValue("")]
    public string Text
    {
        get => _text;
        set => SetField(ref _text, value ?? string.Empty, nameof(Text));
    }

    [Category("Layout")]
    [DefaultValue(44)]
    public int Width
    {
        get => _width;
        set => SetField(ref _width, Math.Max(1, value), nameof(Width));
    }

    [Category("Layout")]
    [DefaultValue(4)]
    public int MarginLeft
    {
        get => _marginLeft;
        set => SetField(ref _marginLeft, Math.Max(0, value), nameof(MarginLeft));
    }

    [Category("Layout")]
    [DefaultValue(4)]
    public int MarginRight
    {
        get => _marginRight;
        set => SetField(ref _marginRight, Math.Max(0, value), nameof(MarginRight));
    }

    [Category("Layout")]
    [DefaultValue(false)]
    public bool Spring
    {
        get => _spring;
        set => SetField(ref _spring, value, nameof(Spring));
    }

    public override string ToString()
        => string.IsNullOrWhiteSpace(ControlName)
            ? $"(new {ItemType})"
            : $"{ControlName} : {ItemType}";

    internal void SetControlName(string value) => _controlName = value;

    private void SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class HybridPanelDesignerItemCollection : Collection<HybridPanelDesignerItem>
{
    private readonly VirtualizingAdaptiveRowPanel _owner;
    private readonly HashSet<string> _pendingRemovals = new(StringComparer.Ordinal);

    // Control -> item pairing, RUNTIME ONLY. Never serialized, never written to Designer.cs.
    // The name is still the on-disk truth; this exists purely for the one instant where the
    // name is unreadable: an explicit surface delete un-sites the component BEFORE the
    // parent's ControlRemoved fires, so Site?.Name is null and a spacer (which stamps no
    // Name of its own) resolves to "" — the item then matched nothing and lingered in the
    // collection editor. Deliberately NOT a persisted Tag/GUID: that was the v1 ARP design
    // and it drifted (stale after rebuilds, cloned by copy/paste, and an unresolvable rule
    // SPAWNED a control). A weak table can only ever answer "which item was this control's":
    // a miss degrades to the old name match, a pasted clone is simply absent, and it holds
    // no property values, so nothing here can overwrite an authored edit.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, HybridPanelDesignerItem> _pairs = new();

    private void Pair(Control control, HybridPanelDesignerItem item)
    {
        _pairs.Remove(control);
        _pairs.Add(control, item);
    }

    // TEMP diagnostics for the item-delete desync (David's spacer resurrection) — the ARP
    // hardening channel. Remove once the removal path is proven.
    private static void DLog(string message)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arp_designer.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] [v2items] {message}{Environment.NewLine}");
        }
        catch { }
    }
    private bool _syncing;
    private bool _mirroring;
    private bool _syncQueued;

    internal HybridPanelDesignerItemCollection(VirtualizingAdaptiveRowPanel owner)
    {
        _owner = owner;
        owner.ControlAdded += Owner_ControlAdded;
        owner.ControlRemoved += Owner_ControlRemoved;
        owner.HandleCreated += (_, _) => ScheduleSync();
    }

    protected override void InsertItem(int index, HybridPanelDesignerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        DLog($"InsertItem[{index}] '{item.ControlName}' ({item.ItemType})");
        PrepareNewItem(item);
        // During designer deserialization the real named control has already been
        // constructed. Treat its concrete type as authoritative so an omitted/default
        // ItemType cannot silently morph a label, dropdown, etc. back to IconButton.
        Control? existingControl = FindControl(item.ControlName);
        if (existingControl != null
            && TryGetItemType(existingControl, out HybridPanelItemType existingType))
        {
            item.ItemType = existingType;
        }
        HybridPanelDesignerItem? duplicate = this.FirstOrDefault(rule =>
            string.Equals(rule.ControlName, item.ControlName, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            CopyItem(item, duplicate);
            ScheduleSync();
            return;
        }

        base.InsertItem(index, item);
        Subscribe(item);
        ScheduleSync();
    }

    protected override void SetItem(int index, HybridPanelDesignerItem item)
    {
        DLog($"SetItem[{index}] '{this[index].ControlName}' -> '{item.ControlName}'");
        HybridPanelDesignerItem previous = this[index];
        Unsubscribe(previous);
        QueueRemoval(previous.ControlName);
        PrepareNewItem(item);
        base.SetItem(index, item);
        Subscribe(item);
        ScheduleSync();
    }

    protected override void RemoveItem(int index)
    {
        DLog($"RemoveItem[{index}] '{this[index].ControlName}'");
        HybridPanelDesignerItem item = this[index];
        Unsubscribe(item);
        QueueRemoval(item.ControlName);
        base.RemoveItem(index);
        ScheduleSync();
    }

    protected override void ClearItems()
    {
        DLog($"ClearItems count={Count}: {string.Join(",", this.Select(r => r.ControlName))}");
        foreach (HybridPanelDesignerItem item in this)
        {
            Unsubscribe(item);
            QueueRemoval(item.ControlName);
        }
        base.ClearItems();
        ScheduleSync();
    }

    private void Owner_ControlAdded(object? sender, ControlEventArgs e)
    {
        if (!IsDesignerHosted() || _syncing || e.Control is not Control control
            || !TryGetItemType(control, out HybridPanelItemType itemType)) return;
        string name = GetControlName(control);
        if (string.IsNullOrWhiteSpace(name)
            || this.Any(rule => string.Equals(rule.ControlName, name, StringComparison.OrdinalIgnoreCase)))
            return;

        var rule = new HybridPanelDesignerItem
        {
            ItemType = itemType,
            Text = control.Text,
            Width = Math.Max(1, control.Width),
            MarginLeft = Math.Max(0, control.Margin.Left),
            MarginRight = Math.Max(0, control.Margin.Right),
            Spring = control is Control springCandidate && _owner.IsSpringChild(springCandidate),
        };
        rule.SetControlName(name);

        // Insert in TABINDEX order, not arrival order. On designer load children arrive in
        // Controls.Add order, which routinely differs from the authored TabIndex order — and
        // Synchronize writes TabIndex back FROM collection order (ApplyRule), so appending
        // here let the add-order overwrite the authored order and scramble the row. TabIndex
        // is the ordering truth; the collection mirrors it, never the reverse.
        int insertAt = 0;
        for (int i = 0; i < Count; i++)
        {
            Control? existing = FindControl(this[i].ControlName);
            if (existing != null && existing.TabIndex <= control.TabIndex)
                insertAt = i + 1;
        }

        base.InsertItem(insertAt, rule);
        Subscribe(rule);
        Pair(control, rule);
    }

    private void Owner_ControlRemoved(object? sender, ControlEventArgs e)
    {
        if (!IsDesignerHosted() || _syncing || e.Control is not Control control) return;
        string name = GetControlName(control);
        // Reference first: an explicit surface delete un-sites the control before this fires,
        // so the name can be blank (spacers always are) and the name match below silently
        // finds nothing, leaving a dead item in the collection editor. The pairing also
        // survives a rename, which the name match does not.
        bool paired = _pairs.TryGetValue(control, out HybridPanelDesignerItem? rule);
        if (paired) _pairs.Remove(control);
        DLog($"ControlRemoved '{name}' (type {control.GetType().Name}) -> {(paired ? $"paired item '{rule!.ControlName}'" : "no pairing, name match")}");
        rule ??= this.FirstOrDefault(candidate =>
            string.Equals(candidate.ControlName, name, StringComparison.OrdinalIgnoreCase));
        if (rule != null && Contains(rule))
        {
            int index = IndexOf(rule);
            Unsubscribe(rule);
            base.RemoveItem(index);
        }
    }

    private void PrepareNewItem(HybridPanelDesignerItem rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ControlName))
            rule.SetControlName(GetUniqueName(rule.ItemType));
        if (string.IsNullOrWhiteSpace(rule.Text))
        {
            if (rule.ItemType == HybridPanelItemType.ModernButton) rule.Text = "Button";
            if (rule.ItemType == HybridPanelItemType.Label) rule.Text = "Label";
            if (rule.ItemType == HybridPanelItemType.DropdownButton) rule.Text = "Dropdown";
            if (rule.ItemType == HybridPanelItemType.TextBox) rule.Text = "Text...";
            if (rule.ItemType == HybridPanelItemType.CheckBox) rule.Text = "CheckBox";
        }
    }

    private void Subscribe(HybridPanelDesignerItem rule) => rule.PropertyChanged += Item_PropertyChanged;

    private void Unsubscribe(HybridPanelDesignerItem rule) => rule.PropertyChanged -= Item_PropertyChanged;

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Ignore the echo of our own control->item mirroring.
        if (_mirroring) return;

        // A value the user typed in the collection editor goes straight to the control.
        if (sender is HybridPanelDesignerItem edited && e.PropertyName is not null)
            PushRuleValueToControl(edited, e.PropertyName);

        // A ControlName edit means RENAME the paired control — not "spawn a control with the
        // new name and orphan the old one" (that was the one-item-two-buttons bug). Rename
        // through the designer Site so the .Designer.cs serialization follows.
        if (sender is HybridPanelDesignerItem renamed
            && e.PropertyName == nameof(HybridPanelDesignerItem.ControlName)
            && !string.IsNullOrWhiteSpace(renamed.PreviousControlName))
        {
            Control? paired = FindControl(renamed.PreviousControlName);
            if (paired != null && FindControl(renamed.ControlName) == null)
            {
                if (paired.Site != null) paired.Site.Name = renamed.ControlName;
                paired.Name = renamed.ControlName;
            }
        }

        if (sender is HybridPanelDesignerItem rule && e.PropertyName == nameof(HybridPanelDesignerItem.ItemType))
        {
            rule.Width = DefaultWidth(rule.ItemType);
            if (string.IsNullOrWhiteSpace(rule.Text))
            {
                if (rule.ItemType == HybridPanelItemType.ModernButton) rule.Text = "Button";
                if (rule.ItemType == HybridPanelItemType.Label) rule.Text = "Label";
                if (rule.ItemType == HybridPanelItemType.DropdownButton) rule.Text = "Dropdown";
                if (rule.ItemType == HybridPanelItemType.TextBox) rule.Text = "Text...";
                if (rule.ItemType == HybridPanelItemType.CheckBox) rule.Text = "CheckBox";
            }
        }
        ScheduleSync();
    }

    private void QueueRemoval(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            _pendingRemovals.Add(name);
    }

    private void ScheduleSync()
    {
        if (!IsDesignerHosted() || _syncQueued || _owner.IsDisposed || _owner.Disposing) return;
        if (!_owner.IsHandleCreated)
        {
            _owner.PerformLayout();
            return;
        }

        _syncQueued = true;
        _owner.BeginInvoke((MethodInvoker)(() =>
        {
            _syncQueued = false;
            if (!_owner.IsDisposed && !_owner.Disposing)
                Synchronize();
        }));
    }

    private void Synchronize()
    {
        if (!IsDesignerHosted() || _syncing) return;
        _syncing = true;
        try
        {
            string[] activeNames = this.Select(rule => rule.ControlName).ToArray();
            DLog($"Sync: items=[{string.Join(",", activeNames)}] pending=[{string.Join(",", _pendingRemovals)}]");
            foreach (string name in _pendingRemovals.ToArray())
            {
                if (!activeNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    Control? removed = FindControl(name);
                    DLog($"Sync: destroy '{name}' -> control {(removed == null ? "NOT FOUND" : "found, destroying")}");
                    if (removed != null) DestroyControl(removed);
                }
                else
                {
                    DLog($"Sync: '{name}' still active — removal skipped");
                }
            }
            _pendingRemovals.Clear();

            // Multi-spring: EVERY item flagged Spring contributes its name, comma-joined —
            // matching the panel's v1-format parser. Collapsing to the first one silently
            // dropped the second spring of a balanced row (spacer either side of a group).
            List<string> springNames = new();
            for (int index = 0; index < Count; index++)
            {
                HybridPanelDesignerItem rule = this[index];
                Control? control = FindControl(rule.ControlName);

                // Hosted natives: apply layout values, NEVER enforce type. The panel can't
                // construct one, and destroy-and-recreate would wipe its authored state.
                if (rule.ItemType == HybridPanelItemType.Hosted)
                {
                    if (control != null)
                    {
                        Pair(control, rule);
                        ApplyRule(control, rule, index);
                        if (rule.Spring)
                            springNames.Add(rule.ControlName);
                    }

                    continue;
                }

                Type requiredType = GetControlType(rule.ItemType);
                // Subclass-tolerant on purpose: InfoButton derives from VirtualIconButton,
                // and an exact-type check would destroy it and mint a plain one on every sync.
                // A control that IS the required type (or derives from it) is the truth — only
                // a genuine type CHANGE (Label -> IconButton) rebuilds.
                if (control != null && !requiredType.IsAssignableFrom(control.GetType()))
                {
                    DestroyControl(control);
                    control = null;
                }

                // Controls are the single source of truth. A BRAND NEW control gets seeded from
                // its item; an existing one is never overwritten — the item mirrors it instead.
                // (ApplyRule used to write Text/Width/Margin on every sync, so editing a
                // control in the property grid and then toggling Spring wiped the edit —
                // David's label losing its text.)
                bool created = control == null;
                control ??= CreateControl(requiredType, rule.ControlName);
                Pair(control, rule);
                if (created)
                    ApplyRule(control, rule, index);
                else
                    MirrorControlIntoRule(control, rule, index);
                if (rule.Spring)
                    springNames.Add(rule.ControlName);
            }
            // THE SPRING EATER. This used to write unconditionally, which erased an authored
            // SpringControlName on any panel whose Items were never serialized: the collection
            // has nothing in it, so springNames is empty, so the panel's own spring is replaced
            // with "" and the designer then drops the line as a default. That is how
            // SplitBarLayoutPanel silently lost `SpringControlName = "bookmarksShared"` and the
            // browser's bookmarks bar stopped stretching - triggered by nothing more than
            // OPENING the collection editor to look at it.
            //
            // An EMPTY collection means "this sync knows nothing about the panel", not "the
            // panel has no spring", so it must not speak. An empty springNames with items
            // present is different - that is the user genuinely clearing the last spring, and
            // it still writes, so a spring can be turned off in the editor as before.
            // No item claims the spring, but the PANEL still names one and that control is
            // really there? Then the items are wrong, not the panel. Items are never serialised
            // in v2 (Controls are the only truth), so they are rebuilt by adoption on every
            // designer visit - and if adoption misses the flag for any reason, writing the
            // items' view back would erase an authored spring PERMANENTLY, because the designer
            // then drops the line as a default. Re-derive from the panel instead, the way
            // VStack's SyncSpringFlagsFromRules does.
            if (springNames.Count == 0 && !string.IsNullOrWhiteSpace(_owner.SpringControlName))
            {
                foreach (string authored in _owner.SpringControlName
                             .Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string wanted = authored.Trim();
                    if (FindControl(wanted) == null)
                        continue;

                    // An item that EXISTS with Spring off is the user's decision - they can see
                    // that checkbox in the editor, and unticking the last spring has to be
                    // possible. Only re-derive when the collection has no item for this control
                    // at all, which means adoption never covered it and the items simply do not
                    // know about the spring. That is the case that ate bookmarksShared.
                    bool known = this.Any(item =>
                        string.Equals(item.ControlName, wanted, StringComparison.OrdinalIgnoreCase));

                    if (known)
                        continue;

                    springNames.Add(wanted);
                }
            }

            if (Count > 0 || springNames.Count > 0)
            {
                _owner.SpringControlName = string.Join(",", springNames);
            }

            _owner.PerformLayout();
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool IsDesignerHosted()
    {
        for (Control? control = _owner; control != null; control = control.Parent)
        {
            if (control.Site?.DesignMode == true) return true;
        }
        return false;
    }

    private Control CreateControl(Type type, string name)
    {
        var host = (IDesignerHost?)_owner.Site?.GetService(typeof(IDesignerHost));
        Control control = host?.CreateComponent(type, name) as Control
            ?? (Control)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not create {type.Name}."));
        if (string.IsNullOrWhiteSpace(control.Name)) control.Name = name;
        ApplyCreatedDefaults(control);
        _owner.Controls.Add(control);
        return control;
    }

    private void DestroyControl(Control control)
    {
        var host = (IDesignerHost?)_owner.Site?.GetService(typeof(IDesignerHost));
        if (host != null && control.Site != null)
            host.DestroyComponent(control);
        else
        {
            _owner.Controls.Remove(control);
            control.Dispose();
        }
    }

    /// <summary>
    /// Existing control -> item. Ordering still flows the other way (TabIndex from collection
    /// order), but every VALUE the user can edit on the control wins over the item's copy.
    /// </summary>
    private void MirrorControlIntoRule(Control control, HybridPanelDesignerItem rule, int index)
    {
        control.TabIndex = index;

        _mirroring = true;
        try
        {
            rule.Text = control.Text ?? string.Empty;
            rule.Width = Math.Max(1, control.Width);
            rule.MarginLeft = Math.Max(0, control.Margin.Left);
            rule.MarginRight = Math.Max(0, control.Margin.Right);
        }
        finally
        {
            _mirroring = false;
        }
    }

    /// <summary>Item -> control for ONE property the user just edited in the collection editor.</summary>
    private void PushRuleValueToControl(HybridPanelDesignerItem rule, string propertyName)
    {
        Control? control = FindControl(rule.ControlName);
        if (control == null) return;

        switch (propertyName)
        {
            case nameof(HybridPanelDesignerItem.Text):
                if (control.Text != rule.Text) control.Text = rule.Text;
                break;
            case nameof(HybridPanelDesignerItem.Width):
                if (control.Width != rule.Width) control.Width = rule.Width;
                break;
            case nameof(HybridPanelDesignerItem.MarginLeft):
            case nameof(HybridPanelDesignerItem.MarginRight):
                control.Margin = new Padding(rule.MarginLeft, control.Margin.Top,
                    rule.MarginRight, control.Margin.Bottom);
                break;
        }
    }

    private static void ApplyRule(Control control, HybridPanelDesignerItem rule, int index)
    {
        control.TabIndex = index;
        control.Width = rule.Width;
        control.Margin = new Padding(rule.MarginLeft, control.Margin.Top,
            rule.MarginRight, control.Margin.Bottom);
        if (!string.IsNullOrEmpty(rule.Text) || control is TextBox or RoundedTextBox)
            control.Text = rule.Text;
    }

    private static void ApplyCreatedDefaults(Control control)
    {
        switch (control)
        {
            case VirtualAdaptiveRowLabel label:
                label.Size = new Size(80, 28);
                break;
            case VirtualRoundedDropdownButton dropdownButton:
                dropdownButton.Size = new Size(140, 36);
                break;
            case VirtualModernButton modernButton:
                modernButton.Size = new Size(150, 40);
                break;
            case InfoButton infoButton:
                infoButton.Size = new Size(28, 28);
                break;
            case VirtualIconButton iconButton:
                iconButton.Size = new Size(44, 44);
                break;
            case VirtualAdaptiveRowSpacer spacer:
                spacer.Size = new Size(48, 30);
                break;
            case VirtualAdaptiveLineBreak lineBreak:
                lineBreak.Size = new Size(12, 42);
                break;
            case VirtualRoundedCheckBox checkBox:
                checkBox.Size = new Size(140, 30);
                break;
            case RoundedComboBox comboBox:
                comboBox.Size = new Size(160, 32);
                break;
            case RoundedTextBox roundedTextBox:
                roundedTextBox.Size = new Size(180, 32);
                break;
            case TextBox textBox:
                textBox.Size = new Size(180, textBox.Height);
                break;
        }
    }

    private string GetUniqueName(HybridPanelItemType itemType)
    {
        string prefix = itemType switch
        {
            HybridPanelItemType.IconButton => "virtualIconButton",
            HybridPanelItemType.ModernButton => "virtualModernButton",
            HybridPanelItemType.Label => "virtualLabel",
            HybridPanelItemType.DropdownButton => "virtualDropdownButton",
            HybridPanelItemType.Spacer => "virtualSpacer",
            HybridPanelItemType.LineBreak => "virtualLineBreak",
            HybridPanelItemType.CheckBox => "virtualCheckBox",
            HybridPanelItemType.TextBox => "roundedTextBox",
            HybridPanelItemType.ComboBox => "roundedComboBox",
            HybridPanelItemType.InfoButton => "infoButton",
            _ => "hostedTextBox",
        };
        int index = 1;
        string candidate;
        // Unique across the whole FORM, not just this panel — IDesignerHost.CreateComponent
        // throws on a name any other component already holds.
        var container = _owner.Site?.Container;
        do candidate = $"{prefix}{index++}";
        while (FindControl(candidate) != null
            || container?.Components[candidate] != null
            || this.Any(rule => string.Equals(rule.ControlName, candidate, StringComparison.OrdinalIgnoreCase)));
        return candidate;
    }

    private Control? FindControl(string name) => _owner.Controls.Cast<Control>()
        .FirstOrDefault(control => string.Equals(GetControlName(control), name,
            StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Re-derives every item's Spring flag from the panel's SpringControlName. Called by the
    /// panel's setter so a deserialized spring survives whatever order the designer replays
    /// Controls.Add versus panel properties in (adoption ran first and seeded Spring=false,
    /// and the deferred item->panel sync then stomped the authored string with "" — the
    /// spring-eater). Convergent, not circular: flag changes ripple through the normal sync,
    /// which recomputes the SAME spring string.
    /// </summary>
    internal void SyncSpringFlagsFromPanel()
    {
        foreach (HybridPanelDesignerItem item in this)
        {
            Control? control = FindControl(item.ControlName);
            bool shouldSpring = control != null && _owner.IsSpringChild(control);
            if (item.Spring != shouldSpring)
                item.Spring = shouldSpring;
        }
    }

    private static string GetControlName(Control control)
        => control.Site?.Name ?? control.Name ?? string.Empty;

    private static int DefaultWidth(HybridPanelItemType itemType) => itemType switch
    {
        HybridPanelItemType.IconButton => 44,
        HybridPanelItemType.ModernButton => 150,
        HybridPanelItemType.Label => 80,
        HybridPanelItemType.DropdownButton => 140,
        HybridPanelItemType.Spacer => 48,
        HybridPanelItemType.LineBreak => 10,
        HybridPanelItemType.CheckBox => 140,
        HybridPanelItemType.TextBox => 180,
        HybridPanelItemType.ComboBox => 160,
        HybridPanelItemType.InfoButton => 28,
        HybridPanelItemType.Hosted => 120,
        _ => 180,
    };

    private static Type GetControlType(HybridPanelItemType itemType) => itemType switch
    {
        HybridPanelItemType.IconButton => typeof(VirtualIconButton),
        HybridPanelItemType.ModernButton => typeof(VirtualModernButton),
        HybridPanelItemType.Label => typeof(VirtualAdaptiveRowLabel),
        HybridPanelItemType.DropdownButton => typeof(VirtualRoundedDropdownButton),
        HybridPanelItemType.Spacer => typeof(VirtualAdaptiveRowSpacer),
        HybridPanelItemType.LineBreak => typeof(VirtualAdaptiveLineBreak),
        HybridPanelItemType.CheckBox => typeof(VirtualRoundedCheckBox),
        HybridPanelItemType.ComboBox => typeof(RoundedComboBox),
        HybridPanelItemType.InfoButton => typeof(InfoButton),
        _ => typeof(RoundedTextBox),
    };

    private static bool TryGetItemType(Control control, out HybridPanelItemType itemType)
    {
        itemType = control switch
        {
            // InfoButton derives from VirtualIconButton — its case must come first or the
            // base pattern claims it and sync would retype it to a plain icon button.
            Control info when info.GetType() == typeof(InfoButton) => HybridPanelItemType.InfoButton,
            VirtualIconButton => HybridPanelItemType.IconButton,
            VirtualModernButton => HybridPanelItemType.ModernButton,
            VirtualAdaptiveRowLabel => HybridPanelItemType.Label,
            VirtualRoundedDropdownButton => HybridPanelItemType.DropdownButton,
            VirtualAdaptiveRowSpacer => HybridPanelItemType.Spacer,
            VirtualAdaptiveLineBreak => HybridPanelItemType.LineBreak,
            VirtualRoundedCheckBox => HybridPanelItemType.CheckBox,
            // EXACT type only: RoundedNumericTextBox derives from RoundedTextBox, and treating
            // it as the TextBox item would have sync replace it with a plain one.
            Control exact when exact.GetType() == typeof(RoundedTextBox) => HybridPanelItemType.TextBox,
            Control plain when plain.GetType() == typeof(TextBox) => HybridPanelItemType.TextBox,
            Control combo when combo.GetType() == typeof(RoundedComboBox) => HybridPanelItemType.ComboBox,
            // EVERY other child is a hosted native (RoundedTextBox, numerics, combos...).
            // They're row members like anything else, so they belong in the editor —
            // previously they were rejected here and simply never appeared.
            _ => HybridPanelItemType.Hosted,
        };
        return true;
    }

    private static void CopyItem(HybridPanelDesignerItem source, HybridPanelDesignerItem target)
    {
        target.ItemType = source.ItemType;
        target.Text = source.Text;
        target.Width = source.Width;
        target.MarginLeft = source.MarginLeft;
        target.MarginRight = source.MarginRight;
        target.Spring = source.Spring;
    }

    internal void MatchControlOrder(IEnumerable<Control> controls)
    {
        var byName = this.ToDictionary(item => item.ControlName, StringComparer.OrdinalIgnoreCase);
        HybridPanelDesignerItem[] ordered = controls
            .Select(control => byName.GetValueOrDefault(GetControlName(control)))
            .Where(item => item != null)
            .Cast<HybridPanelDesignerItem>()
            .ToArray();
        if (ordered.Length != Count) return;
        Items.Clear();
        foreach (HybridPanelDesignerItem item in ordered)
            Items.Add(item);
        ScheduleSync();
    }
}
