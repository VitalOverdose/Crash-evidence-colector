using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Editors;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    // VStack tier 2: a RoundedPanelFaster that stacks its children via the shared VStackEngine.
    // Same stacking/spring behaviour as vStackPanel, but with the rounded chrome, border, and
    // optional top/bottom bars of RoundedPanelFaster.
    [Designer(typeof(VStackDesigner))]
    public class vStackRoundedPanel : RoundedPanelFaster, IVStackHost
    {
        private readonly VStackEngine _engine;

        public vStackRoundedPanel()
        {
            _engine = new VStackEngine(this);
            CornerRadius = 15;
            Padding = new Padding(5);
        }

        VStackEngine IVStackHost.VStackEngine => _engine;

        [Category("VStack")]
        [Description("Authoring rules: spawn items, reorder, set margins (and collapse). Each rule owns one child control, joined by a stable id kept in the control's Tag.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(VStackRuleCollectionEditor), typeof(CollectionEditor))]
        public VStackRuleCollection Rules => _engine.Rules;

        [DefaultValue(null)]
        [Category("VStack")]
        [Description("Comma-separated TabIndexes that absorb spare vertical space, e.g. \"1,4\". Empty = no spring. Multiple springs share the spare evenly.")]
        public string? Spring
        {
            get => _engine.Spring;
            set => _engine.Spring = value;
        }

        [DefaultValue(true)]
        [Category("VStack")]
        [Description("Master on/off for spring behaviour. Turn off while authoring so you can grow the panel and drop a control in without a spring eating the space, then turn back on.")]
        public bool SpringEnabled
        {
            get => _engine.SpringEnabled;
            set => _engine.SpringEnabled = value;
        }

        [DefaultValue(36)]
        [Category("VStack")]
        [Description("The smallest height a spring contracts to when space is tight. Defaults to 36, the standard AdaptiveRowPanel height. A child's own MinimumSize.Height can raise it but not lower it.")]
        public int SpringMinimumHeight
        {
            get => _engine.SpringMinimumHeight;
            set => _engine.SpringMinimumHeight = value;
        }

        [DefaultValue(false)]
        [Category("VStack")]
        [Description("When true, this stack steps direct child fonts down 2pt at 150%+ DPI even if the global StepDownHighDpiFonts setting is off. Nested layout hosts manage their own children.")]
        public bool StepDownFontAtHighDpi
        {
            get => _engine.StepDownFontAtHighDpi;
            set => _engine.StepDownFontAtHighDpi = value;
        }

        public VStackRow Row(string controlName) => _engine.Row(controlName);

        /// <summary>
        /// Groups several row mutations into ONE relayout. Dispose the returned token (a using
        /// block) to apply them together — required when a change is only valid alongside its
        /// siblings, such as swapping which row is the piston.
        /// </summary>
        public IDisposable BeginRowBatch() => _engine.BeginRowBatch();

        public bool Expand(string controlName) => _engine.SetRowCollapsed(controlName, false);

        public bool Collapse(string controlName) => _engine.SetRowCollapsed(controlName, true);

        public bool Toggle(string controlName) => _engine.ToggleRowCollapsed(controlName);

        /// <summary>
        /// Splits the stack evenly between whichever adjustable rows (spring or piston) are
        /// visible — the vertical mirror of AdaptiveRowPanel.EqualizeAdjustableItems. Authored
        /// pixel heights can only ever be right for one window size, and springs cannot fix a
        /// piston/spring pair on their own.
        /// </summary>
        public int EqualizeAdjustableRows() => _engine.EqualizeAdjustableRows();

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            // Guarded: the RoundedPanelFaster base ctor can trigger a layout before _engine is set.
            _engine?.Layout();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null)
            {
                _engine?.AttachChild(e.Control);
            }
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            if (e.Control != null)
            {
                _engine?.DetachChild(e.Control);
            }
        }
    }
}
