using Content.Shared._Stories.Attachables;
using Robust.Client.UserInterface;

namespace Content.Client._Stories.Vehicle.Attachables.UI;

public sealed class VehicleAttachmentStripBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private VehicleAttachableHolderStripMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<VehicleAttachableHolderStripMenu>();

        var metaQuery = EntMan.GetEntityQuery<MetaDataComponent>();
        if (metaQuery.TryGetComponent(Owner, out var metadata))
            _menu.Title = metadata.EntityName;

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VehicleAttachableHolderStripUserInterfaceState msg)
            return;

        _menu?.UpdateMenu(msg.AttachableSlots, slotId => SendMessage(new VehicleAttachableHolderDetachMessage(slotId)));
    }
}
