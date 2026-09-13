using System.ComponentModel;
using System.ComponentModel.Design;
using ControlDesigner = Microsoft.DotNet.DesignTools.Designers.ControlDesigner;

namespace ProfessorSnowsVideoDownloader.CustomControls;

/// <summary>Out-of-process .NET WinForms designer support for AdaptiveRowStrip.</summary>
public class AdaptiveRowStripDesigner : ControlDesigner
{
    private AdaptiveRowStrip Strip => (AdaptiveRowStrip)Control;

    public override DesignerVerbCollection Verbs => new()
    {
        new DesignerVerb("Add Button", (_, _) => AddItem(typeof(StripButton))),
        new DesignerVerb("Add Icon Button", (_, _) => AddItem(typeof(StripIconButton))),
        new DesignerVerb("Add Caption", (_, _) => AddItem(typeof(StripCaption))),
        new DesignerVerb("Add Spacer", (_, _) => AddItem(typeof(StripSpacer))),
        new DesignerVerb("Add Toggle", (_, _) => AddItem(typeof(StripToggle))),
        new DesignerVerb("Add DropDown", (_, _) => AddItem(typeof(StripDropDown))),
    };

    private void AddItem(Type itemType)
    {
        if (GetService(typeof(IDesignerHost)) is not IDesignerHost host)
            return;

        PropertyDescriptor? itemsProperty = TypeDescriptor.GetProperties(Strip)[nameof(AdaptiveRowStrip.Items)];
        IComponentChangeService? changes = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        using DesignerTransaction transaction = host.CreateTransaction($"Add {itemType.Name}");
        StripItem? item = null;
        try
        {
            changes?.OnComponentChanging(Strip, itemsProperty);
            item = (StripItem)host.CreateComponent(itemType);
            Strip.Items.Add(item);
            changes?.OnComponentChanged(Strip, itemsProperty, null, null);
            transaction.Commit();

            if (GetService(typeof(ISelectionService)) is ISelectionService selection)
                selection.SetSelectedComponents(new object[] { item }, SelectionTypes.Primary);
        }
        catch
        {
            if (item?.Site != null)
                host.DestroyComponent(item);
            transaction.Cancel();
            throw;
        }
    }
}
