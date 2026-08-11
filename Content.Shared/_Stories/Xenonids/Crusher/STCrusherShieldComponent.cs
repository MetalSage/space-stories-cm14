using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.Crusher;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STCrusherShieldSystem))]
public sealed partial class STCrusherShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public STCrusherShieldState State = STCrusherShieldState.Off;

    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 25;

    [DataField, AutoNetworkedField]
    public TimeSpan ToggleDelay = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public TimeSpan NextToggleAt;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxPool = 400;

    [DataField, AutoNetworkedField]
    public FixedPoint2 SavedPool = -1;

    [DataField, AutoNetworkedField]
    public FixedPoint2 FlatReduction = 20;

    [DataField, AutoNetworkedField]
    public TimeSpan RegenDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public FixedPoint2 RegenPerSecond = 8;

    [DataField, AutoNetworkedField]
    public TimeSpan LastDamageAt;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MinResistPercent = FixedPoint2.New(0.10);

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxResistPercent = FixedPoint2.New(0.30);

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxResistHpFraction = FixedPoint2.New(0.6);

    [DataField, AutoNetworkedField]
    public FixedPoint2 BreakShrapnelDamage = 40;

    [DataField, AutoNetworkedField]
    public float BreakShrapnelRange = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan BreakEnemySlowTime = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public TimeSpan BreakSelfSpeedBurstTime = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public int BreakArmorPenalty = 10;

    [DataField, AutoNetworkedField]
    public TimeSpan BreakLockoutTime = TimeSpan.FromSeconds(45);

    [DataField, AutoNetworkedField]
    public TimeSpan LockoutEndsAt;

    [DataField, AutoNetworkedField]
    public EntProtoId BreakEffect = "STEffectCrusherShieldBreak";

    [DataField, AutoNetworkedField]
    public SoundSpecifier BreakSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/hit_on_shattered_glass.ogg");
}

[Serializable, NetSerializable]
public enum STCrusherShieldState : byte
{
    Off,
    Active,
    Broken,
}
