using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Attachables;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Stories.Vehicle.UI.Weapons;

[UsedImplicitly]
public sealed class VehicleWeaponLoaderBui : BoundUserInterface
{
    private VehicleWeaponLoaderWindow? _window;
    private readonly Dictionary<Button, NetEntity> _buttonToHardpoint = new();

    public VehicleWeaponLoaderBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new VehicleWeaponLoaderWindow();
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        
        if (state is not VehicleWeaponLoaderWindowState loaderState)
            return;

        PopulateHardpoints(loaderState);
    }

    private void PopulateHardpoints(VehicleWeaponLoaderWindowState state)
    {
        if (_window == null)
            return;

        _window.HardpointsContainer.DisposeAllChildren();
        _buttonToHardpoint.Clear();

        if (state.Hardpoints.Count == 0)
        {
            _window.HardpointsContainer.AddChild(new Label
            {
                Text = Loc.GetString("st-vehicle-ui-no-any-hardpoint")
            });
            return;
        }

        foreach (var hardpointInfo in state.Hardpoints)
        {
            if (EntMan.TryGetComponent<VehicleAttachableComponent>(EntMan.GetEntity(hardpointInfo.Entity), 
                out var attachable) && 
                attachable.Ignored)
                continue;

            var container = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Margin = new Thickness(2)
            };

            var statusText = hardpointInfo.HasActiveMagazine 
                ? Loc.GetString("st-vehicle-ui-magazine-loaded") 
                : Loc.GetString("st-vehicle-ui-magazine-empty");

            var ammoInfo = hardpointInfo.HasActiveMagazine 
                ? Loc.GetString("st-vehicle-ui-ammo-info", ("current", hardpointInfo.CurrentAmmo), ("max", hardpointInfo.MaxAmmo)) 
                : "";

            var buttonText = Loc.GetString("st-vehicle-ui-hardpoint-button", 
                ("name", hardpointInfo.Name), 
                ("status", statusText), 
                ("ammo", ammoInfo));

            var button = new Button
            {
                Text = buttonText,
                HorizontalExpand = true
            };

            var spareInfo = new Label
            {
                Text = Loc.GetString("st-vehicle-ui-spare-info", ("current", hardpointInfo.SpareCount), ("max", hardpointInfo.MaxSpares)),
                StyleClasses = { "LabelSubText" },
                FontColorOverride = Color.Gray
            };

            _buttonToHardpoint[button] = hardpointInfo.Entity;

            button.OnPressed += _ =>
            {
                SendMessage(new VehicleWeaponLoaderSelectHardpointMsg(hardpointInfo.Entity));
            };

            button.Disabled = hardpointInfo.SpareCount == 0;

            container.AddChild(button);
            container.AddChild(spareInfo);

            _window.HardpointsContainer.AddChild(container);
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
