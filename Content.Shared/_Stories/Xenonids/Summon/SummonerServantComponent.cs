using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Summon;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSummonerSystem))]
[AutoGenerateComponentState]
public sealed partial class SummonerServantComponent : Component
{
    /// <summary>
    /// The Summoner this xenoling belongs to.
    /// </summary>
    [DataField("king")]
    [AutoNetworkedField]
    public EntityUid? King;
}
