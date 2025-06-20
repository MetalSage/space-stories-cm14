using Content.Shared._Stories.Attachables;
using Robust.Client.UserInterface;

namespace Content.Client._Stories.APC.UI;

public sealed class APCAttachmentChooseSlotBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private APCAttachableHolderChooseSlotMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<APCAttachableHolderChooseSlotMenu>();

        var metaQuery = EntMan.GetEntityQuery<MetaDataComponent>();
        if (metaQuery.TryGetComponent(Owner, out var metadata))
            _menu.Title = metadata.EntityName;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not APCAttachableHolderChooseSlotUserInterfaceState msg)
            return;

        if (_menu == null)
            return;

        _menu.UpdateMenu(msg.AttachableSlots,
            slotId =>
            {
                SendMessage(new APCAttachableHolderAttachToSlotMessage(slotId));
                _menu.Close();
            });
    }
}
