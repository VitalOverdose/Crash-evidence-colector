using System.ComponentModel;
using System.ComponentModel.Design;

namespace ProfessorSnowsVideoDownloader.CustomControls;

/// <summary>
/// DESIGN-TIME migration: turns a v1 <see cref="AdaptiveRowPanel"/> row into a
/// <see cref="VirtualizingAdaptiveRowPanel"/> in place, keeping its children — retyping the ones
/// that have Virtual twins and leaving the rest hosted.
///
/// Built because every conversion so far was hand-editing Designer.cs (David: "why not add some
/// code to the vstack to do the conversion?"). Two entry points use it: the vStack rule sync
/// (changing a row's ItemType to the virtual panel MIGRATES instead of destroying the row) and
/// the vStack smart tag's "Virtualize eligible rows" action.
///
/// NAMES ARE PRESERVED, which is what makes this safe: code-behind wires events by field name
/// (btnFoo.Click += ...), so a retyped child keeps every handler it had. Virtual twins subclass
/// the real controls, so property blocks survive too.
/// </summary>
internal static class RowVirtualizer
{
    /// <summary>Real type -> its Virtual twin. Anything absent stays hosted.</summary>
    private static readonly Dictionary<Type, Type> VirtualTwins = new()
    {
        [typeof(IconButton)] = typeof(VirtualIconButton),
        [typeof(ModernButton)] = typeof(VirtualModernButton),
        [typeof(AdaptiveRowLabel)] = typeof(VirtualAdaptiveRowLabel),
        [typeof(AdaptiveLineBreak)] = typeof(VirtualAdaptiveLineBreak),
        [typeof(AdaptiveRowSpacer)] = typeof(VirtualAdaptiveRowSpacer),
        [typeof(RoundedCheckBox)] = typeof(VirtualRoundedCheckBox),
        [typeof(RoundedDropdownButton)] = typeof(VirtualRoundedDropdownButton),
        [typeof(RoundedComboBox)] = typeof(VirtualRoundedComboBox),
        // Cross-type: a raw WinForms Label becomes the house label (David: rows with native
        // labels were refused). AdaptiveRowLabel is a ModernButton, not a Label, so the
        // colour needs the explicit ForeColor -> TextColor fixup in MigrateChild.
        [typeof(Label)] = typeof(VirtualAdaptiveRowLabel),
    };

    /// <summary>
    /// Types allowed in a convertible row. The Virtual-twin list above plus the natives that
    /// legitimately keep their handles (text entry, lists, progress) — David's list.
    /// </summary>
    private static readonly Type[] HostedButAllowed =
    {
        typeof(RoundedTextBox), typeof(TextBox),
        typeof(RoundedMultiSelectComboBox), typeof(ComboBox),
        typeof(RoundedNumericTextBox), typeof(NumericUpDown),
        typeof(RoundedProgressBar), typeof(ProgressBar),
        typeof(RoundedToggleSwitch),
    };

    /// <summary>A child the converter understands (either retyped or left hosted).</summary>
    private static bool IsConvertibleChild(Control child)
    {
        Type type = child.GetType();
        if (VirtualTwins.ContainsKey(type)) return true;
        if (HostedButAllowed.Contains(type)) return true;

        // Subclasses of an allowed type are fine too (already-virtual children included).
        return VirtualTwins.Keys.Concat(HostedButAllowed).Any(allowed => allowed.IsAssignableFrom(type));
    }

