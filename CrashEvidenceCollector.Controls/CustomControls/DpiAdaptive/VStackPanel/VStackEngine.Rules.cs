using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    // Design-time authoring half of the engine: the rule collection spawns/destroys real
    // designer controls, pushes margins down to them, and drives their order via TabIndex.
    // Layout (the other half) still reads the live controls — rules are a convenience on top,
    // never the layout source of truth. Join is by the stable ItemId, never Name/TabIndex.
    internal sealed partial class VStackEngine
    {
        private bool _isSyncingRules;
        private bool _rulesSyncPending;
        private readonly HashSet<string> _pendingRemovalIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingDeletedChildRuleIds = new(StringComparer.Ordinal);
        private bool _deletedChildRuleSyncPending;
        private IComponentChangeService? _designerChangeService;

        private IDesignerHost? DesignerHost => (IDesignerHost?)_host.Site?.GetService(typeof(IDesignerHost));

        private IComponentChangeService? ChangeService => (IComponentChangeService?)_host.Site?.GetService(typeof(IComponentChangeService));

        // ---- collection callbacks -------------------------------------------

        public void PrepareNewRule(VStackRule rule)
        {
            if (string.IsNullOrEmpty(rule.Id))
            {
                // Brand new rule (a loaded one always arrives with an Id, so this can't touch
                // deserialization). A new row nearly always wants a layout engine, so it gets
                // a row panel rather than a bare Panel — set BEFORE the name is generated
                // below so it's named after the type it will actually be. The VIRTUAL panel is
                // the default now (David, 2026-08-15): new rows should start handle-free, and
                // v1 remains one dropdown pick away for display-surface rows.
                //
                // Done here and not by changing VStackRule's default, because that default is
                // also the SERIALIZATION default: existing rules omit ItemType and would be
                // silently reinterpreted, rebuilding their children.
                if (IsInDesignMode() && rule.ItemType == VStackItemType.Panel)
                {
                    rule.ItemType = VStackItemType.VirtualizingAdaptiveRowPanel;
                }

                rule.Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(rule.ControlName))
            {
                rule.ControlName = GetNextName(rule.ItemType, rule);
            }

            ApplyTypeDefaultsIfUntouched(rule);
        }

        public void OnRulesChanged() => RequestSync();

        public void OnRuleSpringChanged(VStackRule rule)
        {
            if (!IsInDesignMode() || _isSyncingRules)
            {
                return;
            }

            UpdateSpringFromRules();
            Layout();
        }

        // A deliberate ItemType change rebuilds the control (keeps the same Id so the link holds).
        public void OnRuleTypeChanged(VStackRule rule)
        {
            if (!IsInDesignMode() || _isSyncingRules)
            {
                return;
            }

            if (FindChildById(rule.Id) is Control child && !RuleMatchesChild(rule.ItemType, child))
            {
                _isSyncingRules = true;
                try
                {
                    Control replacement = ReplaceDesignerChild(child, rule);
                    ApplyRuleToChild(rule, replacement);
                    ApplyRuleOrder();
                    UpdateSpringFromRules();
                }
                finally
                {
                    _isSyncingRules = false;
                }

                Layout();
                return;
            }

            RequestSync();
        }

        // Queue, don't destroy: a reorder is a remove+reinsert, so we only destroy ids that are
        // truly gone after the sync settles.
        public void QueueRuleChildRemoval(VStackRule rule)
        {
            if (!string.IsNullOrEmpty(rule.Id))
            {
                _pendingRemovalIds.Add(rule.Id);
            }

            RequestSync();
        }

        // ---- adoption (drop a control in → make a rule) ----------------------

        // Deferred so it runs AFTER the form's InitializeComponent finishes: on load, controls
        // and their rules deserialize in some order, and we must not adopt a control whose rule
        // simply hasn't loaded yet. By the time this runs, all rules exist, so already-ruled
        // controls are skipped and only genuinely-new (dropped) controls get adopted.
        private void RequestAdopt(Control child)
        {
            if (!IsInDesignMode() || _isSyncingRules)
            {
                return;
            }

            if (_host.IsHandleCreated && !_host.Disposing && !_host.IsDisposed)
            {
                _host.BeginInvoke((MethodInvoker)(() => AdoptExternalChild(child)));
            }
        }

        private void AdoptExternalChild(Control child)
        {
            if (!IsInDesignMode()
                || _isSyncingRules
                || child.IsDisposed
                || !ReferenceEquals(child.Parent, _host)
                || IsLayoutExcluded(child))
            {
                return;
            }

            // Already ruled (spawned by us, or adopted earlier)? Skip.
            if (child.Tag is string existing
                && !string.IsNullOrEmpty(existing)
                && _rules.Any(r => string.Equals(r.Id, existing, StringComparison.Ordinal)))
            {
                return;
            }

            VStackRule? existingRule = FindRuleByControlName(child.Name);
            if (existingRule != null && RuleMatchesChild(existingRule.ItemType, child))
            {
                SetProp(child, "Tag", existingRule.Id, child.Tag ?? string.Empty);
                return;
            }

            _isSyncingRules = true;
            try
            {
                // Land it at the bottom: highest TabIndex in the stack + 2.
                int maxIndex = _host.Controls
                    .Cast<Control>()
                    .Where(c => !ReferenceEquals(c, child))
                    .Select(c => c.TabIndex)
                    .DefaultIfEmpty(0)
                    .Max();
                SetIntProp(child, "TabIndex", maxIndex + 2);

                string id = Guid.NewGuid().ToString("N");
                SetProp(child, "Tag", id, child.Tag ?? string.Empty);

                VStackRule rule = new VStackRule
                {
                    Id = id,
                    ItemType = InferType(child),
                    ControlName = child.Name,
                    TopMargin = Math.Max(0, child.Margin.Top),
                    BottomMargin = Math.Max(0, child.Margin.Bottom),
                    ExpandedHeight = Math.Max(0, child.Height),
                    FontBaselineSizeInPoints = ShouldStepDirectChildFont(child) ? child.Font.SizeInPoints : 0f
                };

                PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
                ChangeService?.OnComponentChanging(_host, rulesProperty);
                _rules.Add(rule);
                ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
            }
            finally
            {
                _isSyncingRules = false;
            }

            Layout();
        }

        // A user deleted a control directly. Designer undo/redo can temporarily remove and
        // re-add controls, so never mutate rules immediately from OnControlRemoved.
        private void QueueRuleRemovalForDeletedChild(Control child)
        {
            if (child.Tag is not string tag || string.IsNullOrEmpty(tag))
            {
                return;
            }

            if (FindRuleById(tag) == null)
            {
                return;
            }

            _pendingDeletedChildRuleIds.Add(tag);
            RequestDeletedChildRuleRemoval();
        }

        private void RequestDeletedChildRuleRemoval()
        {
            if (_deletedChildRuleSyncPending || !IsInDesignMode())
            {
                return;
            }

            _deletedChildRuleSyncPending = true;
            if (_host.IsHandleCreated && !_host.Disposing && !_host.IsDisposed)
            {
                _host.BeginInvoke((MethodInvoker)(() =>
                {
                    if (_host.IsHandleCreated && !_host.Disposing && !_host.IsDisposed)
                    {
                        _host.BeginInvoke((MethodInvoker)RemoveRulesForStillDeletedChildren);
                    }
                    else
                    {
                        RemoveRulesForStillDeletedChildren();
                    }
                }));
            }
            else
            {
                RemoveRulesForStillDeletedChildren();
            }
        }

        private void RemoveRulesForStillDeletedChildren()
        {
            _deletedChildRuleSyncPending = false;
            if (!IsInDesignMode() || _isSyncingRules || _pendingDeletedChildRuleIds.Count == 0)
            {
                return;
            }

            List<VStackRule> rulesToRemove = new();
            foreach (string id in _pendingDeletedChildRuleIds.ToArray())
            {
                if (FindChildById(id) != null)
                {
                    _pendingDeletedChildRuleIds.Remove(id);
                    continue;
                }

                if (FindRuleById(id) is VStackRule rule)
                {
                    rulesToRemove.Add(rule);
                }

                _pendingDeletedChildRuleIds.Remove(id);
            }

            if (rulesToRemove.Count == 0)
            {
                return;
            }

            _isSyncingRules = true;
            try
            {
                PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
                ChangeService?.OnComponentChanging(_host, rulesProperty);
                foreach (VStackRule rule in rulesToRemove)
                {
                    _rules.Remove(rule);
                    _pendingRemovalIds.Remove(rule.Id);
                }

                ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
                UpdateSpringFromRules();
            }
            finally
            {
                _isSyncingRules = false;
            }
        }

        // Design-time repair for rule/child drift (the smart-tag "Reindex Rules" action): create a
        // rule for any child that has none, and prune rules whose control no longer exists. Blank
        // authoring placeholders (no Id and no ControlName) are left alone. Mirrors ARP's reindex.
        public void ReindexRules()
        {
            if (!IsInDesignMode())
            {
                return;
            }

            // 0) De-duplicate child tags. Copy/pasting a child in the designer CLONES its Tag GUID,
            //    so the copy resolves to the original's rule — adoption then thinks it is already
            //    ruled and skips it forever ("reindex won't accept my pasted control"). Re-mint a
            //    fresh tag for every extra holder so step 1 can adopt them properly.
            _isSyncingRules = true;
            try
            {
                var duplicateTagGroups = _host.Controls.Cast<Control>()
                    .Where(c => c.Tag is string t && !string.IsNullOrEmpty(t))
                    .GroupBy(c => (string)c.Tag!, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .ToArray();

                foreach (var group in duplicateTagGroups)
                {
                    VStackRule? owner = FindRuleById(group.Key);

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

                        SetProp(duplicate, "Tag", Guid.NewGuid().ToString("N"), duplicate.Tag ?? string.Empty);
                    }
                }
            }
            finally
            {
                _isSyncingRules = false;
            }

            // 1) Adopt children with no rule (idempotent; each call sets its own guard + notifies).
            foreach (Control child in _host.Controls.Cast<Control>().ToArray())
            {
                AdoptExternalChild(child);
            }

            // 2) Prune orphan rules — a rule whose control no longer exists.
            Control[] children = _host.Controls.Cast<Control>().ToArray();
            List<VStackRule> orphans = new();
            foreach (VStackRule rule in _rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Id) && string.IsNullOrWhiteSpace(rule.ControlName))
                {
                    continue;
                }

                if (FindChildForRule(rule) != null)
                {
                    continue;
                }

                // Safety net: FindChildForRule returns null when a rule and its child both carry
                // GUIDs that differ (copy/paste drift), so never delete a rule while a live child
                // still carries its name — losing a real control's rule scrambles the stack.
                string name = rule.ControlName?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(name)
                    && children.Any(c => string.Equals(c.Name, name, StringComparison.Ordinal)))
                {
                    continue;
                }

                orphans.Add(rule);
            }

            if (orphans.Count > 0)
            {
                _isSyncingRules = true;
                try
                {
                    PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
                    ChangeService?.OnComponentChanging(_host, rulesProperty);
                    foreach (VStackRule rule in orphans)
                    {
                        _rules.Remove(rule);
                        _pendingRemovalIds.Remove(rule.Id);
                    }

                    ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
                }
                finally
                {
                    _isSyncingRules = false;
                }
            }

            UpdateSpringFromRules();
            _host.PerformLayout();
        }

        private static VStackItemType InferType(Control child) => child switch
        {
            vStackRoundedPanel => VStackItemType.vStackRoundedPanel,
            vStackPanel => VStackItemType.vStackPanel,
            VirtualizingAdaptiveRowPanel => VStackItemType.VirtualizingAdaptiveRowPanel,
            AdaptiveRowPanel => VStackItemType.AdaptiveRowPanel,
            vStackSpacer => VStackItemType.vStackSpacer,
            _ => VStackItemType.Panel
        };

        // ---- sync ------------------------------------------------------------

        private void RequestSync()
        {
            if (!IsInDesignMode() || _isSyncingRules || _rulesSyncPending)
            {
                return;
            }

            EnsureDesignerChangeServiceHooked();
            _rulesSyncPending = true;
            if (_host.IsHandleCreated && !_host.Disposing && !_host.IsDisposed)
            {
                _host.BeginInvoke((MethodInvoker)(() =>
                {
                    if (_host.IsHandleCreated && !_host.Disposing && !_host.IsDisposed)
                    {
                        _host.BeginInvoke((MethodInvoker)SyncChildrenFromRules);
                    }
                    else
                    {
                        SyncChildrenFromRules();
                    }
                }));
            }
            else
            {
                SyncChildrenFromRules();
            }
        }

        private void SyncChildrenFromRules()
        {
            _rulesSyncPending = false;
            if (!IsInDesignMode() || _isSyncingRules)
            {
                return;
            }

            _isSyncingRules = true;
            try
            {
                foreach (VStackRule rule in _rules)
                {
                    if (string.IsNullOrEmpty(rule.Id))
                    {
                        continue;
                    }

                    Control? existingChild = FindChildForRule(rule);
                    bool createdChild = existingChild == null;
                    Control child = existingChild ?? CreateDesignerChild(rule);
                    if (createdChild
                        && !string.IsNullOrWhiteSpace(child.Name)
                        && !string.Equals(rule.ControlName, child.Name, StringComparison.Ordinal))
                    {
                        rule.ControlName = child.Name;
                    }

                    ApplyRuleToChild(rule, child);
                    CaptureRuleFontBaselineFromChild(child);
                }

                ApplyRuleOrder();
                RemovePendingRuleChildren();
                UpdateSpringFromRules();
                Layout();
            }
            finally
            {
                _isSyncingRules = false;
            }
        }

        /// <summary>
        /// Reorders the RULES to match the children's current visual order, then re-applies that
        /// order to the controls.
        ///
        /// The rules list — not TabIndex — is the source of truth for order: ApplyRuleOrder() writes
        /// every child's TabIndex from it on each sync. So renumbering TabIndex alone always looked
        /// like it worked and was then silently reverted the next time anything triggered a sync
        /// (adding a control, adopting one, changing a rule). This is what actually reorders a stack.
        ///
        /// Reordering is done by ASSIGNING through the indexer, never Remove/Insert:
        /// VStackRuleCollection.RemoveItem queues the rule's CHILD for deletion, so removing a rule
        /// to move it would destroy the control.
        /// </summary>
        public void ReorderRulesToVisualOrder()
        {
            if (!IsInDesignMode() || _rules.Count == 0)
            {
                return;
            }

            Control[] visualOrder = _host.Controls
                .Cast<Control>()
                .Where(c => !IsLayoutExcluded(c))
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            List<VStackRule> ordered = new(_rules.Count);
            foreach (Control child in visualOrder)
            {
                VStackRule? rule = FindRuleForChild(child);
                if (rule != null && !ordered.Contains(rule))
                {
                    ordered.Add(rule);
                }
            }

            // Rules with no live child (blank placeholders, hidden rows) keep their relative order
            // at the end rather than being dropped.
            foreach (VStackRule rule in _rules)
            {
                if (!ordered.Contains(rule))
                {
                    ordered.Add(rule);
                }
            }

            if (ordered.Count != _rules.Count)
            {
                return;     // safety: never reorder if we somehow lost or gained a rule
            }

            bool changed = false;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (!ReferenceEquals(_rules[i], ordered[i]))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            _isSyncingRules = true;
            try
            {
                PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
                ChangeService?.OnComponentChanging(_host, rulesProperty);

                for (int i = 0; i < ordered.Count; i++)
                {
                    if (!ReferenceEquals(_rules[i], ordered[i]))
                    {
                        _rules[i] = ordered[i];     // SetItem — safe; RemoveItem would delete the child
                    }
                }

                ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
                ApplyRuleOrder();
            }
            finally
            {
                _isSyncingRules = false;
            }

            _host.PerformLayout();
        }

        // Order the controls by rule order via TabIndex (2,4,6,... leaving gaps to nudge between).
        private void ApplyRuleOrder()
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                if (FindChildForRule(_rules[i]) is Control child)
                {
                    SetIntProp(child, "TabIndex", (i + 1) * 2);
                }
            }
        }

        /// <summary>
        /// Panel -> rules. Editing (or CLEARING) the Spring property left every rule's
        /// SpecialLayout untouched, so the two truths drifted — the same disease as the ARP v2
        /// spring-eater. Re-derives every rule's spring flag from the panel's spring names.
        /// Convergent, not circular: the rules->panel direction recomputes the same string.
        /// </summary>
        internal void SyncRuleSpringFlagsFromPanel()
        {
            if (_rules.Count == 0) return;

            bool changed = false;
            foreach (VStackRule rule in _rules)
            {
                Control? child = FindChildForRule(rule);
                string name = child?.Name is { Length: > 0 } childName ? childName : rule.ControlName.Trim();
                bool shouldSpring = name.Length > 0 && _springControlNames.Contains(name);
                if (rule.IsSpring != shouldSpring)
                {
                    rule.IsSpring = shouldSpring;
                    changed = true;
                }
            }

            if (changed && IsInDesignMode())
            {
                PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
                ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
            }
        }

        private void UpdateSpringFromRules()
        {
            List<string> springNames = new();
            foreach (VStackRule rule in _rules)
            {
                if (!rule.IsSpring || FindChildForRule(rule) is not Control child)
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(child.Name) ? rule.ControlName.Trim() : child.Name;
                if (name.Length > 0)
                {
                    springNames.Add(name);
                }
            }

            string? newSpring = springNames.Count == 0
                ? null
                : string.Join(",", springNames);

            if (string.Equals(Spring, newSpring, StringComparison.Ordinal))
            {
                return;
            }

            string? oldSpring = Spring;
            PropertyDescriptor? springProperty = TypeDescriptor.GetProperties(_host)["Spring"];
            ChangeService?.OnComponentChanging(_host, springProperty);
            if (springProperty != null)
            {
                springProperty.SetValue(_host, newSpring);
            }
            else
            {
                Spring = newSpring;
            }

            ChangeService?.OnComponentChanged(_host, springProperty, oldSpring, newSpring);
        }

        private void RemovePendingRuleChildren()
        {
            if (_pendingRemovalIds.Count == 0)
            {
                return;
            }

            HashSet<string> activeIds = _rules
                .Select(r => r.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);

            foreach (string id in _pendingRemovalIds.ToArray())
            {
                if (activeIds.Contains(id))
                {
                    continue; // still in use — it was a move, not a delete
                }

                if (FindChildById(id) is Control child)
                {
                    DestroyDesignerChild(child);
                }
            }

            _pendingRemovalIds.Clear();
        }

        // ---- designer child create / destroy ---------------------------------

        private Control CreateDesignerChild(VStackRule rule)
        {
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(_host)["Controls"];
            Type childType = GetChildClrType(rule.ItemType);
            string requestedName = string.IsNullOrWhiteSpace(rule.ControlName) ? GetNextName(rule.ItemType, rule) : rule.ControlName.Trim();

            Control? child = null;
            IDesignerHost? host = DesignerHost;
            if (host != null)
            {
                try { child = host.CreateComponent(childType, requestedName) as Control; }
                catch
                {
                    try { child = host.CreateComponent(childType) as Control; }
                    catch { child = null; }
                }
            }

            if (child == null)
            {
                child = (Control)Activator.CreateInstance(childType)!;
                child.Name = requestedName;
            }

            ConfigureNewChild(child);
            child.Height = rule.GetTargetHeight();
            // The join key lives in the control's Tag (a string id) — universal, serializes,
            // and survives rename/reorder. Set before adding so it's matchable immediately.
            child.Tag = rule.Id;

            ChangeService?.OnComponentChanging(_host, controlsProperty);
            _host.Controls.Add(child);
            ChangeService?.OnComponentChanged(_host, controlsProperty, null, child);
            return child;
        }

        private Control ReplaceDesignerChild(Control oldChild, VStackRule rule)
        {
            // v1 ARP -> virtual panel is a MIGRATION, not a replacement: destroying the row
            // would take every child with it (that is why conversions used to be hand-edits in
            // the .Designer.cs). RowVirtualizer moves the children across, retyping the ones
            // with Virtual twins and keeping their names so code-behind wiring survives.
            if (rule.ItemType == VStackItemType.VirtualizingAdaptiveRowPanel
                && oldChild is AdaptiveRowPanel v1Row
                && DesignerHost is { } migrationHost
                && RowVirtualizer.CanVirtualize(v1Row, out _))
            {
                VirtualizingAdaptiveRowPanel? migrated =
                    RowVirtualizer.Convert(v1Row, migrationHost, ChangeService);
                if (migrated != null)
                {
                    SetRuleControlName(rule, migrated.Name);
                    migrated.Tag = rule.Id;
                    return migrated;
                }
            }

            DestroyDesignerChild(oldChild);
            SetRuleControlName(rule, GetNextName(rule.ItemType, rule));
            ApplyTypeDefaultsIfUntouched(rule);
            return CreateDesignerChild(rule);
        }

        private void DestroyDesignerChild(Control child)
        {
            PropertyDescriptor? controlsProperty = TypeDescriptor.GetProperties(_host)["Controls"];
            ChangeService?.OnComponentChanging(_host, controlsProperty);
            _host.Controls.Remove(child);
            if (DesignerHost is { } host)
            {
                host.DestroyComponent(child);
            }
            else
            {
                child.Dispose();
            }

            ChangeService?.OnComponentChanged(_host, controlsProperty, child, null);
        }

        private static Type GetChildClrType(VStackItemType type) => type switch
        {
            VStackItemType.vStackPanel => typeof(vStackPanel),
            VStackItemType.vStackRoundedPanel => typeof(vStackRoundedPanel),
            VStackItemType.AdaptiveRowPanel => typeof(AdaptiveRowPanel),
            VStackItemType.VirtualizingAdaptiveRowPanel => typeof(VirtualizingAdaptiveRowPanel),
            VStackItemType.vStackSpacer => typeof(vStackSpacer),
            VStackItemType.FlatSeparator => typeof(FlatSeparator),
            _ => typeof(Panel)
        };

        private static bool RuleMatchesChild(VStackItemType type, Control child) => type switch
        {
            VStackItemType.vStackPanel => child is vStackPanel,
            VStackItemType.vStackRoundedPanel => child is vStackRoundedPanel,
            VStackItemType.AdaptiveRowPanel => child is AdaptiveRowPanel,
            VStackItemType.VirtualizingAdaptiveRowPanel => child is VirtualizingAdaptiveRowPanel,
            VStackItemType.vStackSpacer => child is vStackSpacer,
            VStackItemType.FlatSeparator => child is FlatSeparator,
            _ => child.GetType() == typeof(Panel)
        };

        private void ConfigureNewChild(Control child)
        {
            if (child.GetType() == typeof(Panel))
            {
                ((Panel)child).BorderStyle = BorderStyle.FixedSingle;
                child.BackColor = Color.FromArgb(245, 246, 248);
            }
            else if (child is vStackPanel nested)
            {
                nested.BackColor = Color.FromArgb(235, 240, 246);
            }
            else if (child is vStackRoundedPanel roundedNested)
            {
                roundedNested.CornerRadius = _host is vStackRoundedPanel ? 10 : 15;
            }
            else if (child is vStackSpacer spacer)
            {
                spacer.BackColor = Color.Transparent;
            }
            // AdaptiveRowPanel paints its own design-time chrome — leave it alone.

            if (_host is vStackRoundedPanel)
            {
                Padding margin = child.Margin;
                child.Margin = new Padding(0, margin.Top, 0, margin.Bottom);
            }
        }

        // Push the rule's margins onto the control (engine spacing reads control margins).
        private void ApplyRuleToChild(VStackRule rule, Control child)
        {
            string requestedName = rule.ControlName.Trim();
            if (IsValidComponentName(requestedName)
                && !string.Equals(child.Name, requestedName, StringComparison.Ordinal)
                && IsDesignerComponentNameAvailable(requestedName, child))
            {
                RenameDesignerChild(child, requestedName);
            }

            Padding current = child.Margin;
            Padding target = new Padding(current.Left, Math.Max(0, rule.TopMargin), current.Right, Math.Max(0, rule.BottomMargin));
            if (current != target)
            {
                SetProp(child, "Margin", target, current);
            }

            int targetHeight = rule.GetTargetHeight();
            if (child.Height != targetHeight)
            {
                SetIntProp(child, "Height", targetHeight);
            }
        }

        private void CaptureRuleLayoutFromChild(Control child)
        {
            if (!IsInDesignMode() || _isSyncingRules)
            {
                return;
            }

            VStackRule? rule = child.Tag is string id && !string.IsNullOrEmpty(id)
                ? _rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal))
                : FindRuleByControlName(child.Name);
            if (rule == null)
            {
                return;
            }

            int currentRuleHeight = rule.IsCollapsed ? rule.CollapsedHeight : rule.ExpandedHeight;
            int topMargin = Math.Max(0, child.Margin.Top);
            int bottomMargin = Math.Max(0, child.Margin.Bottom);
            if (currentRuleHeight == child.Height
                && rule.TopMargin == topMargin
                && rule.BottomMargin == bottomMargin)
            {
                return;
            }

            PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
            ChangeService?.OnComponentChanging(_host, rulesProperty);
            _isSyncingRules = true;
            try
            {
                // A collapsed row dragged back OPEN in the designer used to capture the new
                // height into CollapsedHeight and stay IsCollapsed — the rule then said
                // "collapsed" while the surface showed it expanded, and the next layout
                // re-folded it (David hit this on arpScanning). Treat a meaningful grow past
                // the collapsed height as the user re-expanding the row.
                if (rule.IsCollapsed && IsInDesignMode()
                    && child.Height > Math.Max(rule.CollapsedHeight, 0) + 2)
                {
                    rule.IsCollapsed = false;
                    rule.CaptureExpandedHeight(child.Height);
                }
                else if (rule.IsCollapsed)
                {
                    rule.CaptureCollapsedHeight(child.Height);
                }
                else
                {
                    rule.CaptureExpandedHeight(child.Height);
                }

                rule.TopMargin = topMargin;
                rule.BottomMargin = bottomMargin;
                if (ShouldStepDirectChildFont(child))
                {
                    rule.CaptureFontBaseline(child.Font.SizeInPoints);
                }
            }
            finally
            {
                _isSyncingRules = false;
            }

            ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
        }

        // ---- helpers ---------------------------------------------------------

        // Match a rule to its control by the id stashed in the control's Tag.
        private Control? FindChildById(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (Control c in _host.Controls)
            {
                if (c.Tag is string tag && string.Equals(tag, id, StringComparison.Ordinal))
                {
                    return c;
                }
            }

            return null;
        }

        private Control? FindChildForRule(VStackRule rule)
        {
            Control? byId = FindChildById(rule.Id);
            if (byId != null)
            {
                return byId;
            }

            string requestedName = rule.ControlName.Trim();
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                return null;
            }

            foreach (Control child in _host.Controls)
            {
                if (!string.Equals(child.Name, requestedName, StringComparison.Ordinal)
                    || !RuleMatchesChild(rule.ItemType, child))
                {
                    continue;
                }

                if (child.Tag is not string tag || string.IsNullOrEmpty(tag))
                {
                    SetProp(child, "Tag", rule.Id, child.Tag ?? string.Empty);
                    return child;
                }

                if (string.Equals(tag, rule.Id, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private VStackRule? FindRuleById(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return _rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        private void EnsureDesignerChangeServiceHooked()
        {
            if (!IsInDesignMode())
            {
                return;
            }

            IComponentChangeService? service = ChangeService;
            if (ReferenceEquals(service, _designerChangeService))
            {
                return;
            }

            UnhookDesignerChangeService();
            _designerChangeService = service;
            if (_designerChangeService != null)
            {
                _designerChangeService.ComponentChanged += DesignerChangeService_ComponentChanged;
            }
        }

        private void UnhookDesignerChangeService()
        {
            if (_designerChangeService == null)
            {
                return;
            }

            _designerChangeService.ComponentChanged -= DesignerChangeService_ComponentChanged;
            _designerChangeService = null;
        }

        private void DesignerChangeService_ComponentChanged(object? sender, ComponentChangedEventArgs e)
        {
            if (_isSyncingRules || !IsInDesignMode())
            {
                return;
            }

            if (e.Member != null && !string.Equals(e.Member.Name, nameof(Control.Name), StringComparison.Ordinal))
            {
                if (string.Equals(e.Member.Name, nameof(Control.Font), StringComparison.Ordinal)
                    && e.Component is Control fontChild
                    && ReferenceEquals(fontChild.Parent, _host))
                {
                    CaptureRuleFontBaselineFromChild(fontChild);
                }

                return;
            }

            if (e.Component is not Control child || !ReferenceEquals(child.Parent, _host))
            {
                return;
            }

            if (child.Tag is not string id || string.IsNullOrEmpty(id))
            {
                return;
            }

            VStackRule? rule = FindRuleById(id);
            string childName = child.Name.Trim();
            if (rule == null
                || string.IsNullOrEmpty(childName)
                || string.Equals(rule.ControlName, childName, StringComparison.Ordinal))
            {
                return;
            }

            _isSyncingRules = true;
            try
            {
                SetRuleControlName(rule, childName);
            }
            finally
            {
                _isSyncingRules = false;
            }

            // Spring references rows by name, so a renamed spring row must be rewritten there too.
            if (rule.IsSpring)
            {
                UpdateSpringFromRules();
                Layout();
            }
        }

        private float? GetRuleFontBaselineSize(Control child)
        {
            VStackRule? rule = FindRuleForChild(child);
            if (rule == null || rule.FontBaselineSizeInPoints <= 0.05f)
            {
                return null;
            }

            return rule.FontBaselineSizeInPoints;
        }

        private void CaptureRuleFontBaselineFromChild(Control child)
        {
            if (!IsInDesignMode()
                || !ShouldStepDirectChildFont(child))
            {
                return;
            }

            VStackRule? rule = FindRuleForChild(child);
            if (rule == null)
            {
                return;
            }

            float sizeInPoints = child.Font.SizeInPoints;
            if (Math.Abs(rule.FontBaselineSizeInPoints - sizeInPoints) <= 0.05f)
            {
                return;
            }

            PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
            ChangeService?.OnComponentChanging(_host, rulesProperty);
            bool wasSyncingRules = _isSyncingRules;
            _isSyncingRules = true;
            try
            {
                rule.CaptureFontBaseline(sizeInPoints);
            }
            finally
            {
                _isSyncingRules = wasSyncingRules;
            }

            ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
        }

        private VStackRule? FindRuleForChild(Control child)
        {
            if (child.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                VStackRule? byId = FindRuleById(tag);
                if (byId != null)
                {
                    return byId;
                }
            }

            return FindRuleByControlName(child.Name);
        }

        private void SetRuleControlName(VStackRule rule, string controlName)
        {
            string newName = controlName.Trim();
            if (string.Equals(rule.ControlName, newName, StringComparison.Ordinal))
            {
                return;
            }

            PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
            ChangeService?.OnComponentChanging(_host, rulesProperty);
            rule.ControlName = newName;
            ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
        }

        private static void ApplyTypeDefaultsIfUntouched(VStackRule rule)
        {
            if (rule.HasCustomCollapsedHeight
                || rule.ExpandedHeight != 80
                || rule.CollapsedHeight != 80)
            {
                return;
            }

            if (rule.ItemType is VStackItemType.AdaptiveRowPanel
                or VStackItemType.VirtualizingAdaptiveRowPanel
                or VStackItemType.vStackSpacer)
            {
                rule.ExpandedHeight = 36;
                rule.CollapsedHeight = 36;
            }
        }

        private string GetNextName(VStackItemType type, VStackRule? currentRule = null)
        {
            string prefix = type switch
            {
                VStackItemType.vStackPanel => "vStackPanel",
                VStackItemType.vStackRoundedPanel => "vStackRoundedPanel",
                VStackItemType.AdaptiveRowPanel => "adaptiveRowPanel",
                VStackItemType.VirtualizingAdaptiveRowPanel => "virtualRowPanel",
                VStackItemType.vStackSpacer => "vStackSpacer",
                _ => "panel"
            };

            HashSet<string> used = GetDesignerComponentNames();
            foreach (Control c in _host.Controls)
            {
                if (!string.IsNullOrWhiteSpace(c.Name))
                {
                    used.Add(c.Name);
                }
            }

            foreach (VStackRule r in _rules)
            {
                if (ReferenceEquals(r, currentRule))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(r.ControlName))
                {
                    used.Add(r.ControlName.Trim());
                }
            }

            for (int i = 1; i < 10000; i++)
            {
                string candidate = $"{prefix}{i}";
                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{prefix}{_host.Controls.Count + _rules.Count + 1}";
        }

        private HashSet<string> GetDesignerComponentNames()
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            IDesignerHost? host = DesignerHost;
            if (host?.Container == null)
            {
                return names;
            }

            foreach (IComponent component in host.Container.Components)
            {
                string? componentName = component.Site?.Name;
                if (!string.IsNullOrWhiteSpace(componentName))
                {
                    names.Add(componentName);
                }
            }

            if (host.RootComponent is Control rootControl)
            {
                AddControlTreeNames(rootControl, names);
            }

            return names;
        }

        private bool IsDesignerComponentNameAvailable(string componentName, IComponent? allowedComponent = null)
        {
            IDesignerHost? host = DesignerHost;
            if (host?.Container == null)
            {
                return true;
            }

            if (allowedComponent == null)
            {
                INameCreationService? nameService = host.GetService(typeof(INameCreationService)) as INameCreationService;
                if (nameService != null)
                {
                    return nameService.IsValidName(componentName);
                }
            }

            foreach (IComponent component in host.Container.Components)
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

            if (host.RootComponent is Control rootControl
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

        private void RenameDesignerChild(Control child, string newControlName)
        {
            PropertyDescriptor? nameProperty = TypeDescriptor.GetProperties(child)[nameof(Control.Name)];
            string oldName = child.Name;

            ChangeService?.OnComponentChanging(child, nameProperty);
            if (nameProperty != null)
            {
                nameProperty.SetValue(child, newControlName);
            }
            else
            {
                child.Name = newControlName;
            }

            ChangeService?.OnComponentChanged(child, nameProperty, oldName, newControlName);
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

        internal VStackRow Row(string controlName) => new(this, controlName);

        internal bool RowExists(string controlName) => FindRuleByControlName(controlName) != null;

        internal Control? GetRowControl(string controlName)
        {
            VStackRule? rule = FindRuleByControlName(controlName);
            return rule == null ? null : FindChildById(rule.Id);
        }

        internal bool GetRowIsCollapsed(string controlName) =>
            FindRuleByControlName(controlName)?.IsCollapsed ?? false;

        internal bool GetRowIsSpring(string controlName) =>
            FindRuleByControlName(controlName)?.IsSpring ?? false;

        internal bool GetRowIsPiston(string controlName) =>
            FindRuleByControlName(controlName)?.IsPiston ?? false;

        internal int GetRowExpandedHeight(string controlName) =>
            FindRuleByControlName(controlName)?.ExpandedHeight ?? 0;

        internal int GetRowCollapsedHeight(string controlName) =>
            FindRuleByControlName(controlName)?.CollapsedHeight ?? 0;

        internal bool SetRowCollapsed(string controlName, bool isCollapsed)
        {
            return MutateRow(controlName, rule => rule.IsCollapsed = isCollapsed, applyChild: true, updateSpring: false);
        }

        internal bool ToggleRowCollapsed(string controlName)
        {
            VStackRule? rule = FindRuleByControlName(controlName);
            return rule != null && SetRowCollapsed(controlName, !rule.IsCollapsed);
        }

        internal bool SetRowSpring(string controlName, bool isSpring)
        {
            return MutateRow(controlName, rule => rule.IsSpring = isSpring, applyChild: false, updateSpring: true);
        }

        // Piston mirror of SetRowSpring. SpecialLayout is a single tri-state (None/Spring/Piston),
        // so piston XOR spring holds by construction — setting one clears the other and there is
        // no intermediate "both" state to guard against. updateSpring: true because the host's
        // spring list is derived from the rules and a piston change alters who absorbs.
        internal bool SetRowPiston(string controlName, bool isPiston)
        {
            return MutateRow(controlName, rule => rule.IsPiston = isPiston, applyChild: false, updateSpring: true);
        }

        internal bool SetRowExpandedHeight(string controlName, int height)
        {
            return MutateRow(controlName, rule => rule.ExpandedHeight = Math.Max(0, height), applyChild: true, updateSpring: false);
        }

        internal bool SetRowCollapsedHeight(string controlName, int height)
        {
            return MutateRow(controlName, rule => rule.CollapsedHeight = Math.Max(0, height), applyChild: true, updateSpring: false);
        }

        // A role swap is several mutations that are only valid TOGETHER — turn one piston off,
        // another on, reveal a splitter, resize two rows. Applied one at a time, each ends in a
        // full Layout() and the engine transiently sees arrangements that should never exist
        // (two pistons, or none, or no spring at all), which is where the flicker and the
        // "flaky" behaviour come from. Inside a batch, mutations still write their rules and
        // notify the designer, but Layout() and the spring resync are deferred to the end, so
        // the whole swap lands as ONE atomic relayout.
        //
        //   using (panel.BeginRowBatch())
        //   {
        //       panel.Row("vStackQuick").IsPiston = false;
        //       panel.Row("tbxStatus").IsPiston = true;
        //   }
        internal IDisposable BeginRowBatch() => new RowBatch(this);

        private int _rowBatchDepth;
        private bool _rowBatchSpringDirty;
        private bool _rowBatchLayoutDirty;

        private void EndRowBatch()
        {
            if (_rowBatchDepth == 0)
            {
                return;
            }

            _rowBatchDepth--;
            if (_rowBatchDepth > 0)
            {
                return;
            }

            bool resyncSpring = _rowBatchSpringDirty;
            bool relayout = _rowBatchLayoutDirty;
            _rowBatchSpringDirty = false;
            _rowBatchLayoutDirty = false;

            if (resyncSpring)
            {
                _isSyncingRules = true;
                try
                {
                    UpdateSpringFromRules();
                }
                finally
                {
                    _isSyncingRules = false;
                }
            }

            if (relayout)
            {
                Layout();
            }

            WarnIfNoSpring();
        }

        // A batch that resumes into a stack with NO spring lays out with nobody to absorb the
        // slack — every row keeps its authored height and the leftover space just sits there.
        // The geometry looks "wrong" in a way that points nowhere near the swap that caused it,
        // so say it out loud instead. Design time only: at runtime a stack briefly without a
        // spring is the caller's business, and this must never cost a release build anything.
        [System.Diagnostics.Conditional("DEBUG")]
        private void WarnIfNoSpring()
        {
            if (_rules.Count == 0 || _rules.Any(rule => rule.IsSpring))
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[VStack] '{_host?.Name}' has no spring after a row batch — " +
                "nothing will absorb the slack. Did a piston swap forget to reassign one?");
        }

        // Nesting-safe: an inner batch just increments the depth, and only the outermost
        // dispose flushes. Disposing twice is a no-op rather than an unbalanced decrement.
        private sealed class RowBatch : IDisposable
        {
            private readonly VStackEngine _engine;
            private bool _disposed;

            internal RowBatch(VStackEngine engine)
            {
                _engine = engine;
                engine._rowBatchDepth++;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _engine.EndRowBatch();
            }
        }

        private bool MutateRow(string controlName, Action<VStackRule> mutation, bool applyChild, bool updateSpring)
        {
            VStackRule? rule = FindRuleByControlName(controlName);
            if (rule == null)
            {
                return false;
            }

            PropertyDescriptor? rulesProperty = TypeDescriptor.GetProperties(_host)["Rules"];
            ChangeService?.OnComponentChanging(_host, rulesProperty);
            _isSyncingRules = true;
            try
            {
                mutation(rule);
                if (applyChild && FindChildById(rule.Id) is Control child)
                {
                    ApplyRuleToChild(rule, child);
                }

                if (updateSpring)
                {
                    if (_rowBatchDepth > 0)
                    {
                        _rowBatchSpringDirty = true;
                    }
                    else
                    {
                        UpdateSpringFromRules();
                    }
                }
            }
            finally
            {
                _isSyncingRules = false;
            }

            ChangeService?.OnComponentChanged(_host, rulesProperty, null, _rules);
            if (_rowBatchDepth > 0)
            {
                _rowBatchLayoutDirty = true;
                return true;
            }

            Layout();
            return true;
        }

        private VStackRule? FindRuleByControlName(string controlName)
        {
            if (string.IsNullOrWhiteSpace(controlName))
            {
                return null;
            }

            string normalized = controlName.Trim();
            return _rules.FirstOrDefault(rule =>
                string.Equals(rule.ControlName.Trim(), normalized, StringComparison.Ordinal));
        }

        private void SetIntProp(Control child, string propertyName, int value)
        {
            PropertyDescriptor? prop = TypeDescriptor.GetProperties(child)[propertyName];
            object? old = prop?.GetValue(child);
            if (old is int oldInt && oldInt == value)
            {
                return;
            }

            ChangeService?.OnComponentChanging(child, prop);
            if (prop != null)
            {
                prop.SetValue(child, value);
            }

            ChangeService?.OnComponentChanged(child, prop, old, value);
        }

        private void SetProp(Control child, string propertyName, object value, object oldValue)
        {
            PropertyDescriptor? prop = TypeDescriptor.GetProperties(child)[propertyName];
            ChangeService?.OnComponentChanging(child, prop);
            if (prop != null)
            {
                prop.SetValue(child, value);
            }

            ChangeService?.OnComponentChanged(child, prop, oldValue, value);
        }
    }
}
