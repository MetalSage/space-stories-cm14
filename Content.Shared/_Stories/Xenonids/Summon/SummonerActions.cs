using Content.Shared.Actions;

namespace Content.Shared._Stories.Xenonids.Summon;

public sealed partial class SummonerRaiseArmyActionEvent : InstantActionEvent
{

}

public sealed partial class SummonerOrderActionEvent : InstantActionEvent
{
    /// <summary>
    /// The type of order being given
    /// </summary>
    [DataField("type")]
    public SummonerOrderType Type;
}
