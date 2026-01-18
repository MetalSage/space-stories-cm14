using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[Serializable] [NetSerializable]
public enum SelfDestructType
{
    Small,
    Big,
}

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class HunterBracerComponent : Component
{
    [DataField]
    public float AttachmentDeployCost = 50f;

    [DataField]
    public EntProtoId BigExplosionId = "STExplosionBracerBig";

    [DataField]
    public ProtoId<AlertPrototype> BracerPowerAlert = "STBracerPowerBase";

    [DataField] [AutoNetworkedField]
    public bool CasterDeployed;

    [DataField]
    public SoundSpecifier CasterDeploySound = new SoundPathSpecifier(
        "/Audio/_Stories/Weapons/pred_plasmacaster_on.ogg",
        AudioParams.Default.WithMaxDistance(5f)
    );

    [DataField]
    public SoundSpecifier CasterModeCycleSound = new SoundPathSpecifier(
        "/Audio/Machines/button.ogg",
        AudioParams.Default.WithMaxDistance(2f)
    );

    [DataField]
    public SoundSpecifier CasterRetractSound = new SoundPathSpecifier(
        "/Audio/_Stories/Weapons/pred_plasmacaster_off.ogg",
        AudioParams.Default.WithMaxDistance(5f)
    );

    [DataField]
    public Dictionary<PlasmaCasterMode, float> CasterShotCost = new()
    {
        { PlasmaCasterMode.Stun, 30f },
        { PlasmaCasterMode.Immobilizer, 150f },
        { PlasmaCasterMode.Lethal, 500f },
        { PlasmaCasterMode.Overcharge, 1000f },
    };

    [ViewVariables(VVAccess.ReadWrite)] [DataField] [AutoNetworkedField]
    public float Charge = 3000f;

    [DataField]
    public EntProtoId CloakEffect = "RMCEffectCloak";

    [DataField]
    public TimeSpan CloakForcedCooldown = TimeSpan.FromSeconds(5f);

    [DataField] [AutoNetworkedField]
    public SoundSpecifier CloakOffSound = new SoundPathSpecifier(
        "/Audio/_Stories/Effects/Hunter/pred_cloakoff.ogg",
        AudioParams.Default.WithMaxDistance(5f)
    );

    [DataField] [AutoNetworkedField]
    public SoundSpecifier CloakOnSound = new SoundPathSpecifier(
        "/Audio/_Stories/Effects/Hunter/pred_cloakon.ogg",
        AudioParams.Default.WithMaxDistance(5f)
    );

    [DataField]
    public float CloakOpacity = 0.045f;

    [DataField]
    public float CloakPowerCost = 50f;

    [DataField] [AutoNetworkedField]
    public TimeSpan CountdownDuration = TimeSpan.FromSeconds(8);

    [DataField]
    public SoundSpecifier CountdownSound = new SoundPathSpecifier(
        "/Audio/_Stories/Effects/Hunter/pred_countdown.ogg",
        AudioParams.Default.WithMaxDistance(15f)
    );

    [DataField] [AutoNetworkedField]
    public EntityUid? CreateHealingCapsuleAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId CreateHealingCapsuleActionId = "STActionCreateHealingCapsule";

    [DataField] [AutoNetworkedField]
    public EntityUid? CreateStabilizingCrystalAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId CreateStabilizingCrystalActionId = "STActionCreateStabilizingCrystal";

    [DataField]
    public SoundSpecifier DeathlaughSound = new SoundPathSpecifier("/Audio/_Stories/Voice/Hunter/pred_deathlaugh.ogg");

    [DataField]
    public SoundSpecifier DelimbSound = new SoundPathSpecifier("/Audio/_Stories/Weapons/bladeslice.ogg");

    [ViewVariables(VVAccess.ReadWrite)] [DataField] [AutoNetworkedField]
    public SelfDestructType ExplosionType = SelfDestructType.Small;

    [DataField]
    public float HealingCapsuleCost = 600f;

    [DataField]
    public EntProtoId HealingCapsulePrototype = "STHealingGel";

    [ViewVariables(VVAccess.ReadWrite)] [DataField] [AutoNetworkedField]
    public bool Locked = true;

    [DataField]
    public SoundSpecifier? LockSound = new SoundPathSpecifier(
        "/Audio/_RMC14/Medical/air_release.ogg",
        AudioParams.Default.WithMaxDistance(3f)
    );

    [DataField]
    public float MalfunctionDelimbChance = 0.20f;

    [ViewVariables(VVAccess.ReadWrite)] [DataField] [AutoNetworkedField]
    public float MaxCharge = 3000f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoNetworkedField]
    public TimeSpan NextCloakToggleTime;

    [DataField]
    public float PlasmaCasterDeployCost = 50f;

    [DataField("powerCost")]
    public float PowerCost = 50f;

    [DataField]
    public float ReducedRegenRate = 10f;

    [DataField]
    public float RegenRate = 30f;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> RequiredSkill = "STSkillIllegalTechnology";

    [DataField]
    public int RequiredSkillLevel = 1;

    [DataField]
    public bool RestrictWeaponsOnCloak;

    [DataField] [AutoNetworkedField]
    public EntityUid? SelfDestructAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId SelfDestructActionId = "STActionToggleSelfDestruct";

    [DataField] [AutoNetworkedField]
    public bool ShowClanName;

    [DataField]
    public EntProtoId SmallExplosionId = "STExplosionBracerSmall";

    [DataField]
    public float StabilizingCrystalCost = 400f;

    [DataField]
    public EntProtoId StabilizingCrystalPrototype = "STStabilizingCrystal";

    [DataField] [AutoNetworkedField]
    public EntityUid? ToggleAttachmentsAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId ToggleAttachmentsActionId = "STActionToggleBracerAttachments";

    [DataField] [AutoNetworkedField]
    public EntityUid? ToggleCloakAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId ToggleCloakActionId = "STActionToggleHunterCloak";

    [DataField] [AutoNetworkedField]
    public EntityUid? TogglePlasmaCasterAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId TogglePlasmaCasterActionId = "STActionTogglePlasmaCaster";

    [DataField] [AutoNetworkedField]
    public EntityUid? ToggleTranslatorAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId ToggleTranslatorActionId = "STActionToggleTranslator";

    [DataField] [AutoNetworkedField]
    public bool TranslatorActive;

    [DataField]
    public float TranslatorPowerCost = 50f;

    [DataField]
    public SoundSpecifier TranslatorSound = new SoundPathSpecifier(
        "/Audio/Machines/button.ogg",
        AudioParams.Default.WithMaxDistance(2f)
    );

    [DataField]
    public float UnauthorizedMalfunctionChance = 0.10f;

    [DataField]
    public float UnauthorizedSuccessChance = 0.20f;

    [DataField]
    public EntProtoId UncloakEffect = "RMCEffectUncloak";

    [DataField]
    public TimeSpan UncloakWeaponLockDuration = TimeSpan.FromSeconds(1.0);

    [DataField]
    public SoundSpecifier? UnlockSound = new SoundPathSpecifier(
        "/Audio/_RMC14/Medical/air_release.ogg",
        AudioParams.Default.WithMaxDistance(3f)
    );
}
