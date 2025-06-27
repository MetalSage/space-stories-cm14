using Content.Shared._Stories.Attachables;
using Robust.Client.UserInterface;

namespace Content.Client._Stories.APC.Attachables.UI;

public sealed class APCAttachmentStripBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private APCAttachableHolderStripMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<APCAttachableHolderStripMenu>();

        var metaQuery = EntMan.GetEntityQuery<MetaDataComponent>();
        if (metaQuery.TryGetComponent(Owner, out var metadata))
            _menu.Title = metadata.EntityName;

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not APCAttachableHolderStripUserInterfaceState msg)
            return;

        _menu?.UpdateMenu(msg.AttachableSlots, slotId => SendMessage(new APCAttachableHolderDetachMessage(slotId)));
    }
}
