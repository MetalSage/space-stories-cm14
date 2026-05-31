using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._RMC14.Vehicle.Ui;
using Content.Shared._RMC14.Vehicle.Supply;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Vehicle.Supply;

public sealed class VehicleSupplyBui : BoundUserInterface
{
    private VehicleSupplyWindow? _window;
    private VehicleSupplyWindowController? _windowController;
    private string? _selectedVehicleId;
    private bool _suppressEvents;
    private readonly List<string> _availableVehicleIds = new();
    private readonly Dictionary<string, int> _availableCounts = new();
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

        _window.OnClose += Close;
        _windowController = new VehicleSupplyWindowController(_window);
        _window.Title = string.Empty;
        _window.RaiseButton.OnPressed += _ => SendMessage(new VehicleSupplyLiftMsg(true));
        _window.LowerButton.OnPressed += _ => SendMessage(new VehicleSupplyLiftMsg(false));

        // Stories-Vehicle-Start
        _window.PurchaseButton.OnPressed += _ =>
        {
            if (_suppressEvents)
                return;

            if (!string.IsNullOrWhiteSpace(_selectedVehicleId))
                SendMessage(new VehicleSupplyPurchaseMsg(_selectedVehicleId));
        };
        // Stories-Vehicle-End

        Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window != null)
            _window.OnClose -= Close;

        _windowController?.Dispose();
        _windowController = null;
        _window?.Dispose();
        _window = null;
    }

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out VehicleSupplyConsoleComponent? console))
            return;

        var uiState = console.Ui;
        _suppressEvents = true;
        UpdateStatus(uiState);
        UpdateLists(uiState);
        _windowController?.RefreshPreview(uiState.Preview, GetSelectedVehicleName(uiState));
        _suppressEvents = false;
    }

    private void UpdateStatus(VehicleSupplyUiState state)
    {
        if (_window == null)
            return;

        // Stories-Vehicle-Start
        var modeText = state.LiftMode == null
            ? Loc.GetString("rmc-vehicle-supply-mode-no-lift")
            : Loc.GetString($"rmc-vehicle-supply-mode-{state.LiftMode.ToString()!.ToLowerInvariant()}");

        var activeText = string.IsNullOrWhiteSpace(state.ActiveVehicleId)
            ? Loc.GetString("rmc-vehicle-supply-status-none")
            : state.ActiveVehicleId;

        var busyText = Loc.GetString(state.Busy ? "rmc-vehicle-supply-status-busy" : "rmc-vehicle-supply-status-idle");

        _window.StatusLabel.Text = Loc.GetString("rmc-vehicle-supply-status-lift",
            ("mode", modeText),
            ("status", busyText),
            ("active", activeText));

        var selectedIsPurchasable = false;
        foreach (var entry in state.Available)
        {
            if (entry.Id == _selectedVehicleId)
            {
                selectedIsPurchasable = entry.IsPurchasable;
                break;
            }
        }

        _window.PurchaseButton.Visible = selectedIsPurchasable;
        _window.RaiseButton.Visible = !selectedIsPurchasable;
        _window.LowerButton.Visible = !selectedIsPurchasable;
        // Stories-Vehicle-End

        var raising = state.LiftMode == VehicleSupplyLiftMode.Raising;
        var lowering = state.LiftMode == VehicleSupplyLiftMode.Lowering;
        _window.RaiseButton.Pulse = raising;
        _window.LowerButton.Pulse = lowering;
        _windowController?.RefreshLiftActivity(state.LiftMode, state.Busy);
    }

    private void UpdateLists(VehicleSupplyUiState state)
    {
        if (_window == null)
            return;

        _availableVehicleIds.Clear();
        _availableCounts.Clear();
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
                if (entry.Id == _selectedVehicleId && !entry.LockedByPop) // Stories-Vehicle
                {
                    hasSelected = true;
                    break;
                }
            }
        }

        // Stories-Vehicle-Start
        if (!hasSelected && state.Available.Count > 0)
        {
            foreach (var entry in state.Available)
            {
                if (!entry.LockedByPop)
                {
                    _selectedVehicleId = entry.Id;
                    break;
                }
            }
        }
        // Stories-Vehicle-End

        foreach (var entry in state.Available)
        {
            var label = entry.Count > 1 ? $"{entry.Name} x{entry.Count}" : entry.Name;
            _availableVehicleIds.Add(entry.Id);
            _availableCounts[entry.Id] = entry.Count;

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalExpand = true
            };

            var select = new HardpointButton
            {
                LabelText = label,
                HorizontalExpand = true,
                Disabled = entry.LockedByPop // Stories-Vehicle
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
                    LabelText = _copyExpanded.Contains(vehicleId)
                        ? Loc.GetString("rmc-vehicle-supply-copies-expanded")
                        : Loc.GetString("rmc-vehicle-supply-copies-collapsed"), // Stories-Vehicle
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

        // Stories-Vehicle-Start
        var selectedIsPurchasable = false;
        foreach (var entry in _availableVehicleIds)
        {
        }
        if (EntMan.TryGetComponent(Owner, out VehicleSupplyConsoleComponent? console))
        {
            var uiState = console.Ui;
            foreach (var entry in uiState.Available)
            {
                if (entry.Id == vehicleId)
                {
                    selectedIsPurchasable = entry.IsPurchasable;
                    break;
                }
            }
        }
        if (_window != null)
        {
            _window.PurchaseButton.Visible = selectedIsPurchasable;
            _window.RaiseButton.Visible = !selectedIsPurchasable;
            _window.LowerButton.Visible = !selectedIsPurchasable;
        }
        // Stories-Vehicle-End

        SendMessage(new VehicleSupplySelectMsg(vehicleId, copyIndex));
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var (id, button) in _selectButtons)
        {
            ApplySelectionStyle(button, id == _selectedVehicleId);
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
        toggle.LabelText = expanded
            ? Loc.GetString("rmc-vehicle-supply-copies-expanded")
            : Loc.GetString("rmc-vehicle-supply-copies-collapsed"); // Stories-Vehicle
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

    private string? GetSelectedVehicleName(VehicleSupplyUiState state)
    {
        var selectedId = state.SelectedVehicleId;
        if (string.IsNullOrWhiteSpace(selectedId))
            return null;

        foreach (var entry in state.Available)
        {
            if (entry.Id == selectedId)
                return entry.Name;
        }

        return null;
    }
}
