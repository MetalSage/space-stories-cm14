using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.FixedPoint;

namespace Content.Shared._Stories.Xenonids.Summon;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSummonerSystem))]
[AutoGenerateComponentState]
public sealed partial class SummonerComponent : Component
{
    [DataField("actionRaiseArmy", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionRaiseArmy = "ActionSummonerRaiseArmy";

    /// <summary>
    ///     The action for the Raise Army ability
    /// </summary>
    [DataField("actionRaiseArmyEntity")]
    public EntityUid? ActionRaiseArmyEntity;

    /// <summary>
    ///     The amount of plasma one use of Raise Army consumes
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 20;

    /// <summary>
    ///     The entity prototype of the mob that Raise Army summons
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("armyMobSpawnId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ArmyMobSpawnId = "MobXenoLing";

    /// <summary>
    /// The current order that the Summoner assigned.
    /// </summary>
    [DataField("currentOrders"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public SummonerOrderType CurrentOrder = SummonerOrderType.Loose;

    /// <summary>
    /// The servants that the Summoner is currently controlling
    /// </summary>
    [DataField("servants")]
    public HashSet<EntityUid> Servants = new();

    [DataField("actionOrderStay", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderStay = "ActionSummonerOrderStay";

    [DataField("actionOrderStayEntity")]
    public EntityUid? ActionOrderStayEntity;

    [DataField("actionOrderFollow", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderFollow = "ActionSummonerOrderFollow";

    [DataField("actionOrderFollowEntity")]
    public EntityUid? ActionOrderFollowEntity;

    [DataField("actionOrderAttack", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderAttack = "ActionSummonerOrderAttack";

    [DataField("actionOrderAttackEntity")]
    public EntityUid? ActionOrderAttackEntity;

    [DataField("actionOrderLoose", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderLoose = "ActionSummonerOrderLoose";

    [DataField("actionOrderLooseEntity")]
    public EntityUid? ActionOrderLooseEntity;

    /// <summary>
    /// A dictionary with an order type to the corresponding callout dataset.
    /// </summary>
    [DataField("orderCallouts")]
    public Dictionary<SummonerOrderType, string> OrderCallouts = new()
    {
        { SummonerOrderType.Stay, "SummonerCommandStay" },
        { SummonerOrderType.Follow, "SummonerCommandFollow" },
        { SummonerOrderType.Attack, "SummonerCommandAttack" },
        { SummonerOrderType.Loose, "SummonerCommandLoose" }
    };
}

[Serializable, NetSerializable]
public enum SummonerOrderType : byte
{
    Stay,
    Follow,
    Attack,
    Loose
}
