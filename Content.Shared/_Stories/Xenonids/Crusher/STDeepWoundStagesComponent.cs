using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Crusher;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STXenoDeepWoundsSystem))]
public sealed partial class STDeepWoundStagesComponent : Component
{
    [DataField, AutoNetworkedField]
    public int DeepStage;

    [DataField, AutoNetworkedField]
    public TimeSpan DeepNextEscalateAt;

    [DataField, AutoNetworkedField]
    public int WeepingStage;

    [DataField, AutoNetworkedField]
    public TimeSpan WeepingNextEscalateAt;
}
