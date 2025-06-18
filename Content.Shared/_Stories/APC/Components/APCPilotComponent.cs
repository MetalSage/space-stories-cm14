using Robust.Shared.GameStates;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCPilotComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? APC;
}


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCPilotSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? APC;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();
}