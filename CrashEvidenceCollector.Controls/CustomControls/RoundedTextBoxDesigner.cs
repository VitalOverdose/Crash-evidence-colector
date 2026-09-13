using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// Smart-tag for RoundedTextBox (and RoundedNumericTextBox via inheritance). One click
    /// fixes the long-running intermittent "inner textbox doesn't blend" problem for an
    /// instance: the surface colour is written from the parent's effective BackColor through
    /// a PropertyDescriptor, so it serialises into the designer file and supports undo.
    /// </summary>
    public class RoundedTextBoxDesigner : ControlDesigner
    {
        private DesignerActionListCollection? _actionLists;

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                _actionLists ??= new DesignerActionListCollection
                {
                    new RoundedTextBoxActionList(Component)
                };
                return _actionLists;
            }
        }
    }

    public class RoundedTextBoxActionList : DesignerActionList
    {
        public RoundedTextBoxActionList(IComponent component) : base(component) { }

        public void SyncInnerColourToParent()
        {
            if (Component is not RoundedTextBox box || box.Parent == null)
                return;

            SetProp(box, nameof(RoundedTextBox.TextBoxBackColor), EffectiveParentColor(box));
        }

        public void SyncInnerAndHoverToParent()
        {
            if (Component is not RoundedTextBox box || box.Parent == null)
                return;

            Color surface = EffectiveParentColor(box);
            SetProp(box, nameof(RoundedTextBox.TextBoxBackColor), surface);
            SetProp(box, nameof(RoundedTextBox.HoverBackColor), surface);
        }

        // The nearest opaque ancestor colour — same rule the runtime uses for flat blending.
        private static Color EffectiveParentColor(Control control)
        {
            for (Control? p = control.Parent; p != null; p = p.Parent)
            {
                if (p.BackColor.A == 255)
                    return p.BackColor;
            }

            return SystemColors.Control;
        }

        private static void SetProp(object target, string name, object value)
        {
            PropertyDescriptor? prop = TypeDescriptor.GetProperties(target)[name];
            prop?.SetValue(target, value);
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            return new DesignerActionItemCollection
            {
                new DesignerActionMethodItem(this, nameof(SyncInnerColourToParent),
                    "Sync inner colour to parent", "Colours",
                    "Set TextBoxBackColor to the parent's effective BackColor so the inner textbox blends.", true),
                new DesignerActionMethodItem(this, nameof(SyncInnerAndHoverToParent),
                    "Sync inner + hover to parent", "Colours",
                    "As above, and HoverBackColor too (no tint change under the cursor).", true),
            };
        }
    }
}
