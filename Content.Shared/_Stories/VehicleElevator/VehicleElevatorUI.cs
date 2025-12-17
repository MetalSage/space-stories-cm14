using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.VehicleElevator;

[Serializable, NetSerializable]
public enum VehicleElevatorUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class VehicleElevatorBuiState : BoundUserInterfaceState
{
    public RequisitionsElevatorMode? PlatformLowered;
    public bool Busy;
    public bool HasOrder;
    public bool ComputerActive;

    public VehicleElevatorBuiState(RequisitionsElevatorMode? platformLowered, bool busy, bool hasOrder, bool computerActive)
    {
        PlatformLowered = platformLowered;
        Busy = busy;
        HasOrder = hasOrder;
        ComputerActive = computerActive;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleElevatorBuyMsg(EntProtoId order) : BoundUserInterfaceMessage
{
    public EntProtoId Order = order;
}

[Serializable, NetSerializable]
public sealed class VehicleElevatorPlatformMsg(bool raise) : BoundUserInterfaceMessage
{
    public bool Raise = raise;
}
