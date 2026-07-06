using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Breacher.Components;

/// <summary>
///     Marks a suit as the Breacher's M40 armor, granting the Enrage ability while worn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BreacherArmorSystem))]
public sealed partial class BreacherArmorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> RequiredSkill = "RMCSkillSpecialistWeapons";

    [DataField, AutoNetworkedField]
    public TimeSpan EnrageDuration = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public ProtoId<DamageModifierSetPrototype> EnrageDamageResist = "STBreacherEnrageResist";

    /// <summary>
    ///     Divides the duration of incoming Stun/KnockedDown/Unconscious/Dazed effects while enraged.
    ///     A high value approximates immunity without fully blocking the effect from being applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EnrageStunResistance = 1000f;

    /// <summary>
    ///     How long before the end of Enrage the body flash starts pulsing faster as a warning.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BlinkThreshold = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     Flash interval once inside <see cref="BlinkThreshold"/> -- fast, like blinking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BlinkInterval = TimeSpan.FromSeconds(0.3);

    /// <summary>
    ///     Flash interval for the rest of the duration -- slow, steady pulse.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PulseInterval = TimeSpan.FromSeconds(2);
}
