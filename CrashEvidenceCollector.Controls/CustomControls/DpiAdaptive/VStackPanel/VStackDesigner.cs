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
    // Shared designer for every VStack host. Adds the smart-tag actions; the action list works
    // against the IVStackHost interface so the same code serves vStackPanel / vStackRoundedPanel / VStackForm.
    public class VStackDesigner : ParentControlDesigner
    {
        private DesignerActionListCollection? _actionLists;

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    _actionLists.Add(new VStackActionList(Component));
                    _actionLists.AddRange(base.ActionLists);
                }

                return _actionLists;
            }
        }
    }

    public class VStackActionList : DesignerActionList
    {
        public VStackActionList(IComponent component) : base(component) { }

        private Control? HostControl => Component as Control;

        private VStackEngine? Engine => (Component as IVStackHost)?.VStackEngine;

        private IComponentChangeService? ChangeService =>
            (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

        private IDesignerHost? DesignerHost =>
            (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

        private int _verticalSpacing = 2;

        public int VerticalSpacing
        {
            get => _verticalSpacing;
            set => _verticalSpacing = Math.Max(0, value);
        }

        private int _verticalSpacingAdjustment;

        public int VerticalSpacingAdjustment
        {
            get => _verticalSpacingAdjustment;
            set => _verticalSpacingAdjustment = value; // signed: + grows the gaps, - shrinks them
        }

        // Commits the current visual order by reordering the RULES, then re-applying it to the
        // children as TabIndex 2, 4, 6, 8... (starting at 2 leaves 0/1 free to slot something in
        // above the first item).
        //
        // This deliberately does NOT just rewrite TabIndex. The rules list is the source of truth
        // for order — the engine's sync rewrites every TabIndex from it — so renumbering TabIndex
        // alone appeared to work and was then silently reverted the next time anything triggered a
        // sync (adding a control, a paste, a rule edit), which made the stack look like it
        // reordered itself at random.
        public void ResequenceTabIndexes()
        {
            if (HostControl is not { } host || Engine is not { } engine)
            {
                return;
            }

            using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Renumber Tab Indexes");
            engine.ReorderRulesToVisualOrder();
            host.PerformLayout();
            tx?.Commit();
        }

        // Normalize the vertical gaps so dragged-in controls with mismatched margins line up. N
        // is the total gap between items, split across adjacent margins (upper.Bottom gets the
        // higher half, lower.Top the lower half). Top control's Top = N; bottom control's Bottom
        // = N when a spring fills the stack to the edge, else the lower half. Outer margins
        // subtract the host padding so the real edge gap totals N. Left/Right untouched.
        public void ApplyVerticalSpacing()
        {
            if (HostControl is not { } host || Engine is not { } engine)
            {
                return;
            }

            Control[] controls = host.Controls
                .Cast<Control>()
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();
            if (controls.Length == 0)
            {
                return;
            }

            int n = Math.Max(0, _verticalSpacing);
            int half = n / 2;
            int higher = n - half;
            bool springsPresent = engine.GetSpringControls().Count > 0;

            IComponentChangeService? svc = ChangeService;
            using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Apply Vertical Spacing");
            for (int i = 0; i < controls.Length; i++)
            {
                Control child = controls[i];

                int top = i == 0
                    ? Math.Max(0, n - host.Padding.Top)
                    : half;
                int bottom = i == controls.Length - 1
                    ? Math.Max(0, (springsPresent ? n : half) - host.Padding.Bottom)
                    : higher;

                Padding current = child.Margin;
                SetMargin(child, new Padding(current.Left, top, current.Right, bottom), svc);
            }

            host.PerformLayout();
            tx?.Commit();
        }

        // Fine-tune: add a signed delta to the combined gap between every stacked control, split
        // across the two adjacent margins so each combined gap changes by exactly the delta.
        // Negative shrinks; each margin clamps at 0. Left/Right untouched.
        public void AdjustVerticalSpacing()
        {
            if (HostControl is not { } host)
            {
                return;
            }

            int delta = _verticalSpacingAdjustment;
            if (delta == 0)
            {
                return;
            }

            int topAdd = delta / 2;
            int bottomAdd = delta - topAdd;

            IComponentChangeService? svc = ChangeService;
            using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Adjust Vertical Spacing");
            foreach (Control child in host.Controls.Cast<Control>())
            {
                Padding m = child.Margin;
                int top = Math.Max(0, m.Top + topAdd);
                int bottom = Math.Max(0, m.Bottom + bottomAdd);
                SetMargin(child, new Padding(m.Left, top, m.Right, bottom), svc);
            }

            host.PerformLayout();
            tx?.Commit();
        }

        private static void SetTabIndex(Control child, int newIndex, IComponentChangeService? svc)
        {
            if (child.TabIndex == newIndex)
            {
                return;
            }

            PropertyDescriptor? prop = TypeDescriptor.GetProperties(child)["TabIndex"];
            int old = child.TabIndex;
            svc?.OnComponentChanging(child, prop);
            if (prop != null)
            {
                prop.SetValue(child, newIndex);
            }
            else
            {
                child.TabIndex = newIndex;
            }

            svc?.OnComponentChanged(child, prop, old, newIndex);
        }

        private static void SetMargin(Control child, Padding newMargin, IComponentChangeService? svc)
        {
            if (child.Margin == newMargin)
            {
                return;
            }

            PropertyDescriptor? prop = TypeDescriptor.GetProperties(child)["Margin"];
            Padding old = child.Margin;
            svc?.OnComponentChanging(child, prop);
            if (prop != null)
            {
                prop.SetValue(child, newMargin);
            }
            else
            {
                child.Margin = newMargin;
            }

            svc?.OnComponentChanged(child, prop, old, newMargin);
        }

        /// <summary>
        /// Converts every eligible v1 AdaptiveRowPanel row to a VirtualizingAdaptiveRowPanel.
        /// REBUILT after crashing VS (2026-08-15): the first version swapped the controls
        /// directly and never updated the rules, so the engine's type-enforcement destroyed
        /// each new panel and rebuilt a v1 while the loop marched on — migration and
        /// un-migration chasing each other until the designer hung.
        ///
        /// Now it does what you'd do by hand, automated (David's recipe): reindex first so
        /// every rule/child pairing is true, then flip each rule's ItemType and let
        /// OnRuleTypeChanged drive the guarded migrate path, one row fully finishing before
        /// the next starts. No transaction spans rows.
        /// </summary>
        public void VirtualizeEligibleRows()
        {
            if (HostControl is not { } host)
            {
                return;
            }

            var converted = new List<string>();
            var skipped = new List<string>();

            // Controls that keep their handle after conversion. Unexpected ones are the warning:
            // a row is supposed to end up with no handles in it at all.
            var hostedExpected = new List<string>();
            var hostedUnexpected = new List<string>();

            // RECURSIVE (David): nested vStacks are the norm — the bookmarks panel is a base
            // stack whose rows are themselves stacks with ARP rows inside. Every vStack host
            // under the clicked one gets the same treatment, each through its OWN engine's
            // guarded channel, one engine fully finished before the next.
            foreach (VStackEngine engine in CollectStackEngines(host))
            {
                VirtualizeEngineRows(engine, converted, skipped, hostedExpected, hostedUnexpected);
            }

            string newline = Environment.NewLine;
            string message = converted.Count > 0
                ? $"Converted {converted.Count} row(s):{newline}  " + string.Join(newline + "  ", converted)
                : "No rows converted.";
            if (skipped.Count > 0)
                message += $"{newline}{newline}Skipped {skipped.Count}:{newline}  " + string.Join(newline + "  ", skipped);

            // The warning that replaced the old refusal. Unexpected first — that is the one worth
            // acting on; a row keeping a ListView's handle is not the row you thought you had.
            if (hostedUnexpected.Count > 0)
            {
                message += $"{newline}{newline}⚠ STILL HOSTED — these keep a window handle, so the row "
                         + $"is not fully virtual:{newline}  " + string.Join(newline + "  ", hostedUnexpected);
            }

            if (hostedExpected.Count > 0)
            {
                message += $"{newline}{newline}Hosted as expected (text entry, lists, progress — these "
                         + $"need their handles):{newline}  " + string.Join(newline + "  ", hostedExpected);
            }

            ShowDesignerMessage(message);
        }

        // Depth-first, self included: children's engines convert before the parent touches
        // its own rows, so a nested stack is already settled when its hosting row is visited.
        private static List<VStackEngine> CollectStackEngines(Control root)
        {
            var engines = new List<VStackEngine>();

            void Walk(Control control)
            {
                foreach (Control child in control.Controls)
                {
                    Walk(child);
                }

                if (control is IVStackHost stackHost)
                {
                    engines.Add(stackHost.VStackEngine);
                }
            }

            Walk(root);
            return engines;
        }

        private static void VirtualizeEngineRows(
            VStackEngine engine,
            List<string> converted,
            List<string> skipped,
            List<string> hostedExpected,
            List<string> hostedUnexpected)
        {
            // 1) Clean index first (David's recipe): de-dupe tags, adopt ruleless children,
            //    prune orphan rules — migration starts from a true pairing.
            engine.ReindexRules();

            Control? host = engine.Host;
            if (host == null)
            {
                return;
            }

            // 2) Snapshot AFTER the reindex; each conversion mutates Controls.
            foreach (AdaptiveRowPanel row in host.Controls.OfType<AdaptiveRowPanel>().ToList())
            {
                if (!RowVirtualizer.CanVirtualize(row, out string? reason))
                {
                    skipped.Add($"{row.Name} — {reason}");
                    continue;
                }

                // REFUSED, not warned. Everything else the converter leaves behind is visible —
                // a hosted control still shows and still works. A splitter converts cleanly and
                // just stops dragging, which nobody would notice until they tried to drag it,
                // by which time the conversion is committed and the row rebuilt.
                if (RowVirtualizer.HasDivider(row, out string? divider))
                {
                    skipped.Add($"{row.Name} — has a {divider}; v2 has no splitter/piston support");
                    continue;
                }

                // Surveyed BEFORE conversion: afterwards the children have been reparented into
                // the new panel and the old row is gone.
                (List<string> expected, List<string> unexpected) = RowVirtualizer.ListHostedChildren(row);
                foreach (string entry in expected) hostedExpected.Add($"{row.Name}: {entry}");
                foreach (string entry in unexpected) hostedUnexpected.Add($"{row.Name}: {entry}");

                VStackRule? rule = engine.Rules.FirstOrDefault(r =>
                    row.Tag is string tag && string.Equals(r.Id, tag, StringComparison.Ordinal));
                if (rule == null)
                {
                    skipped.Add($"{row.Name} — no rule (reindex should have adopted it)");
                    continue;
                }

                string name = row.Name;
                // The one supported channel: the rule changes first, the engine replaces the
                // child under its own _isSyncingRules guard, synchronously.
                rule.ItemType = CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
                converted.Add(name);
            }
        }

        // NO raw modals from designer actions — both attempts hung VS. CustomMessageBox is an
        // app form with no app around it here; a native MessageBox opens on the
        // DesignToolsServer's own thread, lands BEHIND VS unfocusable, and VS waits forever on
        // a modal nobody can click (David heard the boing, then the freeze). IUIService is the
        // one supported channel — it marshals the dialog through VS itself.
        private void ShowDesignerMessage(string message)
        {
            var ui = (System.Windows.Forms.Design.IUIService?)Component?.Site?.GetService(
                typeof(System.Windows.Forms.Design.IUIService));
            if (ui != null)
            {
                ui.ShowMessage(message, "Virtualize Rows");
            }
            else
            {
                // No service, no dialog. The converted rows are visible on the surface anyway.
                System.Diagnostics.Debug.WriteLine($"[VirtualizeRows] {message}");
            }
        }

        public void ReindexRules()
        {
            if (Engine is not { } engine)
            {
                return;
            }

            using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Reindex VStack Rules");
            engine.ReindexRules();
            tx?.Commit();
        }

        /// <summary>
        /// Pins the stack's BackColor to the colour actually painted behind it, via the ARP's
        /// zone-aware surface walk (RoundedPanelFaster/RoundedForm bars understood; refuses to
        /// pin when the panel straddles a bar boundary, where no single colour is correct).
        /// </summary>
        public void InheritParentBackColor()
        {
            if (Component is not Control panel || panel.Parent == null)
            {
                return;
            }

            Color newColor = ShellSurfaceCompat.Resolve(panel) ?? SystemColors.Control;
            Color oldColor = panel.BackColor;
            if (oldColor == newColor)
            {
                return;
            }

            IComponentChangeService? svc = ChangeService;
            PropertyDescriptor? prop = TypeDescriptor.GetProperties(panel)["BackColor"];
            using DesignerTransaction? tx = DesignerHost?.CreateTransaction("Inherit Parent BackColor");
            svc?.OnComponentChanging(panel, prop);
            if (prop != null)
            {
                prop.SetValue(panel, newColor);
            }
            else
            {
                panel.BackColor = newColor;
            }

            svc?.OnComponentChanged(panel, prop, oldColor, newColor);
            tx?.Commit();
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items = new();
            items.Add(new DesignerActionHeaderItem("Layout"));
            items.Add(new DesignerActionMethodItem(this, nameof(ResequenceTabIndexes), "Renumber Tab Indexes (2,4,6...)", "Layout", true));
            items.Add(new DesignerActionMethodItem(this, nameof(InheritParentBackColor), "Inherit Parent BackColor", "Layout", true));
            items.Add(new DesignerActionHeaderItem("Spacing"));
            items.Add(new DesignerActionPropertyItem(nameof(VerticalSpacing), "V-Spacing", "Spacing", "Total vertical gap (px) between items. Edges and odd splits are handled automatically."));
            items.Add(new DesignerActionMethodItem(this, nameof(ApplyVerticalSpacing), "Apply", "Spacing", true));
            items.Add(new DesignerActionPropertyItem(nameof(VerticalSpacingAdjustment), "Adjust Vspace", "Spacing", "Signed delta added to every combined gap (negative shrinks). Fine-tuning on top of V-Spacing."));
            items.Add(new DesignerActionMethodItem(this, nameof(AdjustVerticalSpacing), "Adjust", "Spacing", true));
            items.Add(new DesignerActionHeaderItem("Migration"));
            items.Add(new DesignerActionMethodItem(this, nameof(ReindexRules), "Reindex Rules (add missing, prune orphans)", "Migration", true));
            items.Add(new DesignerActionMethodItem(this, nameof(VirtualizeEligibleRows), "Virtualize Eligible Rows (ARP -> Virtual)", "Migration", true));
            return items;
        }
    }
}
