using System.Numerics;
using Content.Client._RMC14.Vehicle.Ui;
using Content.Shared._RMC14.Vehicle.Supply;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Localization;
using System.Collections.Generic;

namespace Content.Client._RMC14.Vehicle.Supply;

public sealed class VehicleSupplyBui : BoundUserInterface
{
    private VehicleSupplyWindow? _window;
    private string? _selectedVehicleId;
    private bool _suppressEvents;
    private bool _lastBusy;
    private readonly List<string> _availableVehicleIds = new();
    private readonly Dictionary<string, int> _availableCounts = new();
    private readonly Dictionary<string, bool> _lockedByPop = new();
    private readonly Dictionary<string, bool> _purchasable = new(); // Stories-Vehicle
    private readonly Dictionary<string, int> _selectedCopyIndices = new();
    private readonly Dictionary<string, HardpointButton> _selectButtons = new();
    private readonly Dictionary<string, HardpointButton> _copyToggleButtons = new();
    private readonly Dictionary<string, BoxContainer> _copyContainers = new();
    private readonly Dictionary<string, List<HardpointButton>> _copyButtons = new();
    private readonly HashSet<string> _copyExpanded = new();

    public VehicleSupplyBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<VehicleSupplyWindow>();

        if (_window == null)
            return;

        _window.Title = string.Empty;
        _window.RaiseButton.OnPressed += _ => SendMessage(new VehicleSupplyLiftMsg(true));
        _window.LowerButton.OnPressed += _ => SendMessage(new VehicleSupplyLiftMsg(false));
        _window.PurchaseButton.OnPressed += _ =>
        {
            if (!string.IsNullOrWhiteSpace(_selectedVehicleId))
                SendMessage(new VehicleSupplyPurchaseMsg(_selectedVehicleId));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VehicleSupplyBuiState uiState || _window == null)
            return;

        _lastBusy = uiState.Busy;
        _suppressEvents = true;
        UpdateStatus(uiState);
        UpdateLists(uiState);
        _window.SetPreview(uiState.Preview);
        _suppressEvents = false;

