using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.StatusEffect;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.CombatMech;

// Shared container IDs between CombatMechComponent and weapon-side components.
// Field initializers cannot reference instance members of another component, so the
// literals would otherwise be duplicated and drift apart.
public static class CombatMechContainerIds
{
    public const string FlamerTank = "rx47_flamer_tank";
    public const string GunMagazine = "gun_magazine";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(CombatMechSystem))]
public sealed partial class CombatMechComponent : Component
{
    [DataField]
    public string EmptyWeaponState = "empty";

    [DataField]
    public string UnderbarrelSlot = "rmc-aslot-underbarrel";

    [DataField]
    public string GunMagazineContainerId = CombatMechContainerIds.GunMagazine;

    [DataField]
    public string GunChamberContainerId = "gun_chamber";

    [DataField]
    public string WeaponTankContainerId = CombatMechContainerIds.FlamerTank;

    [DataField]
    public string BodyOverlayContainerId = "rx47_body_overlay";

    [DataField(required: true)]
    public EntProtoId PrimaryWeapon;

    [DataField(required: true)]
    public EntProtoId SecondaryWeapon;

    [DataField, AutoNetworkedField]
    public string PrimaryWeaponState = "empty";

    [DataField, AutoNetworkedField]
    public string SecondaryWeaponState = "empty";

    [DataField, AutoNetworkedField]
    public bool HelmetClosed;

    [DataField, AutoNetworkedField]
    public string MarkingsColorState = string.Empty;

    [DataField, AutoNetworkedField]
    public string MarkingsSpecialtyState = string.Empty;

    [DataField, AutoNetworkedField]
    public bool HasTowLauncher;

    [DataField, AutoNetworkedField]
    public float MaxHealth = 3000f;

    [DataField]
    public float DamagedAlertThreshold = 25f;

    [DataField]
    public float CriticalAlertThreshold = 10f;

    [DataField]
    public TimeSpan WeaponInstallDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan WeaponDetachDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ForceEjectDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public int DefaultWeaponEnsureMaxAttempts = 3;

    [DataField]
    public float BaseMoveDelay = 7f;

    [DataField]
    public float MinimumMoveDelay = 3f;

    [DataField]
    public float MoveDelayReductionPerSkill = 2f;

    [DataField]
    public TimeSpan StepStunDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan StepStunCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public float StepDamage = 90f;

    [DataField]
    public EntityWhitelist StepTargetWhitelist = new()
    {
        Components = ["Marine"],
    };

    [DataField]
    public float StepStunOverlapRatio = 0.2f;

    [DataField]
    public TimeSpan StepActiveDuration = TimeSpan.FromSeconds(0.4);

    [DataField]
    public float BarricadeCollisionDamage = 900f;

    [DataField]
    public float BarricadeBumperRange = 0.5f;

    [DataField]
    public float BarricadeBumperProbeRadius = 0.5f;

    [DataField]
    public float BarricadeForwardDotMinimum = 0.35f;

    [DataField]
    public float WeaponDetachDropDistance = 1.1f;

    [DataField]
    public TimeSpan BarricadeBumperCooldown = TimeSpan.FromSeconds(0.25);

    [DataField]
    public Vector2 PilotVisualOffsetNorth = new(0f, 0.45f);

    [DataField]
    public Vector2 PilotVisualOffsetSouth = new(0f, 0.12f);

    [DataField]
    public Vector2 PilotVisualOffsetEastWest = new(0f, 0.28f);

    // Used only when the vehicle has no Damageable damageContainer; otherwise forwarding is
    // derived from the container's supported types/groups.
    [DataField]
    public List<ProtoId<DamageTypePrototype>> ForwardedDamageTypes = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Shock",
        "Caustic",
    };

    [DataField]
    public HashSet<ProtoId<StatusEffectPrototype>> ProtectedStatusEffects = new()
    {
        "Blinded",
        "Dazed",
        "Drunk",
        "Flashed",
        "KnockedDown",
        "SlowedDown",
        "Stun",
    };

    [DataField]
    public EntProtoId<SkillDefinitionComponent> WeaponSkill = "RMCSkillPowerLoader";

    [DataField]
    public int WeaponSkillRequired = 3;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> ForceEjectSkill = "RMCSkillFireman";

    [DataField]
    public int ForceEjectSkillRequired = 1;

    [DataField]
    public SoundSpecifier? EnterSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_powerloader_step.ogg");

    [DataField]
    public SoundSpecifier? ExitSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_powerloader_step.ogg");

    [DataField]
    public SoundSpecifier? DamageAlertSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    [AutoNetworkedField]
    public EntityUid? PrimaryWeaponEntity;

    [AutoNetworkedField]
    public EntityUid? SecondaryWeaponEntity;

    [AutoNetworkedField]
    public EntityUid? PilotEntity;

    [DataField(required: true)]
    public EntProtoId BodyOverlayPrototype;

    [AutoNetworkedField]
    public EntityUid? BodyOverlayEntity;

    [DataField]
    public int PilotRenderOrder = 1;

    [ViewVariables]
    public bool DamagedAlertTriggered;

    [ViewVariables]
    public bool CriticalAlertTriggered;

    [ViewVariables]
    public TimeSpan NextBarricadeBumpAt;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> NextStepStunAt = new();

    [ViewVariables]
    public TimeSpan LastStepMoveAt;

    [ViewVariables]
    public bool DefaultWeaponEnsureQueued;

    [ViewVariables]
    public int DefaultWeaponEnsureAttempts;

    [ViewVariables]
    public bool PrimaryWeaponInstallInProgress;

    [ViewVariables]
    public bool SecondaryWeaponInstallInProgress;

    [ViewVariables]
    public TimeSpan NextStepStunCheckAfter;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CombatMechWeaponComponent : Component
{
    [DataField]
    public float FiringArc = 150f;

    // Must match the weapon_{armState}_{left/right} states in the RX47 visualizer.
    [DataField(required: true)]
    public string ArmState = string.Empty;

    [AutoNetworkedField]
    public EntityUid? LinkedMech;
}

