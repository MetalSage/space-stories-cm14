using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using System.Collections.Generic;

namespace Content.Client._Stories.Vehicle.UI;

[UsedImplicitly]
public sealed class VehicleSelectHardpointBui : BoundUserInterface
{
    private VehicleSelectHardpointWindow? _window;
    private readonly Dictionary<EntityUid, Button> _hardpointButtons = new();

    public VehicleSelectHardpointBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new VehicleSelectHardpointWindow();
        _window.OnClose += Close;
        _window.OpenCentered();

        PopulateHardpoints();
    }

    private VehicleComponent? GetVehicleComponent()
    {
        if (EntMan.TryGetComponent<VehicleComponent>(Owner, out var vehicle))
            return vehicle;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform) &&
            xform.GridUid.HasValue &&
            EntMan.TryGetComponent<VehicleGridComponent>(xform.GridUid.Value, out var vehicleGrid) &&
            EntMan.TryGetComponent(EntMan.GetEntity(vehicleGrid.Vehicle), out vehicle))
        {
            return vehicle;
        }

        return null;
    }

    private void PopulateHardpoints()
    {
        if (_window == null)
            return;

        _window.HardpointsContainer.DisposeAllChildren();
        _hardpointButtons.Clear();

        var vehicle = GetVehicleComponent();
        if (vehicle == null || vehicle.Hardpoints.Count == 0)
        {
            _window.HardpointsContainer.AddChild(new Label
            {
                Text = Loc.GetString("st-vehicle-ui-no-any-hardpoint"),
                HorizontalExpand = true
            });
            return;
        }

        foreach (var hardpoint in vehicle.Hardpoints)
        {
            if (!EntMan.EntityExists(hardpoint))
                continue;

            if (!EntMan.HasComponent<VehicleGunComponent>(hardpoint))
                continue;

            if (EntMan.TryGetComponent<VehicleAttachableComponent>(hardpoint, out var attachable) && attachable.Ignored)
                continue;

            var button = new Button
            {
                Text = EntMan.GetComponent<MetaDataComponent>(hardpoint).EntityName,
                HorizontalExpand = true,
                Margin = new Thickness(4)
            };

            button.OnPressed += _ =>
            {
                SendMessage(new VehicleSelectHardpointBuiMsg(EntMan.GetNetEntity(hardpoint)));
                UpdateButtons();
            };

            _hardpointButtons[hardpoint] = button;
            _window.HardpointsContainer.AddChild(button);
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var vehicle = GetVehicleComponent();
        var activeHardpoint = vehicle?.ActiveHardpoint;

        foreach (var kvp in _hardpointButtons)
        {
            var hardpoint = kvp.Key;
            var button = kvp.Value;

            if (activeHardpoint != null && activeHardpoint == hardpoint)
                button.ModulateSelfOverride = Color.FromHex("#90EE90");
            else
                button.ModulateSelfOverride = null;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window != null && state is VehicleHardpointWindowUserInterfaceState)
            UpdateButtons();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
            _window = null;
            _hardpointButtons.Clear();
        }
    }
}