        UpdateSelectionVisuals();
    }

    private void UpdateStatus(VehicleSupplyBuiState state)
    {
        if (_window == null)
            return;

        var modeText = state.LiftMode switch
        {
            VehicleSupplyLiftMode.Lowered => Loc.GetString("rmc-vehicle-supply-mode-lowered"),
            VehicleSupplyLiftMode.Raised => Loc.GetString("rmc-vehicle-supply-mode-raised"),
            VehicleSupplyLiftMode.Lowering => Loc.GetString("rmc-vehicle-supply-mode-lowering"),
            VehicleSupplyLiftMode.Raising => Loc.GetString("rmc-vehicle-supply-mode-raising"),
            VehicleSupplyLiftMode.Preparing => Loc.GetString("rmc-vehicle-supply-mode-preparing"),
            _ => Loc.GetString("rmc-vehicle-supply-mode-no-lift")
        };

        var activeText = string.IsNullOrWhiteSpace(state.ActiveVehicleId) ? Loc.GetString("rmc-vehicle-supply-status-none") : state.ActiveVehicleId;
        var busyText = state.Busy ? Loc.GetString("rmc-vehicle-supply-status-busy") : Loc.GetString("rmc-vehicle-supply-status-idle");

        _window.StatusLabel.Text = Loc.GetString("rmc-vehicle-supply-status-lift", ("mode", modeText), ("status", busyText), ("active", activeText));

        var raising = state.LiftMode == VehicleSupplyLiftMode.Raising;
        var lowering = state.LiftMode == VehicleSupplyLiftMode.Lowering;
        _window.RaiseButton.Pulse = raising;
        _window.LowerButton.Pulse = lowering;
        _window.SetLiftActivity(state.LiftMode, state.Busy);
    }

    private void UpdateLists(VehicleSupplyBuiState state)
    {
        if (_window == null)
            return;

        _availableVehicleIds.Clear();
        _availableCounts.Clear();
        _lockedByPop.Clear();
        _purchasable.Clear(); // Stories-Vehicle
        _window.AvailableRows.DisposeAllChildren();
        _selectButtons.Clear();
        _copyToggleButtons.Clear();
        _copyContainers.Clear();
        _copyButtons.Clear();

        if (state.Available.Count == 0)
        {
            _selectedVehicleId = null;
            return;
        }

        _selectedVehicleId = state.SelectedVehicleId;
        var hasSelected = false;
        if (!string.IsNullOrWhiteSpace(_selectedVehicleId))
        {
            foreach (var entry in state.Available)
            {
                if (entry.Id == _selectedVehicleId)
                {
                    hasSelected = true;
                    break;
                }
            }
        }

        if (!hasSelected && state.Available.Count > 0)
            _selectedVehicleId = state.Available[0].Id;

        foreach (var entry in state.Available)
        {
            var label = entry.Count > 1 ? $"{entry.Name} x{entry.Count}" : entry.Name;
            _availableVehicleIds.Add(entry.Id);
            _availableCounts[entry.Id] = entry.Count;
            _lockedByPop[entry.Id] = entry.LockedByPop;
            _purchasable[entry.Id] = entry.IsPurchasable; // Stories-Vehicle

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalExpand = true
            };

            var select = new HardpointButton
            {
                LabelText = label,
                HorizontalExpand = true
            };

            var vehicleId = entry.Id;
            select.OnPressed += _ =>
            {
                if (_suppressEvents)
                    return;

                SelectVehicle(vehicleId, _selectedCopyIndices.TryGetValue(vehicleId, out var copy) ? copy : 0);
            };

            ApplySelectionStyle(select, _selectedVehicleId == vehicleId);

            row.AddChild(select);
            _selectButtons[vehicleId] = select;

            if (entry.Count > 1)
            {
                var copyToggle = new HardpointButton
                {
                    LabelText = _copyExpanded.Contains(vehicleId) ? Loc.GetString("rmc-vehicle-supply-copies-expanded") : Loc.GetString("rmc-vehicle-supply-copies-collapsed"),
                    MinSize = new Vector2(110, 0)
                };

                var copies = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(12, 0, 0, 0),
                    HorizontalExpand = true,
                    Visible = _copyExpanded.Contains(vehicleId)
                };

                for (var i = 0; i < entry.Count; i++)
                {
                    var copyIndex = i;
                    var copyButton = new HardpointButton
                    {
                        LabelText = $"    #{i + 1}",
                        HorizontalExpand = true
                    };

                    copyButton.OnPressed += _ =>
                    {
                        if (_suppressEvents)
                            return;

                        _selectedCopyIndices[vehicleId] = copyIndex;
                        UpdateCopySelection(vehicleId);
                        SelectVehicle(vehicleId, copyIndex);
                    };

                    copies.AddChild(copyButton);
                    if (!_copyButtons.TryGetValue(vehicleId, out var list))
                    {
                        list = new List<HardpointButton>();
                        _copyButtons[vehicleId] = list;
                    }

                    list.Add(copyButton);
                }

                row.AddChild(copyToggle);
                _copyToggleButtons[vehicleId] = copyToggle;
                _copyContainers[vehicleId] = copies;

                copyToggle.OnPressed += _ =>
                {
                    if (_suppressEvents)
                        return;

                    if (_copyExpanded.Contains(vehicleId))
                        _copyExpanded.Remove(vehicleId);
                    else
                        _copyExpanded.Add(vehicleId);

                    UpdateCopyExpanded(vehicleId);
                };
            }

            var outer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 2,
                HorizontalExpand = true
            };
            outer.AddChild(row);

            if (_copyContainers.TryGetValue(vehicleId, out var copyContainer))
            {
                outer.AddChild(copyContainer);
            }

            _window.AvailableRows.AddChild(outer);
        }

        foreach (var (vehicleId, count) in _availableCounts)
        {
            if (count <= 1)
                continue;

            if (vehicleId == _selectedVehicleId)
                _selectedCopyIndices[vehicleId] = state.SelectedCopyIndex;
            else if (!_selectedCopyIndices.TryGetValue(vehicleId, out var index) || index < 0 || index >= count)
                _selectedCopyIndices[vehicleId] = 0;

            UpdateCopySelection(vehicleId);
            UpdateCopyExpanded(vehicleId);
        }
    }

    private void SelectVehicle(string vehicleId, int copyIndex)
    {
        if (_selectedVehicleId == vehicleId)
        {
            SendMessage(new VehicleSupplySelectMsg(vehicleId, copyIndex));
            return;
        }

        _selectedVehicleId = vehicleId;
        UpdateSelectionVisuals();
        SendMessage(new VehicleSupplySelectMsg(vehicleId, copyIndex));
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var (id, button) in _selectButtons)
        {
            ApplySelectionStyle(button, id == _selectedVehicleId);
        }

        if (_window != null && !string.IsNullOrWhiteSpace(_selectedVehicleId))
        {
            if (_purchasable.GetValueOrDefault(_selectedVehicleId)) // Stories-Vehicle
            {
                _window.PurchaseButton.Visible = true;
                _window.RaiseButton.Visible = false;
                // Stories-Start
                _window.LowerButton.Visible = false;
                // Stories-End

                var isLocked = _lockedByPop.GetValueOrDefault(_selectedVehicleId);
                _window.PurchaseButton.Disabled = isLocked || _lastBusy;
                _window.PurchaseButton.LabelText = isLocked
                    ? Loc.GetString("rmc-vehicle-supply-locked-pop")
                    : Loc.GetString("rmc-vehicle-supply-purchase");
            }
            else
            {
                _window.PurchaseButton.Visible = false;
                _window.RaiseButton.Visible = true;
                // Stories-Start
                _window.LowerButton.Visible = true;
                // Stories-End
            }
        }
        else if (_window != null)
        {
            _window.PurchaseButton.Visible = false;
            _window.RaiseButton.Visible = true;
            // Stories-Start
            _window.LowerButton.Visible = true;
            // Stories-End
        }
    }

    private void UpdateCopySelection(string vehicleId)
    {
        if (!_copyButtons.TryGetValue(vehicleId, out var buttons))
            return;

        if (!_selectedCopyIndices.TryGetValue(vehicleId, out var selected))
            selected = 0;

        for (var i = 0; i < buttons.Count; i++)
        {
            ApplySelectionStyle(buttons[i], i == selected);
        }
    }

    private void UpdateCopyExpanded(string vehicleId)
    {
        if (!_copyContainers.TryGetValue(vehicleId, out var container) ||
            !_copyToggleButtons.TryGetValue(vehicleId, out var toggle))
        {
            return;
        }

        var expanded = _copyExpanded.Contains(vehicleId);
        container.Visible = expanded;
        toggle.LabelText = expanded ? Loc.GetString("rmc-vehicle-supply-copies-expanded") : Loc.GetString("rmc-vehicle-supply-copies-collapsed");
    }

    private static void ApplySelectionStyle(HardpointButton button, bool selected)
    {
        button.Selected = selected;
        button.SelectedColor = HardpointButton.DefaultUnhoveredColor;
        button.UnhoveredColor = Color.FromHex("#1A3D5C");
        button.HoveredColor = HardpointButton.DefaultHoveredColor;
        button.DisabledColor = HardpointButton.DefaultDisabledColor;
        button.TextColor = selected
            ? HardpointButton.DefaultTextColor
            : HardpointButton.DefaultUnselectedTextColor;
        button.DisabledTextColor = HardpointButton.DefaultDisabledTextColor;

        button.RefreshStyle();
    }
}
