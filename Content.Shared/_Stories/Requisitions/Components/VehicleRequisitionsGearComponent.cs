using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Shared._Stories.Requisitions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedVehicleRequisitionsSystem))]
public sealed partial class VehicleRequisitionsGearComponent : Component
{
    [DataField, AutoNetworkedField]
    public RequisitionsGearMode Mode;

    [DataField, AutoNetworkedField]
    public string StaticState = "base";

    [DataField, AutoNetworkedField]
    public string MovingState = "moving";
}
