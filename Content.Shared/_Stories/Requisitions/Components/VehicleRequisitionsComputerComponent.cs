using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Requisitions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleRequisitionsSystem))]
public sealed partial class VehicleRequisitionsComputerComponent : Component
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