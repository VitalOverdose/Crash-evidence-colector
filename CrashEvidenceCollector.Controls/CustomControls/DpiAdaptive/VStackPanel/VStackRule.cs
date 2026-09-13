using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Editors;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum VStackItemType
    {
        Panel,
        vStackPanel,
        vStackRoundedPanel,
        AdaptiveRowPanel,
        vStackSpacer,
        FlatSeparator,
        VirtualizingAdaptiveRowPanel
    }

    /// <summary>
    /// A row's layout role — one value, so spring-AND-piston is impossible by construction.
    /// Spring: absorbs spare vertical space. Piston: fixed height the user adjusts via an
    /// adjacent FlatSeparator splitter, with the spring absorbing the difference.
    /// </summary>
    public enum VStackSpecialLayout
    {
        None,
        Spring,
        Piston
    }

    // ARP-style authoring rule for one stacked child. The hidden Id joins the rule to the
    // child through Control.Tag; visible properties push values down to the live child.
    public class VStackRule : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _controlName = string.Empty;
        // MUST stay Panel: it's the serialization default, and 39 existing rules across the
        // app omit ItemType because they rely on it. Changing it would reinterpret every one
        // as an ARP, and a type mismatch makes the engine destroy and rebuild the child.
        // New rules get AdaptiveRowPanel from the collection editor instead (CreateInstance).
        private VStackItemType _itemType = VStackItemType.Panel;
        private int _topMargin;
        private int _bottomMargin = 4;
        private VStackSpecialLayout _specialLayout = VStackSpecialLayout.None;
        private bool _isCollapsed;
        private int _expandedHeight = 80;
        private int _collapsedHeight = 80;
        private bool _hasCustomCollapsedHeight;
        private float _fontBaselineSizeInPoints;

        public event PropertyChangedEventHandler? PropertyChanged;

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string Id
        {
            get => _id;
            set => _id = value ?? string.Empty;
        }

        [DefaultValue(VStackItemType.Panel)]
        [Category("Item")]
        [Description("What this rule spawns: Panel, vStackPanel, vStackRoundedPanel, AdaptiveRowPanel, vStackSpacer, or FlatSeparator. Changing it rebuilds the control.")]
        public VStackItemType ItemType
        {
            get => _itemType;
            set => SetField(ref _itemType, value, nameof(ItemType));
        }

        [Category("Item")]
        [Description("Name of the spawned child control. The hidden Id in Tag is the join key, so renaming is safe.")]
        public string ControlName
        {
            get => _controlName;
            set
            {
                string newValue = value ?? string.Empty;
                if (string.Equals(_controlName, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                _controlName = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ControlName)));
            }
        }

        // Compatibility alias for any designer code generated while this property was named Name.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Name
        {
            get => ControlName;
            set => ControlName = value;
        }

        [DefaultValue(0)]
        [Category("Item")]
        [Description("Top margin pushed onto the spawned control.")]
        public int TopMargin
        {
            get => _topMargin;
            set => SetField(ref _topMargin, Math.Max(0, value), nameof(TopMargin));
        }

        [DefaultValue(4)]
        [Category("Item")]
        [Description("Bottom margin pushed onto the spawned control.")]
        public int BottomMargin
        {
            get => _bottomMargin;
            set => SetField(ref _bottomMargin, Math.Max(0, value), nameof(BottomMargin));
        }

        [DefaultValue(VStackSpecialLayout.None)]
        [Category("Layout")]
        [Description("The row's layout role. Spring: absorbs spare vertical space. Piston: fixed height the user adjusts by dragging an adjacent FlatSeparator (the spring absorbs the difference). A row can only ever be one of these — the enum makes both impossible.")]
        public VStackSpecialLayout SpecialLayout
        {
            get => _specialLayout;
            set => SetField(ref _specialLayout, value, nameof(SpecialLayout));
        }

        // ------------------------------------------------------------------------------------
        // LEGACY SHIMS — old designer files serialised `IsSpring = true` / `IsPiston = true`
        // as separate bools. These map onto SpecialLayout so those lines still compile and
        // load, but they are hidden from the grid and never re-serialised: the next designer
        // save writes SpecialLayout instead. Do not use in new code.
        // ------------------------------------------------------------------------------------

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsSpring
        {
            get => _specialLayout == VStackSpecialLayout.Spring;
            set
            {
                if (value)
                    SpecialLayout = VStackSpecialLayout.Spring;
                else if (_specialLayout == VStackSpecialLayout.Spring)
                    SpecialLayout = VStackSpecialLayout.None;
            }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsPiston
        {
            get => _specialLayout == VStackSpecialLayout.Piston;
            set
            {
                if (value)
                    SpecialLayout = VStackSpecialLayout.Piston;
                else if (_specialLayout == VStackSpecialLayout.Piston)
                    SpecialLayout = VStackSpecialLayout.None;
            }
        }

        [DefaultValue(false)]
        [Category("Collapse")]
        [Description("When true, the child is set to CollapsedHeight. When false, it is restored to ExpandedHeight.")]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set => SetField(ref _isCollapsed, value, nameof(IsCollapsed));
        }

        [Category("Collapse")]
        [Description("Height restored when expanded. Designer resizing updates this while IsCollapsed is false.")]
        [DefaultValue(80)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int ExpandedHeight
        {
            get => _expandedHeight;
            set
            {
                int newValue = Math.Max(0, value);
                bool changed = SetField(ref _expandedHeight, newValue, nameof(ExpandedHeight));
                if (!_hasCustomCollapsedHeight)
                {
                    changed |= SetField(ref _collapsedHeight, newValue, nameof(CollapsedHeight));
                }
            }
        }

        [Category("Collapse")]
        [Description("Height used while collapsed. It follows ExpandedHeight until explicitly set or resized while collapsed.")]
        [DefaultValue(80)]
        public int CollapsedHeight
        {
            get => _hasCustomCollapsedHeight ? _collapsedHeight : _expandedHeight;
            set
            {
                // An explicit set is ALWAYS honored verbatim. The old "must be smaller
                // than ExpandedHeight" coercion silently ate serialized values, because
                // the designer assigns properties alphabetically — CollapsedHeight loads
                // BEFORE ExpandedHeight, while ExpandedHeight still holds its default.
                int newValue = Math.Max(0, value);
                bool changed = SetField(ref _collapsedHeight, newValue, nameof(CollapsedHeight));
                changed |= SetField(ref _hasCustomCollapsedHeight, true, nameof(HasCustomCollapsedHeight));

                if (!changed)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollapsedHeight)));
                }
            }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DefaultValue(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool HasCustomCollapsedHeight
        {
            get => _hasCustomCollapsedHeight;
            set
            {
                if (SetField(ref _hasCustomCollapsedHeight, value, nameof(HasCustomCollapsedHeight))
                    && !value)
                {
                    SetField(ref _collapsedHeight, _expandedHeight, nameof(CollapsedHeight));
                }
            }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DefaultValue(0f)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public float FontBaselineSizeInPoints
        {
            get => _fontBaselineSizeInPoints;
            set => SetField(ref _fontBaselineSizeInPoints, Math.Max(0f, value), nameof(FontBaselineSizeInPoints));
        }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(_controlName) ? $"({_itemType})" : $"{_controlName} ({_itemType})";

        internal int GetTargetHeight()
        {
            return IsCollapsed ? CollapsedHeight : ExpandedHeight;
        }

        internal void CaptureExpandedHeight(int height)
        {
            int newHeight = Math.Max(0, height);
            bool changed = SetField(ref _expandedHeight, newHeight, nameof(ExpandedHeight));
            if (!_hasCustomCollapsedHeight)
            {
                changed |= SetField(ref _collapsedHeight, newHeight, nameof(CollapsedHeight));
            }
        }

        internal void CaptureCollapsedHeight(int height)
        {
            // A designer resize while collapsed is an explicit statement of the
            // collapsed height — honor it verbatim, same as the property setter.
            int newHeight = Math.Max(0, height);
            SetField(ref _collapsedHeight, newHeight, nameof(CollapsedHeight));
            SetField(ref _hasCustomCollapsedHeight, true, nameof(HasCustomCollapsedHeight));
        }

        internal void CaptureFontBaseline(float sizeInPoints)
        {
            FontBaselineSizeInPoints = sizeInPoints;
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class VStackRuleCollection : Collection<VStackRule>
    {
        internal VStackEngine? Owner { get; set; }

        protected override void InsertItem(int index, VStackRule item)
        {
            Owner?.PrepareNewRule(item);
            base.InsertItem(index, item);
            Subscribe(item);
            NotifyChanged();
        }

        protected override void SetItem(int index, VStackRule item)
        {
            Owner?.PrepareNewRule(item);
            Unsubscribe(this[index]);
            base.SetItem(index, item);
            Subscribe(item);
            NotifyChanged();
        }

        protected override void RemoveItem(int index)
        {
            VStackRule item = this[index];
            Unsubscribe(item);
            Owner?.QueueRuleChildRemoval(item);
            base.RemoveItem(index);
            NotifyChanged();
        }

        protected override void ClearItems()
        {
            // Queue (never destroy directly): the designer AND the collection editor's OK
            // commit as Clear + re-add, so a queued id is only acted on after the sync
            // settles and its rule genuinely hasn't come back — RemovePendingRuleChildren
            // skips ids still present in the collection. Rules re-added by undo/redo or a
            // content refresh keep their children; rules deleted in the editor lose theirs.
            foreach (VStackRule item in this.ToArray())
            {
                Unsubscribe(item);
                Owner?.QueueRuleChildRemoval(item);
            }

            base.ClearItems();
            NotifyChanged();
        }

        private void Subscribe(VStackRule item) => item.PropertyChanged += Item_PropertyChanged;

        private void Unsubscribe(VStackRule item) => item.PropertyChanged -= Item_PropertyChanged;

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is VStackRule rule && e.PropertyName == nameof(VStackRule.ItemType))
            {
                Owner?.OnRuleTypeChanged(rule);
                return;
            }

            // Any role transition can change spring membership (None→Spring, Spring→Piston…),
            // so the host Spring list resyncs on SpecialLayout changes. The legacy IsSpring
            // shim routes through SpecialLayout, so this covers old callers too.
            if (sender is VStackRule springRule && e.PropertyName == nameof(VStackRule.SpecialLayout))
            {
                Owner?.OnRuleSpringChanged(springRule);
                return;
            }

            NotifyChanged();
        }

        private void NotifyChanged() => Owner?.OnRulesChanged();
    }

    public class VStackRuleCollectionEditor : CollectionEditor
    {
        public VStackRuleCollectionEditor(IServiceProvider provider, Type collectionType)
            : base(provider, collectionType)
        {
        }

        protected override Type[] CreateNewItemTypes() => new[] { typeof(VStackRule) };

        // A new row almost always wants a layout engine, so it spawns an AdaptiveRowPanel
        // rather than a bare Panel. Done here rather than by changing VStackRule's default,
        // because that default is also the SERIALIZATION default — existing rules omit
        // ItemType and would be silently reinterpreted (and their children rebuilt).
        protected override object CreateInstance(Type itemType)
        {
            object instance = base.CreateInstance(itemType);

            if (instance is VStackRule rule)
                rule.ItemType = VStackItemType.AdaptiveRowPanel;

            return instance;
        }
    }
}
