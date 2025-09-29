using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Vehicle.UI;

[UsedImplicitly]
public sealed class VehicleSelectHardpointBui : BoundUserInterface
{
    private VehicleSelectHardpointWindow? _window;
    private EntityUid? _selectedHardpoint;
    private readonly Dictionary<Button, EntityUid> _buttonToHardpoint = new();

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
        _buttonToHardpoint.Clear();

        var vehicle = GetVehicleComponent();
        if (vehicle == null || vehicle.Hardpoints.Count == 0)
        {
            _window.HardpointsContainer.AddChild(new Label { Text = "Нет доступных модулей" });
            return;
        }

        foreach (var hardpoint in vehicle.Hardpoints)
        {
            if (!EntMan.EntityExists(hardpoint))
                continue;

            if (!EntMan.HasComponent<VehicleGunComponent>(hardpoint))
                continue;

            var button = new Button
            {
                Text = EntMan.GetComponent<MetaDataComponent>(hardpoint).EntityName,
                HorizontalExpand = true,
                Margin = new Thickness(2)
            };

            _buttonToHardpoint[button] = hardpoint;

            button.OnPressed += _ =>
            {
                _selectedHardpoint = hardpoint;
                UpdateButtons();
                SendMessage(new VehicleSelectHardpointBuiMsg(EntMan.GetNetEntity(hardpoint)));
            };

            _window.HardpointsContainer.AddChild(button);
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        foreach (var kvp in _buttonToHardpoint)
        {
            var button = kvp.Key;
            var hardpoint = kvp.Value;

            if (_selectedHardpoint == hardpoint)
            {
                button.ModulateSelfOverride = Color.LightGreen;
            }
            else
            {
                button.ModulateSelfOverride = null;
            }
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window != null && state is VehicleHardpointWindowUserInterfaceState)
        {
            PopulateHardpoints();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
            _window = null;
        }
    }
}
