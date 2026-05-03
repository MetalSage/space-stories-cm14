using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.StatusEffect;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.CombatMech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CombatMechComponent : Component
{
    public const string EmptyWeaponState = "empty";
    public const string UnderbarrelSlot = "rmc-aslot-underbarrel";
    public const string GunMagazineContainerId = "gun_magazine";
    public const string GunChamberContainerId = "gun_chamber";
    public const string WeaponTankContainerId = "rx47_flamer_tank";

    [DataField(required: true)]
    public EntProtoId PrimaryWeapon;

    [DataField(required: true)]
    public EntProtoId SecondaryWeapon;

    [DataField, AutoNetworkedField]
    public string PrimaryWeaponState = EmptyWeaponState;

    [DataField, AutoNetworkedField]
    public string SecondaryWeaponState = EmptyWeaponState;

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
    public TimeSpan WeaponInstallDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan WeaponDetachDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ForceEjectDelay = TimeSpan.FromSeconds(8);

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
    public float StepStunOverlapRatio = 0.2f;

    [DataField]
    public TimeSpan StepActiveDuration = TimeSpan.FromSeconds(0.4);

    [DataField]
    public float BarricadeCollisionDamage = 900f;

    [DataField]
    public float BarricadeBumperRange = 0.9f;

    [DataField]
    public TimeSpan BarricadeBumperCooldown = TimeSpan.FromSeconds(0.25);

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
    public SoundSpecifier? EnterSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_powerloader_step.ogg");

    [DataField]
    public SoundSpecifier? DamageAlertSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    [DataField, AutoNetworkedField]
    public EntityUid? PrimaryWeaponEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? SecondaryWeaponEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? PilotEntity;

    [ViewVariables]
    public bool DamageAlert25;

    [ViewVariables]
    public bool DamageAlert10;

    [ViewVariables]
    public TimeSpan NextBarricadeBumpAt;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> NextStepStunAt = new();

    [ViewVariables]
    public TimeSpan LastStepMoveAt;

    [ViewVariables]
    public bool DefaultWeaponEnsureQueued;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CombatMechWeaponComponent : Component
{
    [DataField]
    public float FiringArc = 150f;

    /// <summary>
    /// Must match weapon_{armState}_{left/right} states in the RX47 visualizer.
    /// </summary>
    [DataField(required: true)]
    public string ArmState = string.Empty;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedMech;
}

[RegisterComponent]
public sealed partial class CombatMechUnderbarrelComponent : Component;

[RegisterComponent]
public sealed partial class CombatMechWeaponFlamerTankComponent : Component
{
    [DataField]
    public string WeaponTankContainerId = CombatMechComponent.WeaponTankContainerId;

    [DataField]
    public string LocalTankContainerId = CombatMechComponent.GunMagazineContainerId;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class InsideCombatVehicleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    [DataField]
    [ViewVariables]
    public bool RemovedInfectable;

    [DataField]
    [ViewVariables]
    public bool AddedUnparalyzable;

    [DataField]
    [ViewVariables]
    public bool RemovedExplosionStun;

    [DataField]
    [ViewVariables]
    public bool AddedTurnInvisible;

    [DataField]
    [ViewVariables]
    public bool AddedActiveInvisible;

    [DataField]
    [ViewVariables]
    public bool RemovedAffectableByWeeds;

    [DataField]
    [ViewVariables]
    public bool CollisionDisabled;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> OpenFaceplateDamageAt = new();

    [ViewVariables]
    public Dictionary<string, int> FixtureMasks = new();

    [ViewVariables]
    public Dictionary<string, int> FixtureLayers = new();
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
    public bool Primary;

    public override DoAfterEvent Clone() => new CombatMechInstallWeaponDoAfterEvent { Primary = Primary };
}

[Serializable, NetSerializable]
public sealed partial class CombatMechDetachWeaponDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool Primary;

    public override DoAfterEvent Clone() => new CombatMechDetachWeaponDoAfterEvent { Primary = Primary };
}

[Serializable, NetSerializable]
public sealed partial class CombatMechForceEjectDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class CombatMechUnderbarrelShootEvent : EntityEventArgs
{
    public NetCoordinates Coordinates { get; init; }
    public NetEntity? Weapon { get; init; }
    public NetEntity? Target { get; init; }

    public CombatMechUnderbarrelShootEvent()
    {
    }

    public CombatMechUnderbarrelShootEvent(NetCoordinates coordinates, NetEntity? weapon, NetEntity? target)
    {
        Coordinates = coordinates;
        Weapon = weapon;
        Target = target;
    }
}
