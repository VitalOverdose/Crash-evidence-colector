using System.ComponentModel;
using System.ComponentModel.Design;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace ProfessorSnowsVideoDownloader.CustomControls;

/// <summary>
/// Designer for <see cref="VirtualizingAdaptiveRowPanel"/>: the stock parent-control designer
/// plus a smart tag carrying the v1 actions David actually uses while authoring rows.
/// </summary>
public class VirtualizingAdaptiveRowPanelDesigner : ParentControlDesigner
{
    // The v2 panel paints nothing of its own at design time, which made rows invisible on
    // the surface and hard to select (David). Same dashed outline plain Panels get.
    protected override void OnPaintAdornments(System.Windows.Forms.PaintEventArgs pe)
    {
        base.OnPaintAdornments(pe);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 110, 145, 190))
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
        };
        System.Drawing.Rectangle bounds = Control.ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        pe.Graphics.DrawRectangle(pen, bounds);
    }

    private DesignerActionListCollection? _actionLists;

    public override DesignerActionListCollection ActionLists
    {
        get
        {
            if (_actionLists == null)
            {
                _actionLists = new DesignerActionListCollection
                {
                    new VirtualizingAdaptiveRowPanelActionList(Component),
                };
                _actionLists.AddRange(base.ActionLists);
            }

            return _actionLists;
        }
    }
}

public class VirtualizingAdaptiveRowPanelActionList : DesignerActionList
{
    private int _gapValue = 6;
    private int _marginDelta = 4;
    private int _verticalGap = 2;
    private Font? _standardFont;

    public VirtualizingAdaptiveRowPanelActionList(IComponent component) : base(component)
    {
    }

    private VirtualizingAdaptiveRowPanel? Panel => Component as VirtualizingAdaptiveRowPanel;

