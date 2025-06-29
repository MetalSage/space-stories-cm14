using Content.Client.Stylesheets;
using Content.Shared._Stories.APC;
using Content.Shared._Stories.Attachables;
using Content.Shared.IdentityManagement;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stories.APC.UI;

[UsedImplicitly]
public sealed class APCSelectHardpointBui : BoundUserInterface
{
    private EntityUid? _selectedHardpoint;
    private Direction _previewRotation = Direction.South;
    private APCSelectHardpointWindow? _window;
    private APCEntityComponent? _cachedApc;

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
        if (_window == null || _selectedHardpoint == null)
            return;

        SendPredictedMessage(new APCSelectHardpointBuiMsg(EntMan.GetNetEntity(_selectedHardpoint.Value)));
        _window.Close();
    }

    private void PopulateHardpoints()
    {
        if (_window == null)
            return;

        var apc = GetAPCComponent();
        if (apc == null)
        {
            _window.Select.Disabled = true;
            return;
        }

        _cachedApc = apc;
        _window.HardpointsContainer.DisposeAllChildren();

        if (apc.Hardpoints.Count == 0)
        {
            _window.Select.Disabled = true;
            return;
        }

        foreach (var hardpoint in apc.Hardpoints)
        {
            if (!EntMan.EntityExists(hardpoint))
                continue;

            AddHardpointButtonToList(apc, hardpoint);
        }

        if (apc.ActiveHardpoint != null && EntMan.EntityExists(apc.ActiveHardpoint.Value))
        {
            _selectedHardpoint = apc.ActiveHardpoint;
            UpdatePreview(apc.ActiveHardpoint.Value);
        }
        else
        {
            _selectedHardpoint = null;
            ClearPreview();
        }

        UpdateSelectButtonState();
    }

    private APCEntityComponent? GetAPCComponent()
    {
        if (EntMan.TryGetComponent<APCEntityComponent>(Owner, out var apc))
            return apc;

        if (!EntMan.TryGetComponent<TransformComponent>(Owner, out var xform) ||
            xform.GridUid == null)
            return null;

        if (!EntMan.TryGetComponent<APCEntityGridComponent>(xform.GridUid, out var apcGrid) ||
            apcGrid.APC == null)
            return null;

        var apcEntity = EntMan.GetEntity(apcGrid.APC);
        if (!EntMan.EntityExists(apcEntity))
            return null;

        EntMan.TryGetComponent<APCEntityComponent>(apcEntity, out apc);
        return apc;
    }

    private void AddHardpointButtonToList(APCEntityComponent apc, EntityUid hardpoint)
    {
        if (_window == null || !EntMan.EntityExists(hardpoint))
            return;

        var isSelected = _selectedHardpoint == hardpoint;
        var isActive = apc.ActiveHardpoint == hardpoint;

        var button = new APCHardpointButton(hardpoint)
        {
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = isSelected || (_selectedHardpoint == null && isActive),
            Text = Identity.Name(hardpoint, EntMan),
            Margin = new Thickness(5f),
            StyleClasses = { StyleBase.ButtonOpenRight }
        };

        button.OnToggled += args =>
        {
            if (_window == null || _cachedApc == null)
                return;

            if (args.Pressed)
            {
                DeselectOtherButtons(button);
                HandleHardpointSelection(hardpoint);
            }
            else
            {
                if (_selectedHardpoint == hardpoint)
                    HandleHardpointDeselection();
            }
        };

        _window.HardpointsContainer.AddChild(button);
    }

    private void DeselectOtherButtons(APCHardpointButton excludeButton)
    {
        if (_window == null)
            return;

        foreach (var child in _window.HardpointsContainer.Children)
        {
            if (child is APCHardpointButton otherButton && otherButton != excludeButton)
                otherButton.Pressed = false;
        }
    }

    private void HandleHardpointSelection(EntityUid hardpoint)
    {
        if (!EntMan.EntityExists(hardpoint))
            return;

        _selectedHardpoint = hardpoint;
        UpdatePreview(hardpoint);
        UpdateSelectButtonState();
    }

    private void HandleHardpointDeselection()
    {
        _selectedHardpoint = null;

        if (_cachedApc?.ActiveHardpoint != null && EntMan.EntityExists(_cachedApc.ActiveHardpoint.Value))
        {
            UpdatePreview(_cachedApc.ActiveHardpoint.Value);
        }
        else
        {
            ClearPreview();
        }

        UpdateSelectButtonState();
    }

    private void UpdateSelectButtonState()
    {
        if (_window == null || _cachedApc == null)
            return;

        var disabled = _selectedHardpoint == null || _selectedHardpoint == _cachedApc.ActiveHardpoint;
        _window.Select.Disabled = disabled;
    }

    private void RotatePreview(Direction rotation)
    {
        if (_window?.Mob == null)
            return;

        _previewRotation = rotation;
        _window.Mob.OverrideDirection = rotation;
    }

    private void UpdatePreview(EntityUid hardpoint)
    {
        if (_window?.Mob == null || !EntMan.EntityExists(hardpoint))
            return;

        _window.Mob.SetEntity(hardpoint);
        RotatePreview(_previewRotation);
    }

    private void ClearPreview()
    {
        if (_window?.Mob == null)
            return;

        _window.Mob.SetEntity(null);
        RotatePreview(_previewRotation);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        if (_window != null)
        {
            _window.Select.OnPressed -= OnSelectButtonPressed;
            _window.OnClose -= Close;
            _window.Dispose();
            _window = null;
        }

        _cachedApc = null;
        _selectedHardpoint = null;

        base.Dispose(disposing);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not APCHardpointWindowUserInterfaceState)
            return;

        if (_window?.IsOpen == true)
        {
            PopulateHardpoints();
        }
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
