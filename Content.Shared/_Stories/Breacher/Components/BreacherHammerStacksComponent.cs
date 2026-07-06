using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Breacher.Components;

/// <summary>
///     Tracks consecutive hammer hits on a xeno. Interpreted differently depending on whether the
///     hit that landed was one-handed or wielded (two-handed):
///     - One-handed hits: no chance roll, a guaranteed knockdown triggers once
///       <see cref="OneHandedHitsForKnockdown"/> consecutive hits land.
///     - Wielded hits: each hit rolls a knockdown chance that starts at <see cref="TwoHandedBaseChance"/>
///       and increases every hit, reaching a guaranteed 100% by <see cref="TwoHandedGuaranteedHits"/>.
///     Stacks decay over time if the target isn't hit again within <see cref="IncrementGraceTime"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BreacherHammerSystem))]
public sealed partial class BreacherHammerStacksComponent : Component
{
    [DataField, AutoNetworkedField]
    public int StackCount;

    [DataField, AutoNetworkedField]
    public int OneHandedHitsForKnockdown = 5;

    [DataField, AutoNetworkedField]
    public float TwoHandedBaseChance = 0.1f;

    /// <summary>
    ///     Shapes the chance curve between hit 1 (TwoHandedBaseChance) and the guaranteed hit.
    ///     1 = linear. Higher values back-load the chance so early hits stay low and it ramps up
    ///     sharply only near the guaranteed hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TwoHandedCurvePower = 3f;

    /// <summary>
    ///     Multiplies the computed two-handed chance against small/quick castes (runner-tier and
    ///     below). Big castes never reach this check at all -- they're routed to the superslow
    ///     branch instead, so they're unaffected by this multiplier either way.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SmallXenoChanceMultiplier = 1.5f;

    [DataField, AutoNetworkedField]
    public int TwoHandedGuaranteedHits = 4;

    /// <summary>
    ///     How often a stack decays once past <see cref="IncrementGraceTime"/> since the last hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DecayEvery = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan NextDecay;

    [DataField]
    public TimeSpan LastHitAt;

    /// <summary>
    ///     Stacks won't start decaying until this long has passed since the last hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan IncrementGraceTime = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan BigXenoSuperSlowDuration = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan AttackerStunDuration = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float ThrowStrength = 3f;

    /// <summary>
    ///     Extra damage applied directly to the target when the knockdown/superslow proc triggers,
    ///     on top of the normal hit damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier ProcBonusDamage = new()
    {
        DamageDict = new() { ["Blunt"] = 40 },
    };

    [DataField]
    public SoundSpecifier ProcSound = new SoundCollectionSpecifier("MetalSlam");
}