    private IComponentChangeService? ChangeService =>
        (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

    private IDesignerHost? DesignerHost =>
        (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

    // Announced single-property write — every action below goes through this so each change
    // serializes and lands in the undo stack.
    private void SetProp(Control child, string propertyName, object value)
    {
        PropertyDescriptor? prop = TypeDescriptor.GetProperties(child)[propertyName];
        if (prop == null) return;

        object? old = prop.GetValue(child);
        if (Equals(old, value)) return;

        ChangeService?.OnComponentChanging(child, prop);
        prop.SetValue(child, value);
        ChangeService?.OnComponentChanged(child, prop, old, value);
    }

    // Target gap (N) for the one-shot Set Gap action. Design-time only — the layout reads
    // spacing from the child margins, so there is no persistent gap property to keep.
    public int GapValue
    {
        get => _gapValue;
        set => _gapValue = Math.Max(0, value);
    }

    /// <summary>
    /// One-shot: writes GapValue/2 into every child's left AND right margin, so the gap between
    /// any two adjacent children equals GapValue (the layout sums the two facing margins —
    /// same spacing model as v1). Announced per child, so it serializes and undoes.
    /// </summary>
    public void SetGap()
    {
        if (Panel is not { } panel) return;

        int half = _gapValue / 2;
        IComponentChangeService? svc = ChangeService;
        using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Set Gap");
        foreach (Control child in panel.Controls)
        {
            PropertyDescriptor? prop = TypeDescriptor.GetProperties(child)["Margin"];
            if (prop == null) continue;

            Padding old = child.Margin;
            Padding updated = new(half, old.Top, half, old.Bottom);
            if (old == updated) continue;

            svc?.OnComponentChanging(child, prop);
            prop.SetValue(child, updated);
            svc?.OnComponentChanged(child, prop, old, updated);
        }

        panel.PerformLayout();
        panel.Invalidate();
        tx?.Commit();
    }

    /// <summary>
    /// Pins the panel's BackColor to the colour actually painted behind it, via v1's zone-aware
    /// surface walk (understands RoundedPanelFaster/RoundedForm bars; refuses to pin a colour
    /// when the panel straddles a bar boundary, where no single colour is correct).
    /// </summary>
    public void InheritParentBackColor()
    {
        if (Panel is not { } panel || panel.Parent == null) return;

        Color newColor = ShellSurfaceCompat.Resolve(panel) ?? SystemColors.Control;
        Color oldColor = panel.BackColor;

        IComponentChangeService? svc = ChangeService;
        PropertyDescriptor? prop = TypeDescriptor.GetProperties(panel)["BackColor"];
        using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Inherit Parent BackColor");
        // NB: no early-out when the panel already matches — the CHILDREN may still be stale,
        // which is exactly what David hit on vArpStatus2.
        if (oldColor != newColor)
        {
            svc?.OnComponentChanging(panel, prop);
            if (prop != null) prop.SetValue(panel, newColor); else panel.BackColor = newColor;
            svc?.OnComponentChanged(panel, prop, oldColor, newColor);
        }

        // Children too: for the house controls BackColor is the BACKDROP outside the rounded
        // corners (BackgroundColor is the face), so a child left on the old colour shows it in
        // its corners. Faces are never touched — only the backdrop follows the surface.
        foreach (Control child in panel.Controls)
        {
            if (child.BackColor == newColor) continue;
            SetProp(child, "BackColor", newColor);
        }

        panel.Invalidate();
        tx?.Commit();
    }

    public int MarginDelta
    {
        get => _marginDelta;
        set => _marginDelta = value;
    }

    public int VerticalGap
    {
        get => _verticalGap;
        set => _verticalGap = Math.Max(0, value);
    }

    public Font StandardFont
    {
        get => _standardFont ?? Panel?.Font ?? Control.DefaultFont;
        set => _standardFont = value;
    }

    /// <summary>Signed nudge added to every child's left/right margin. Negative shrinks.</summary>
    public void AdjustChildMargins()
    {
        RunOnChildren("Adjust Child Margins", child =>
        {
            Padding m = child.Margin;
            SetProp(child, "Margin", new Padding(
                Math.Max(0, m.Left + _marginDelta), m.Top,
                Math.Max(0, m.Right + _marginDelta), m.Bottom));
        });
    }

    public void RemoveSpacing()
    {
        RunOnChildren("Remove Spacing", child =>
        {
            Padding m = child.Margin;
            SetProp(child, "Margin", new Padding(0, m.Top, 0, m.Bottom));
        });
    }

    public void FitChildrenHeightsToRow()
    {
        if (Panel is not { } panel) return;
        int target = Math.Max(18, panel.Height - panel.Padding.Vertical - _verticalGap * 2);
        RunOnChildren("Fit Children Heights to Row", child => SetProp(child, "Height", target));
    }

    /// <summary>
    /// Centers the sprung group. The engine already keeps spring WIDTHS equal (they share the
    /// free space), so the only thing that skews a centered group is unequal FIXED content
    /// outside the outermost springs. This measures both flanks and pads the lighter edge's
    /// spacer to match — creating a virtual spacer at that edge if there isn't one.
    /// </summary>
    public void EqualiseSprings()
    {
        if (Panel is not { } panel) return;

        var ordered = panel.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToList();
        var springFlags = ordered.Select(panel.IsSpringChild).ToList();
        int firstSpring = springFlags.IndexOf(true);
        int lastSpring = springFlags.LastIndexOf(true);
        if (firstSpring < 0 || firstSpring == lastSpring) return;   // needs two springs to center between

        static int Weigh(IEnumerable<Control> items)
            => items.Sum(c => c.Width + Math.Max(0, c.Margin.Left) + Math.Max(0, c.Margin.Right));
        int leftWeight = Weigh(ordered.Take(firstSpring));
        int rightWeight = Weigh(ordered.Skip(lastSpring + 1));
        if (leftWeight == rightWeight) return;

        bool padLeft = leftWeight < rightWeight;
        int needed = Math.Abs(leftWeight - rightWeight);
        Control? edge = padLeft ? ordered.FirstOrDefault() : ordered.LastOrDefault();

        using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Equalise Springs");
        if (edge is AdaptiveRowSpacer && !panel.IsSpringChild(edge))
        {
            SetProp(edge, "Width", edge.Width + needed);
        }
        else
        {
            var spacer = DesignerHost?.CreateComponent(typeof(VirtualAdaptiveRowSpacer)) as VirtualAdaptiveRowSpacer
                ?? new VirtualAdaptiveRowSpacer();
            spacer.Width = needed;
            spacer.TabIndex = padLeft
                ? Math.Max(0, ordered.First().TabIndex - 2)
                : ordered.Last().TabIndex + 2;
            panel.Controls.Add(spacer);
        }
        tx?.Commit();
        panel.PerformLayout();
        panel.Invalidate();
    }

    public void MakeSameWidthLargest() => ApplyUniform("Width", largest: true);
    public void MakeSameWidthSmallest() => ApplyUniform("Width", largest: false);
    public void MakeSameHeightLargest() => ApplyUniform("Height", largest: true);
    public void MakeSameHeightSmallest() => ApplyUniform("Height", largest: false);

    public void StandardizeFont()
    {
        Font font = StandardFont;
        RunOnChildren("Standardize Font", child => SetProp(child, "Font", font));
    }

    /// <summary>0,2,4… in current order. TabIndex is v2's ordering truth, so gaps left by
    /// deletions are worth compacting before they collide with a later add.</summary>
    public void ResequenceTabIndexes()
    {
        if (Panel is not { } panel) return;
        Control[] ordered = panel.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToArray();
        using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Resequence Tab Indexes");
        for (int i = 0; i < ordered.Length; i++)
        {
            SetProp(ordered[i], "TabIndex", i * 2);
        }
        tx?.Commit();
    }

    private void ApplyUniform(string propertyName, bool largest)
    {
        if (Panel is not { } panel) return;
        Control[] children = panel.Controls.Cast<Control>().ToArray();
        if (children.Length == 0) return;

        int target = largest
            ? children.Max(c => propertyName == "Width" ? c.Width : c.Height)
            : children.Min(c => propertyName == "Width" ? c.Width : c.Height);
        RunOnChildren($"Make Same {propertyName} ({(largest ? "Largest" : "Smallest")})",
            child => SetProp(child, propertyName, target));
    }

    private void RunOnChildren(string transactionName, Action<Control> apply)
    {
        if (Panel is not { } panel) return;
        using DesignerTransaction? tx = DesignerHost?.CreateTransaction(transactionName);
        foreach (Control child in panel.Controls)
        {
            apply(child);
        }
        panel.PerformLayout();
        panel.Invalidate();
        tx?.Commit();
    }

    /// <summary>
    /// Retypes every child that has a Virtual twin — IconButton to VirtualIconButton and so on —
    /// so ordinary controls can be dropped into the row and made virtual here, rather than only
    /// as a side effect of converting a v1 row from the vStack's smart tag.
    ///
    /// Partial by design: children with no twin (text boxes, combos, progress bars) stay hosted,
    /// which is a legitimate end state. Names are preserved, so code-behind handlers survive.
    /// </summary>
    public void VirtualizeChildren()
    {
        VirtualizingAdaptiveRowPanel? panel = Panel;
        IDesignerHost? host = DesignerHost;
        if (panel == null || host == null)
        {
            return;
        }

        (int convertible, int alreadyVirtual, int hosted) = RowVirtualizer.SurveyChildren(panel);
        if (convertible == 0)
        {
            ShowDesignerMessage(
                alreadyVirtual > 0 && hosted == 0
                    ? $"All {alreadyVirtual} children are already virtual."
                    : "Nothing here can be retyped.\n\n"
                      + $"Already virtual: {alreadyVirtual}\n"
                      + $"Hosted (no virtual twin): {hosted}");
            return;
        }

        // ONE transaction for the lot, so the whole conversion is a single undo step. Half a
        // converted row would be a miserable thing to unpick by hand.
        using DesignerTransaction transaction = host.CreateTransaction("Virtualize children");
        int converted;
        try
        {
            converted = RowVirtualizer.ConvertChildrenInPlace(panel, host, ChangeService);
        }
        catch
        {
            transaction.Cancel();
            throw;
        }

        transaction.Commit();

        string newline = Environment.NewLine;
        string message = $"Retyped {converted} child control{(converted == 1 ? "" : "s")}."
                       + $"{newline}Already virtual: {alreadyVirtual}";

        // Warned, not refused: this panel is ALREADY v2, so a splitter in it is already inert —
        // retyping its children takes nothing further away. Still worth saying, because it means
        // the divider the user can see is not going to drag.
        if (RowVirtualizer.HasDivider(panel, out string? divider))
        {
            message += $"{newline}{newline}⚠ This row has a {divider}, but v2 has no splitter or "
                     + "piston support — that divider will not drag.";
        }

        (List<string> expected, List<string> unexpected) = RowVirtualizer.ListHostedChildren(panel);

        // Unexpected first — that is the one worth acting on. A row keeping a display surface's
        // handle is not the row you thought you had.
        if (unexpected.Count > 0)
        {
            message += $"{newline}{newline}⚠ STILL HOSTED — these keep a window handle, so this row "
                     + $"is not fully virtual:{newline}  " + string.Join(newline + "  ", unexpected);
        }

        if (expected.Count > 0)
        {
            message += $"{newline}{newline}Hosted as expected (text entry, lists, progress — these "
                     + $"need their handles):{newline}  " + string.Join(newline + "  ", expected);
        }

        ShowDesignerMessage(message);
    }

    /// <summary>
    /// NO raw modals from designer actions — same law as VStackDesigner, learned the hard way.
    /// A native MessageBox opens on the DesignToolsServer's own thread, lands BEHIND VS
    /// unfocusable, and VS then waits forever on a modal nobody can click. IUIService is the one
    /// supported channel: it marshals the dialog through VS itself.
    /// </summary>
    private void ShowDesignerMessage(string message)
    {
        var ui = (System.Windows.Forms.Design.IUIService?)Component?.Site?.GetService(
            typeof(System.Windows.Forms.Design.IUIService));

        if (ui != null)
        {
            ui.ShowMessage(message, "Virtualize Children");
        }
        else
        {
            // No service, no dialog. The retyped children are visible on the surface anyway.
            System.Diagnostics.Debug.WriteLine($"[VirtualizeChildren] {message}");
        }
    }

    public override DesignerActionItemCollection GetSortedActionItems()
    {
        return new DesignerActionItemCollection
        {
            new DesignerActionHeaderItem("Virtualization"),
            new DesignerActionMethodItem(this, nameof(VirtualizeChildren), "Virtualize Children", "Virtualization", true),

            new DesignerActionHeaderItem("Spacing"),
            new DesignerActionPropertyItem(nameof(GapValue), "Gap (N)", "Spacing",
                "Target gap between children for Set Gap. Spacing lives in the child margins — there is no persistent gap property."),
            new DesignerActionMethodItem(this, nameof(SetGap), "Set Gap (one-shot)", "Spacing", true),
            new DesignerActionPropertyItem(nameof(MarginDelta), "Margin Delta N", "Spacing",
                "Signed amount added to each child's left/right margin. Negative shrinks."),
            new DesignerActionMethodItem(this, nameof(AdjustChildMargins), "Adjust Child Margins", "Spacing", true),
            new DesignerActionMethodItem(this, nameof(RemoveSpacing), "Remove Spacing", "Spacing", true),
            new DesignerActionHeaderItem("Sizing"),
            new DesignerActionPropertyItem(nameof(VerticalGap), "Vertical Gap (px)", "Sizing",
                "Pixel gap above and below children when fitting to row height."),
            new DesignerActionMethodItem(this, nameof(FitChildrenHeightsToRow), "Fit Children Heights to Row", "Sizing", true),
            new DesignerActionMethodItem(this, nameof(MakeSameWidthLargest), "Make Same Width (Largest)", "Sizing", true),
            new DesignerActionMethodItem(this, nameof(MakeSameWidthSmallest), "Make Same Width (Smallest)", "Sizing", true),
            new DesignerActionMethodItem(this, nameof(MakeSameHeightLargest), "Make Same Height (Largest)", "Sizing", true),
            new DesignerActionMethodItem(this, nameof(MakeSameHeightSmallest), "Make Same Height (Smallest)", "Sizing", true),
            new DesignerActionHeaderItem("Layout"),
            new DesignerActionPropertyItem(nameof(StandardFont), "Standard Font", "Layout",
                "Font applied to every child by 'Apply Font to All'."),
            new DesignerActionMethodItem(this, nameof(EqualiseSprings), "Equalise Springs (center group)", "Layout", true),
            new DesignerActionMethodItem(this, nameof(StandardizeFont), "Apply Font to All", "Layout", true),
            new DesignerActionMethodItem(this, nameof(ResequenceTabIndexes), "Resequence Tab Indexes (0,2,4...)", "Layout", true),
            new DesignerActionMethodItem(this, nameof(InheritParentBackColor), "Inherit Parent BackColor", "Layout", true),
        };
    }
}
