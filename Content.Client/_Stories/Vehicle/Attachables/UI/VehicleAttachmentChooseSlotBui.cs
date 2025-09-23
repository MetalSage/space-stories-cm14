using Content.Shared._Stories.Attachables;
using Robust.Client.UserInterface;

namespace Content.Client._Stories.Vehicle.Attachables.UI;

public sealed class VehicleAttachmentChooseSlotBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private VehicleAttachableHolderChooseSlotMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<VehicleAttachableHolderChooseSlotMenu>();

        var metaQuery = EntMan.GetEntityQuery<MetaDataComponent>();
        if (metaQuery.TryGetComponent(Owner, out var metadata))
            _menu.Title = metadata.EntityName;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VehicleAttachableHolderChooseSlotUserInterfaceState msg)
            return;

        if (_menu == null)
            return;

        _menu.UpdateMenu(msg.AttachableSlots,
            slotId =>
            {
                SendMessage(new VehicleAttachableHolderAttachToSlotMessage(slotId));
                _menu.Close();
            });
    }
}
