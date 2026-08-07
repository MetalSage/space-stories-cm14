using Content.Shared._Stories.Hunter.Profiles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Marking.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class HunterComponent : Component
{
    [DataField]
    public ComponentRegistry? AddComponents;

    [DataField] [AutoNetworkedField]
    public EntityUid? MarkForHuntAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId MarkForHuntActionId = "STActionMarkForHunt";

    [DataField] [AutoNetworkedField]
    public EntityUid? OpenMarkPanelAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId OpenMarkPanelActionId = "STActionOpenMarkPanel";

    [DataField] [AutoNetworkedField]
    public NetEntity? Prey;

    [DataField]
    public ComponentRegistry? RemoveComponents;

    [DataField] [AutoNetworkedField]
    public HunterStatus Status = HunterStatus.Normal;

    [DataField]
    public float StunResistance = 2.5f;

    [DataField] [AutoNetworkedField]
    public NetEntity? Thrall;
}
