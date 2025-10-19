using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.VehicleElevator.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleElevatorSystem))]
public sealed partial class VehicleElevatorComputerComponent : Component
{
    [DataField]
    public EntityUid? Platform;

    [DataField]
    public bool IsActive = true;

    [DataField, AutoNetworkedField]
    public bool UsedOnce = false;

    [DataField(required: true), AutoNetworkedField, AlwaysPushInheritance]
    public Dictionary<EntProtoId, int> Orders = new();
}
