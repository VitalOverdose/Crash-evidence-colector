using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public class AdaptiveRowLabelDesigner : ControlDesigner
    {
        private DesignerActionListCollection? _actionLists;

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    _actionLists.Add(new AdaptiveRowLabelActionList(Component));
                    _actionLists.AddRange(base.ActionLists);
                }
                return _actionLists;
            }
        }
    }

    public class AdaptiveRowLabelActionList : DesignerActionList
    {
        private int _gapPx = 2;

        public AdaptiveRowLabelActionList(IComponent component) : base(component) { }

        private AdaptiveRowLabel? Label => Component as AdaptiveRowLabel;

        private IComponentChangeService? ChangeService =>
            (IComponentChangeService?)Component?.Site?.GetService(typeof(IComponentChangeService));

        private IDesignerHost? DesignerHost =>
            (IDesignerHost?)Component?.Site?.GetService(typeof(IDesignerHost));

        public int GapPx
        {
            get => _gapPx;
            set => _gapPx = Math.Max(0, value);
        }

        public void FitToContainer()
        {
            if (Label is not { } label || label.Parent == null)
                return;

            int newHeight = Math.Max(label.MinimumSize.Height, label.Parent.ClientSize.Height - _gapPx * 2);
            var prop = TypeDescriptor.GetProperties(label)["LabelHeight"];
            int old = label.LabelHeight;
            if (old == newHeight)
                return;

            var svc = ChangeService;
            using var tx = DesignerHost?.CreateTransaction("Fit Label to Container");
            svc?.OnComponentChanging(label, prop);
            if (prop != null)
                prop.SetValue(label, newHeight);
            else
                label.LabelHeight = newHeight;
            svc?.OnComponentChanged(label, prop, old, newHeight);
            tx?.Commit();
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();
            items.Add(new DesignerActionHeaderItem("Sizing"));
            items.Add(new DesignerActionPropertyItem(nameof(GapPx), "Gap (px)", "Sizing", "Pixel gap above and below when fitting to container height"));
            items.Add(new DesignerActionMethodItem(this, nameof(FitToContainer), "Fit Height to Container", "Sizing", true));
            return items;
        }
    }
}
