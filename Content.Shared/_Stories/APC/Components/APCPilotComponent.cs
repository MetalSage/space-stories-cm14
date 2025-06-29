using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCPilotComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? APC;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCGunnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? APC;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public abstract partial class BaseAPCSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? APC;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();
}


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCPilotSeatComponent : BaseAPCSeatComponent
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCGunnerSeatComponent : BaseAPCSeatComponent
{
    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    [DataField, AutoNetworkedField]
    public EntProtoId? Action = "STAPCHardpointMenuAction";
}
