using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._Stories.Vehicle.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleSystem))]
public sealed partial class VehiclePilotSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsGunner;

    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();

    [DataField, AutoNetworkedField]
    public List<EntProtoId> ActionIds = new();

    [DataField, AutoNetworkedField]
    public Vector2 Zoom = Vector2.One;
}
