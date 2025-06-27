using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.APC;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared._Stories.Attachables;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Content.Client._Stories.APC.UI;

[UsedImplicitly]
public sealed class APCSelectHardpointBui : BoundUserInterface
{
    private EntityUid? _selectedHardpoint;
    private Direction _previewRotation = Direction.South;
    private APCSelectHardpointWindow? _window;

    public APCSelectHardpointBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) 
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new APCSelectHardpointWindow();

        _window.Select.OnPressed += OnSelectButtonPressed;
        _window.OnClose += Close;

        PopulateHardpoints();
        RotatePreview(_previewRotation);

        _window.OpenCentered();
    }

    private void OnSelectButtonPressed(BaseButton.ButtonEventArgs args)
    {
        if (_selectedHardpoint == null)
            return;

        SendPredictedMessage(new APCSelectHardpointBuiMsg(EntMan.GetNetEntity(_selectedHardpoint.Value)));
    }

    private void PopulateHardpoints()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent<APCEntityComponent>(Owner, out var apc))
            if (!EntMan.TryGetComponent<TransformComponent>(Owner, out var xform) ||
                !EntMan.TryGetComponent<APCEntityGridComponent>(xform.GridUid, out var apcGrid) ||
                !EntMan.TryGetComponent<APCEntityComponent>(EntMan.GetEntity(apcGrid.APC), out apc))
            {
                _window.Select.Disabled = true;
                return;
            }

        _window.HardpointsContainer.DisposeAllChildren();

        foreach (var hardpoint in apc.Hardpoints)
        {
            AddHardpointButtonToList(apc, hardpoint);
        }

        if (apc.ActiveHardpoint != null)
        {
            _selectedHardpoint = apc.ActiveHardpoint;
            UpdatePreview(apc.ActiveHardpoint.Value);
        }
        else
        {
            _selectedHardpoint = null;
            RotatePreview(_previewRotation);
        }

        UpdateSelectButtonState(apc);
    }

    private void AddHardpointButtonToList(APCEntityComponent apc, EntityUid hardpoint)
    {
        if (_window == null)
            return;

        var button = new APCHardpointButton(hardpoint)
        {
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = (_selectedHardpoint == hardpoint) || (_selectedHardpoint == null && apc.ActiveHardpoint == hardpoint),
            Text = Identity.Name(hardpoint, EntMan),
            Margin = new Thickness(5f),
            StyleClasses = { StyleBase.ButtonOpenRight }
        };

        button.OnToggled += args =>
        {
            if (_window == null)
                return;

            if (args.Pressed)
            {
                foreach (var child in _window.HardpointsContainer.Children)
                {
                    if (child is APCHardpointButton otherButton && otherButton != button)
                        otherButton.Pressed = false;
                }

                HandleHardpointSelection(apc, hardpoint);
            }
            else
            {
                if (_selectedHardpoint == hardpoint)
                    HandleHardpointDeselection(apc);
            }
        };

        _window.HardpointsContainer.AddChild(button);
    }

    private void HandleHardpointSelection(APCEntityComponent apc, EntityUid hardpoint)
    {
        _selectedHardpoint = hardpoint;
        UpdatePreview(hardpoint);
        UpdateSelectButtonState(apc);
    }

    private void HandleHardpointDeselection(APCEntityComponent apc)
    {
        _selectedHardpoint = null;

        if (_window == null)
            return;

        if (apc.ActiveHardpoint != null)
            UpdatePreview(apc.ActiveHardpoint.Value);

        UpdateSelectButtonState(apc);
    }

    private void UpdateSelectButtonState(APCEntityComponent apc)
    {
        if (_window == null)
            return;

        var disabled = _selectedHardpoint == null || _selectedHardpoint == apc.ActiveHardpoint;
        _window.Select.Disabled = disabled;
    }

    private void RotatePreview(Direction rotation)
    {
        if (_window?.Mob == null)
            return;

        _window.Mob.OverrideDirection = rotation;
    }

    private void UpdatePreview(EntityUid hardpoint)
    {
        _window?.Mob.SetEntity(hardpoint);
        RotatePreview(_previewRotation);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
        {
            _window.Select.OnPressed -= OnSelectButtonPressed;
            _window.OnClose -= Close;

            _window.Dispose();
            _window = null;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not APCHardpointWindowUserInterfaceState msg)
            return;

        if (_window != null && _window.IsOpen)
            PopulateHardpoints();
    }

    private sealed class APCHardpointButton : Button
    {
        public EntityUid Hardpoint { get; }

        public APCHardpointButton(EntityUid hardpoint)
        {
            Hardpoint = hardpoint;
        }
    }
}