[RegisterComponent]
public sealed partial class CombatMechUnderbarrelComponent : Component;

[RegisterComponent]
public sealed partial class CombatMechMeleeDamageMultiplierComponent : Component
{
    [DataField(required: true)]
    public float Multiplier;
}

[RegisterComponent]
public sealed partial class CombatMechBumpDamageableComponent : Component;

[RegisterComponent]
public sealed partial class CombatMechWeaponFlamerTankComponent : Component
{
    [DataField]
    public string WeaponTankContainerId = CombatMechContainerIds.FlamerTank;

    [DataField]
    public string LocalTankContainerId = CombatMechContainerIds.GunMagazine;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class InsideCombatVehicleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    // Snapshot of mob-level components captured on seal-up and restored on dismount.
    // Not [DataField]: pre-set values would make Restore think it owns components it never took.
    [ViewVariables]
    public bool RemovedInfectable;

    [ViewVariables]
    public Dictionary<Sex, SoundSpecifier>? InfectableSound;

    [ViewVariables]
    public bool AddedUnparalyzable;

    [ViewVariables]
    public bool RemovedExplosionStun;

    [ViewVariables]
    public bool ExplosionStunWeak;

    [ViewVariables]
    public TimeSpan ExplosionStunBlindTime;

    [ViewVariables]
    public TimeSpan ExplosionStunBlurTime;

    [ViewVariables]
    public bool RemovedAffectableByWeeds;

    [ViewVariables]
    public bool OnXenoWeeds;

    [ViewVariables]
    public bool OnFriendlyWeeds;

    [ViewVariables]
    public bool OnXenoSlowResin;

    [ViewVariables]
    public bool OnXenoFastResin;

    [ViewVariables]
    public bool CollisionDisabled;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> OpenFaceplateDamageAt = new();

    [ViewVariables]
    public Dictionary<string, CombatMechFixtureCollisionState> Fixtures = new();
}

[DataDefinition, Serializable, NetSerializable]
public readonly partial record struct CombatMechFixtureCollisionState
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Mask { get; init; }

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Layer { get; init; }

    public CombatMechFixtureCollisionState(int mask, int layer)
    {
        Mask = mask;
        Layer = layer;
    }
}

[Serializable, NetSerializable]
public enum WeaponSlot : byte
{
    Primary,
    Secondary,
}

[Serializable, NetSerializable]
public enum CombatMechVisuals : byte
{
    HelmetClosed,
    PrimaryWeapon,
    SecondaryWeapon,
    MarkingsColor,
    MarkingsSpecialty,
    HasTowLauncher,
}

[Serializable, NetSerializable]
public enum CombatMechVisualLayers : byte
{
    Legs,
    Body,
    Helmet,
    Arms,
    PrimaryWeapon,
    SecondaryWeapon,
    MarkingsColor,
    MarkingsSpecialty,
    TowLauncher,
}

[Serializable, NetSerializable]
public sealed partial class CombatMechInstallWeaponDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public WeaponSlot Slot;

    public override DoAfterEvent Clone() => new CombatMechInstallWeaponDoAfterEvent { Slot = Slot };

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is CombatMechInstallWeaponDoAfterEvent install && install.Slot == Slot;
    }
}

[Serializable, NetSerializable]
public sealed partial class CombatMechDetachWeaponDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public WeaponSlot Slot;

    public override DoAfterEvent Clone() => new CombatMechDetachWeaponDoAfterEvent { Slot = Slot };

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is CombatMechDetachWeaponDoAfterEvent detach && detach.Slot == Slot;
    }
}

[Serializable, NetSerializable]
public sealed partial class CombatMechForceEjectDoAfterEvent : SimpleDoAfterEvent;
