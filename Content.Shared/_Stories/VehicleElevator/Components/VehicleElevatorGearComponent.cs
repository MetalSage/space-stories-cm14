using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.VehicleElevator.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedVehicleElevatorSystem))]
public sealed partial class VehicleElevatorGearComponent : Component
{
    [DataField, AutoNetworkedField]
    public RequisitionsGearMode Mode;

    [DataField, AutoNetworkedField]
    public string StaticState = "base";

    [DataField, AutoNetworkedField]
    public string MovingState = "moving";
}