    /// <summary>
    /// Can this row convert? STRUCTURAL only — is it a v1 row that isn't already virtual.
    ///
    /// It used to refuse the whole row over a single unrecognised child, which made one ListView
    /// enough to block a row of twelve convertible buttons (David: "a pain in the ass"). That was
    /// never a technical limit — MigrateChild has always just reparented anything without a twin,
    /// and v2's layout handles any Control, only hiding the ones implementing
    /// IAdaptiveVirtualControl. The refusal was enforcing the container taxonomy (rows hold
    /// normal controls; display surfaces live in plain panels), which is a judgement, not a
    /// constraint.
    ///
    /// So it converts what it can and hosts the rest. Callers report what stayed hosted via
    /// <see cref="ListHostedChildren"/> — the warning moves to the summary rather than blocking.
    /// </summary>
    public static bool CanVirtualize(Control row, out string? reason)
    {
        reason = null;
        if (row is VirtualizingAdaptiveRowPanel)
        {
            reason = "already virtual";
            return false;
        }
        if (row is not AdaptiveRowPanel)
        {
            reason = "not an AdaptiveRowPanel";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Does this row own a working DIVIDER — a splitter line break, or a piston rule for one to
    /// drag? v2 has no splitter or piston support whatsoever (v1: 9 and 16 references; v2: zero),
    /// and that logic lives in v1's engine, not in the controls.
    ///
    /// This is the silent failure the converter can't otherwise see: AdaptiveLineBreak HAS a
    /// virtual twin, so a row with a live splitter converts perfectly cleanly and simply stops
    /// dragging, with nothing reporting it. Callers refuse or warn loudly.
    ///
    /// <paramref name="detail"/> names what was found, for the summary.
    /// </summary>
    public static bool HasDivider(Control row, out string? detail)
    {
        detail = null;

        // SHELL DIVERGENCE: this shell's AdaptiveLineBreak predates splitter Roles - every
        // line break is a plain separator, so no row has a divider to detect.
        _ = row;

        // A piston is a RULE flag, not a control property, so it has to come from the row's own
        // item rules rather than from walking the children.
        // SHELL DIVERGENCE: this shell's v1 predates SpecialLayout/piston rules, so there
        // is nothing to detect on a v1 row here.

        return false;
    }

    /// <summary>
    /// Children that will KEEP their window handle after conversion, split by how surprising
    /// that is:
    ///
    ///   Expected   — on the allowed-hosted list: text entry, combos, progress. These genuinely
    ///                need their handles; nothing is wrong.
    ///   Unexpected — everything else: a display surface, a nested container, a custom control
    ///                the renderers know nothing about. Worth flagging, because the whole point
    ///                of a virtual row is that it has no handles in it.
    /// </summary>
    public static (List<string> Expected, List<string> Unexpected) ListHostedChildren(Control row)
    {
        var expected = new List<string>();
        var unexpected = new List<string>();

        foreach (Control child in row.Controls)
        {
            Type type = child.GetType();
            if (child is IAdaptiveVirtualControl || VirtualTwins.ContainsKey(type))
            {
                continue;   // becomes virtual, or already is
            }

            string label = $"{child.Name} ({type.Name})";
            if (IsConvertibleChild(child))
            {
                expected.Add(label);
            }
            else
            {
                unexpected.Add(label);
            }
        }

        return (expected, unexpected);
    }

    /// <summary>
    /// Converts the row. Returns the new panel, or null when it could not (see CanVirtualize).
    /// The caller owns the DesignerTransaction — this may run many times inside one.
    /// </summary>
    public static VirtualizingAdaptiveRowPanel? Convert(
        AdaptiveRowPanel oldRow, IDesignerHost host, IComponentChangeService? changeService)
    {
        if (!CanVirtualize(oldRow, out _)) return null;

        Control parent = oldRow.Parent!;
        if (parent == null) return null;

        // ORDER MATTERS: destroy the old component under its ORIGINAL name before creating
        // the twin. The first version renamed the old row aside ("__migrating"), created the
        // twin, then destroyed the alias — but VS's serializer tracks field declarations
        // incrementally BY NAME, so the original name's declaration was never removed and the
        // twin added a second one: duplicate-field CS0102s in every converted file.
        string rowName = oldRow.Name;
        object? rowTag = oldRow.Tag;
        int childIndex = parent.Controls.GetChildIndex(oldRow);
        string? springNames = oldRow.Spring;
        var rowSnapshot = SnapshotProperties(oldRow);

        // Children out first — destroying the row must not take them with it.
        var oldChildren = oldRow.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToList();
        foreach (Control child in oldChildren)
        {
            oldRow.Controls.Remove(child);
        }

        PropertyDescriptor? parentControls = TypeDescriptor.GetProperties(parent)["Controls"];
        changeService?.OnComponentChanging(parent, parentControls);

        parent.Controls.Remove(oldRow);
        host.DestroyComponent(oldRow);

        var newRow = host.CreateComponent(typeof(VirtualizingAdaptiveRowPanel), rowName)
            as VirtualizingAdaptiveRowPanel;
        if (newRow == null)
        {
            // Old row is gone; the children still exist unparented. Don't guess — surface it.
            changeService?.OnComponentChanged(parent, parentControls, null, null);
            return null;
        }

        ApplySnapshot(rowSnapshot, newRow);
        newRow.Tag = rowTag;

        foreach (Control child in oldChildren)
        {
            newRow.Controls.Add(MigrateChild(child, host, changeService));
        }

        parent.Controls.Add(newRow);
        parent.Controls.SetChildIndex(newRow, childIndex);
        changeService?.OnComponentChanged(parent, parentControls, null, newRow);

        // v1 spring names carry over verbatim (same comma syntax).
        if (!string.IsNullOrWhiteSpace(springNames))
            newRow.SpringControlName = springNames;

        newRow.PerformLayout();
        newRow.Invalidate();
        return newRow;
    }

    /// <summary>
    /// Retypes the children of a panel that is ALREADY a VirtualizingAdaptiveRowPanel, in place.
    /// Drop ordinary controls into a virtual row and turn them virtual without rebuilding the row.
    ///
    /// Unlike <see cref="Convert"/> this is deliberately PARTIAL. Convert refuses a whole row over
    /// one unknown child, because it is swapping the panel and cannot half-do that. Here the panel
    /// already exists, so anything without a twin — a text box, a combo, a progress bar — simply
    /// stays hosted, which is a legitimate end state rather than a failure. Returns how many were
    /// retyped.
    ///
    /// Names are preserved by MigrateChild, which is what keeps code-behind working: handlers are
    /// wired by field name, so a retyped child keeps every one of them.
    ///
    /// The caller owns the DesignerTransaction.
    /// </summary>
    public static int ConvertChildrenInPlace(
        VirtualizingAdaptiveRowPanel panel, IDesignerHost host, IComponentChangeService? changeService)
    {
        if (panel == null || host == null)
        {
            return 0;
        }

        // Snapshot first: MigrateChild reparents, so iterating the live collection would skip.
        // TabIndex order because that is what v2 lays out by.
        var children = panel.Controls.Cast<Control>().OrderBy(child => child.TabIndex).ToList();

        PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(panel)["Controls"];
        changeService?.OnComponentChanging(panel, controlsProperty);

        int converted = 0;
        foreach (Control child in children)
        {
            // Already virtual, or nothing to become — leave it exactly where it is. Calling
            // MigrateChild on a hosted control would unparent it for no gain.
            if (child is IAdaptiveVirtualControl || !VirtualTwins.ContainsKey(child.GetType()))
            {
                continue;
            }

            // Child index restored explicitly: the twin is a NEW component appended at the end,
            // and although TabIndex drives layout, Controls order is the tie-break for equal
            // TabIndexes and the order the items editor shows.
            int childIndex = panel.Controls.GetChildIndex(child);
            Control twin = MigrateChild(child, host, changeService);
            panel.Controls.Add(twin);
            panel.Controls.SetChildIndex(twin, childIndex);
            converted++;
        }

        changeService?.OnComponentChanged(panel, controlsProperty, null, null);

        panel.PerformLayout();
        panel.Invalidate();
        return converted;
    }

    /// <summary>How many of a panel's children could still be retyped, and how many cannot.</summary>
    public static (int Convertible, int AlreadyVirtual, int Hosted) SurveyChildren(Control panel)
    {
        int convertible = 0, alreadyVirtual = 0, hosted = 0;
        foreach (Control child in panel.Controls)
        {
            if (child is IAdaptiveVirtualControl) alreadyVirtual++;
            else if (VirtualTwins.ContainsKey(child.GetType())) convertible++;
            else hosted++;
        }

        return (convertible, alreadyVirtual, hosted);
    }

    private static Control MigrateChild(Control child, IDesignerHost host, IComponentChangeService? changeService)
    {
        Type childType = child.GetType();
        if (!VirtualTwins.TryGetValue(childType, out Type? twinType))
        {
            // Hosted (or already virtual): just move it — reparenting keeps everything.
            child.Parent?.Controls.Remove(child);
            return child;
        }

        // Snapshot, destroy under the ORIGINAL name, recreate — same serializer reason as the
        // row itself (see Convert).
        string name = child.Name;
        var snapshot = SnapshotProperties(child);
        child.Parent?.Controls.Remove(child);
        host.DestroyComponent(child);

        Control? twin = null;
        try { twin = host.CreateComponent(twinType, name) as Control; }
        catch { twin = null; }

        if (twin == null)
        {
            // Could not mint the twin and the original is gone — rebuild the original type
            // from the snapshot rather than lose the control.
            var fallback = (Control)host.CreateComponent(childType, name)!;
            ApplySnapshot(snapshot, fallback);
            return fallback;
        }

        ApplySnapshot(snapshot, twin);

        // Cross-type fixups: names copy blindly, semantics sometimes don't.
        if (childType == typeof(Label) && twin is AdaptiveRowLabel label)
        {
            // A Label's text colour is ForeColor; the house label PAINTS from TextColor.
            object? foreColor = snapshot.FirstOrDefault(entry => entry.Name == "ForeColor").Value;
            if (foreColor is Color color)
                label.TextColor = color;
            // Face colour follows the surface by default (AutoInheritSurfaceColor) — a raw
            // Label's opaque BackColor would read as a painted box otherwise.
            label.AutoInheritSurfaceColor = true;

            // TopLeft is Label's DEFAULT, not a decision — nobody top-aligns text in a 30px
            // row. The house default is MiddleLeft (David). Deliberate alignments survive.
            if (label.TextAlign == ContentAlignment.TopLeft)
                label.TextAlign = ContentAlignment.MiddleLeft;

            // David sizes controls in the property grid — a copied AutoSize=true from the
            // source Label would snap the size back and fight every Height he types.
            label.AutoSize = false;
        }

        return twin;
    }

    /// <summary>
    /// Captures every designer-serializable property value so the source can be DESTROYED
    /// before its replacement exists (the serializer needs the name freed first). Properties
    /// the target lacks (v1-only: StretchChildHeight, ItemGap, ItemRules...) skip themselves
    /// on apply — the behaviour the hand conversions did by grep.
    /// </summary>
    private static List<(string Name, object? Value)> SnapshotProperties(Control source)
    {
        var snapshot = new List<(string, object?)>();
        foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(source))
        {
            if (prop.IsReadOnly || prop.DesignTimeOnly) continue;
            if (prop.SerializationVisibility == DesignerSerializationVisibility.Hidden) continue;
            if (SkippedProperties.Contains(prop.Name)) continue;

            try { snapshot.Add((prop.Name, prop.GetValue(source))); }
            catch { /* a property that refuses to read is never worth failing a migration over */ }
        }
        return snapshot;
    }

    private static void ApplySnapshot(List<(string Name, object? Value)> snapshot, Control target)
    {
        PropertyDescriptorCollection targetProps = TypeDescriptor.GetProperties(target);
        foreach ((string name, object? value) in snapshot)
        {
            PropertyDescriptor? prop = targetProps[name];
            if (prop == null || prop.IsReadOnly) continue;

            try
            {
                if (!Equals(prop.GetValue(target), value))
                    prop.SetValue(target, value);
            }
            catch { /* skip what refuses to copy */ }
        }
    }

    // Identity and parentage are managed by the migration itself; collections would move
    // children twice.
    private static readonly HashSet<string> SkippedProperties = new(StringComparer.Ordinal)
    {
        "Name", "Site", "Parent", "Controls", "Container", "DataBindings",
        "ItemRules", "DesignerItems", "Bounds", "ClientSize", "WindowTarget",
    };
}
