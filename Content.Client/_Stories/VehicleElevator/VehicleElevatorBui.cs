using Content.Shared._Stories.VehicleElevator;
using Content.Shared._Stories.VehicleElevator.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Content.Client._RMC14;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Client._Stories.VehicleElevator;

[UsedImplicitly]
public sealed class VehicleElevatorBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    [ViewVariables]
    private VehicleElevatorWindow? _window;

    private readonly List<(EntProtoId Order, Button button)> _orderButtons = new();

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<VehicleElevatorWindow>();

        _window.LowerPlatformButton.OnPressed += _ => SendMessage(new VehicleElevatorPlatformMsg(false));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is VehicleElevatorBuiState uiState)
            UpdateState(uiState);
    }

    private void UpdateState(VehicleElevatorBuiState uiState)
    {
        _window ??= this.CreateWindow<VehicleElevatorWindow>();

        var platformLabel = "No platform";
        var showLowerButton = false;
        
        switch (uiState.PlatformLowered)
        {
            case Lowered:
                platformLabel = "Platform position: Lowered";
                break;
            case Raised:
                platformLabel = "Platform position: Raised";
                showLowerButton = true;
                break;
            case Lowering:
                platformLabel = "Platform lowering...";
                break;
            case Raising:
                platformLabel = "Platform raising...";
                break;
            case null:
                platformLabel = "No platform";
                break;
            default:
                platformLabel = $"Platform position: {uiState.PlatformLowered}";
                break;
        }

        _window.PlatformLabel.SetMessage(platformLabel);
        _window.LowerPlatformButton.Visible = showLowerButton;

        if (_orderButtons.Count == 0)
            CreateOrderButtons(uiState);

        UpdateOrderButtonsState(uiState);

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void CreateOrderButtons(VehicleElevatorBuiState uiState)
    {
        if (_window == null)
            return;

        if (!_entities.TryGetComponent(Owner, out VehicleElevatorComputerComponent? computer))
            return;

        _window.VehiclesContainer.DisposeAllChildren();
        _orderButtons.Clear();

        foreach (var (orderProto, requiredOnline) in computer.Orders)
        {
            if (_playerManager.PlayerCount < requiredOnline)
                continue;

            var button = new Button
            {
                HorizontalExpand = true
            };
            
            button.AddStyleClass("ButtonSquare");
            button.AddStyleClass(CMStyleClasses.CMLabelAlignLeft);
            button.Label.AddStyleClass(CMStyleClasses.CMLabelAlignLeft);
            
            var itemName = _prototypes.Index<EntityPrototype>(orderProto).Name;
            button.Text = $"{itemName} (Required: {requiredOnline} players)";
            
            var order = orderProto;
            button.OnPressed += _ => OnOrderButtonPressed(order);
            
            _orderButtons.Add((orderProto, button));
            _window.VehiclesContainer.AddChild(button);
        }
    }

    private void OnOrderButtonPressed(EntProtoId order)
    {
        var state = State as VehicleElevatorBuiState;
        if (state == null)
            return;

        if (state.PlatformLowered == Lowered && !state.Busy && !state.HasOrder && state.ComputerActive)
        {
            SendMessage(new VehicleElevatorBuyMsg(order));
        }
    }

    private void UpdateOrderButtonsState(VehicleElevatorBuiState uiState)
    {
        foreach (var (_, button) in _orderButtons)
        {
            button.Disabled = !uiState.ComputerActive ||
                            uiState.PlatformLowered != Lowered ||
                            uiState.Busy ||
                            uiState.HasOrder;
        }
    }
}