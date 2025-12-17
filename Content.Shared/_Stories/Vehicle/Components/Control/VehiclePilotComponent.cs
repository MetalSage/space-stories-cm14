using System.Numerics;
using Content.Shared._Stories.Vehicle.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleSystem))]
public sealed partial class VehiclePilotComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public EntityUid? Gun;

    [DataField, AutoNetworkedField]
    public bool DrawOverlay = true;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> Actions = new();

    [DataField, AutoNetworkedField]
    public Vector2 StoredZoom = Vector2.One;
}
