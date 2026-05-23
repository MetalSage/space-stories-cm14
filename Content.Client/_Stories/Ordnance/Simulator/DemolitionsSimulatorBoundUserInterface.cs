using Content.Shared._Stories.Ordnance.Simulator;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Client.Eye;

namespace Content.Client._Stories.Ordnance.Simulator;

[UsedImplicitly]
public sealed class DemolitionsSimulatorBoundUserInterface : BoundUserInterface
{
    private DemolitionsSimulatorWindow? _window;
    private readonly EyeLerpingSystem _eyeLerping;

    public DemolitionsSimulatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerping = EntMan.System<EyeLerpingSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = new DemolitionsSimulatorWindow();
        _window.OpenCentered();
        _window.OnClose += Close;

        _window.EjectButton.OnPressed += _ => SendMessage(new DemolitionsSimulatorEjectMessage());
        _window.OnDetonate += () => SendMessage(new DemolitionsSimulatorDetonateMessage());
        _window.ResetButton.OnPressed += _ => SendMessage(new DemolitionsSimulatorResetMessage());

        _window.ModeHumanButton.OnPressed += _ => SendMessage(new DemolitionsSimulatorSwitchCategoryMessage("Human"));
        _window.ModeXenoButton.OnPressed += _ => SendMessage(new DemolitionsSimulatorSwitchCategoryMessage("Xeno"));
        _window.ModeStructureButton.OnPressed += _ => SendMessage(new DemolitionsSimulatorSwitchCategoryMessage("Structure"));

        _window.PrototypeDropdown.OnItemSelected += args =>
        {
            var selectedProto = _window.GetPrototypeByIndex(args.Id);
            if (!string.IsNullOrEmpty(selectedProto))
                SendMessage(new DemolitionsSimulatorSwitchProtoMessage(selectedProto));
        };

        Refresh();
    }

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent<DemolitionsSimulatorComponent>(Owner, out var sim))
            return;

        var hasItem = false;
        string? itemName = null;

        var itemSlots = EntMan.System<ItemSlotsSystem>();
        if (itemSlots.TryGetSlot(Owner, sim.ItemSlotId, out var slot) && slot.Item != null)
        {
            hasItem = true;
            if (EntMan.TryGetComponent<MetaDataComponent>(slot.Item.Value, out var metaData))
                itemName = metaData.EntityName;
        }

        _window.UpdateState(
            hasItem,
            itemName,
            sim.NextDetonationTime,
            sim.Cooldown,
            sim.SelectedCategory,
            sim.SelectedPrototype,
            sim.SpawnCategories
        );

        if (sim.Camera != null &&
            EntMan.TryGetEntity(sim.Camera, out var cameraUid) &&
            EntMan.TryGetComponent<EyeComponent>(cameraUid.Value, out var eye))
        {
            _eyeLerping.AddEye(cameraUid.Value);
            _window.Viewport.Eye = eye.Eye;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
