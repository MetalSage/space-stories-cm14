using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Marines.Skills;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleControllerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();

    [DataField, AutoNetworkedField]
    public List<EntProtoId> ActionIds = new();

    [DataField, AutoNetworkedField]
    public EntityUid? ControllableEntity;

    [DataField, AutoNetworkedField]
    public string Id = string.Empty;

}