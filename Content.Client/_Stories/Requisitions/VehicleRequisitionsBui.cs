using Content.Shared._Stories.Requisitions;
using Content.Shared._Stories.Requisitions.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Client._Stories.Requisitions;

[UsedImplicitly]
public sealed class VehicleRequisitionsBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    [ViewVariables]
    private VehicleRequisitionsWindow? _window;

    private readonly List<(EntProtoId Order, VehicleRequisitionsOrderButton button)> _orderButtons = new();

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<VehicleRequisitionsWindow>();

        _window.LowerPlatformButton.OnPressed += _ => SendMessage(new VehicleRequisitionsPlatformMsg(false));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is VehicleRequisitionsBuiState uiState)
            UpdateState(uiState);
    }

    private void UpdateState(VehicleRequisitionsBuiState uiState)
    {
        _window ??= this.CreateWindow<VehicleRequisitionsWindow>();

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

    private void CreateOrderButtons(VehicleRequisitionsBuiState uiState)
    {
        if (_window == null)
            return;

        if (!_entities.TryGetComponent(Owner, out VehicleRequisitionsComputerComponent? computer))
            return;

        _window.ItemsContainer.DisposeAllChildren();
        _orderButtons.Clear();

        foreach (var (orderProto, requiredOnline) in computer.Orders)
        {
            if (_playerManager.PlayerCount < requiredOnline)
                continue;

            var order = new VehicleRequisitionsOrderButton();
            
            var itemName = _prototypes.Index<EntityPrototype>(orderProto).Name;
            order.Button.Text = $"{itemName} (Required: {requiredOnline} players)";
            
            order.Button.OnPressed += _ => OnOrderButtonPressed(orderProto);
            
            _orderButtons.Add((orderProto, order));
            _window.ItemsContainer.AddChild(order);
        }
    }

    private void OnOrderButtonPressed(EntProtoId order)
    {
        var state = State as VehicleRequisitionsBuiState;
        if (state == null)
            return;

        if (state.PlatformLowered == Lowered && !state.Busy && !state.HasOrder && state.ComputerActive)
        {
            SendMessage(new VehicleRequisitionsBuyMsg(order));
        }
    }

    private void UpdateOrderButtonsState(VehicleRequisitionsBuiState uiState)
    {
        foreach (var (_, button) in _orderButtons)
        {
            button.Button.Disabled = !uiState.ComputerActive ||
                                    uiState.PlatformLowered != Lowered ||
                                    uiState.Busy ||
                                    uiState.HasOrder;
        }
    }
}