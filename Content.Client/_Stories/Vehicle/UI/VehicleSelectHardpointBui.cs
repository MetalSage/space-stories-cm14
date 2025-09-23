using Content.Client.Stylesheets;
using Content.Shared._Stories.Vehicle;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Content.Shared._Stories.Attachables;

namespace Content.Client._Stories.Vehicle.UI;

[UsedImplicitly]
public sealed class VehicleSelectHardpointBui : BoundUserInterface
{
    private VehicleSelectHardpointWindow? _window;
    private EntityUid? _selectedHardpoint;
    private Direction _previewRotation = Direction.South;
    private readonly Dictionary<Button, EntityUid> _buttonToHardpoint = new();

    public VehicleSelectHardpointBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new VehicleSelectHardpointWindow();
        _window.OnClose += Close;
        _window.Select.OnPressed += OnSelectButtonPressed;
        
        _window.RotateLeft.OnPressed += _ => RotatePreview(false);
        _window.RotateRight.OnPressed += _ => RotatePreview(true);

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
            _window.HardpointsContainer.AddChild(new Label { Text = "Нет доступных точек крепления." });
            _window.Select.Disabled = true;
            ClearPreview();
            return;
        }

        if (_selectedHardpoint.HasValue && (!vehicle.Hardpoints.Contains(_selectedHardpoint.Value) || !EntMan.EntityExists(_selectedHardpoint)))
        {
            _selectedHardpoint = null;
        }

        _selectedHardpoint ??= vehicle.ActiveHardpoint;

        foreach (var hardpoint in vehicle.Hardpoints)
        {
            if (!EntMan.EntityExists(hardpoint))
                continue;

            AddHardpointButtonToList(hardpoint, vehicle.ActiveHardpoint);
        }

        UpdatePreview();
        UpdateSelectButtonState(vehicle.ActiveHardpoint);
    }

    private void AddHardpointButtonToList(EntityUid hardpoint, EntityUid? activeHardpoint)
    {
        if (_window == null)
            return;

        var button = new Button
        {
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = _selectedHardpoint == hardpoint,
            Text = EntMan.GetComponent<MetaDataComponent>(hardpoint).EntityName
        };
        button.StyleClasses.Add(StyleBase.ButtonOpenRight);
        
        _buttonToHardpoint[button] = hardpoint;

        if (hardpoint == activeHardpoint)
        {
            button.Text = button.Text + " (Активно)";
            button.ModulateSelfOverride = Color.LightGreen;
        }

        button.OnToggled += args =>
        {
            if (args.Pressed)
            {
                _selectedHardpoint = hardpoint;
            }
            else if (_selectedHardpoint == hardpoint)
            {
                 _selectedHardpoint = null;
            }

            RefreshUI();
        };

        _window.HardpointsContainer.AddChild(button);
    }

    private void RefreshUI()
    {
        var vehicle = GetVehicleComponent();
        if (_window == null || vehicle == null)
            return;

        foreach (var child in _window.HardpointsContainer.Children)
        {
            if (child is Button button && _buttonToHardpoint.TryGetValue(button, out var hardpoint))
            {
                button.Pressed = _selectedHardpoint == hardpoint;
                
                var isActive = hardpoint == vehicle.ActiveHardpoint;
                var baseText = EntMan.GetComponent<MetaDataComponent>(hardpoint).EntityName;
                
                if (isActive)
                {
                    button.Text = baseText + " (Активно)";
                    button.ModulateSelfOverride = Color.LightGreen;
                }
                else
                {
                    button.Text = baseText;
                    button.ModulateSelfOverride = null;
                }
            }
        }

        UpdatePreview();
        UpdateSelectButtonState(vehicle.ActiveHardpoint);
    }

    private void OnSelectButtonPressed(BaseButton.ButtonEventArgs args)
    {
        if (_selectedHardpoint.HasValue)
        {
            var netEntity = EntMan.GetNetEntity(_selectedHardpoint.Value);
            if (netEntity != null)
                SendMessage(new VehicleSelectHardpointBuiMsg(netEntity));
        }

        Close(); 
    }

    private void UpdateSelectButtonState(EntityUid? activeHardpoint)
    {
        if (_window == null)
            return;

        _window.Select.Disabled = _selectedHardpoint == null || _selectedHardpoint == activeHardpoint;
    }

    private void UpdatePreview()
    {
        if (_window?.Mob == null)
            return;

        _window.Mob.SetEntity(_selectedHardpoint);
        _window.Mob.OverrideDirection = _previewRotation;
    }

    private void ClearPreview()
    {
        if (_window?.Mob == null)
            return;

        _window.Mob.SetEntity(null);
    }

    private void RotatePreview(bool clockwise)
    {
        if (_window?.Mob == null)
            return;

        var rotationAngle = clockwise ? Angle.FromDegrees(90) : Angle.FromDegrees(-90);
        _previewRotation = (Direction)(((int)_previewRotation + (clockwise ? 1 : 3)) % 4);
        
        _window.Mob.OverrideDirection = _previewRotation;
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