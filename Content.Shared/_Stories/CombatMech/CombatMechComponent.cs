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
    public TimeSpan BarricadeBumperCooldown = TimeSpan.FromSeconds(0.25);

    [DataField]
    public Vector2 PilotVisualOffsetNorth = new(0f, 0.45f);

    [DataField]
    public Vector2 PilotVisualOffsetSouth = new(0f, 0.12f);

    [DataField]
    public Vector2 PilotVisualOffsetEastWest = new(0f, 0.28f);

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
    public HashSet<EntProtoId> ProtectedStatusEffectEntities = [];

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
    public SoundSpecifier? DamageAlertSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    [DataField, AutoNetworkedField]
    public EntityUid? PrimaryWeaponEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? SecondaryWeaponEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? PilotEntity;

    [DataField(required: true)]
    public EntProtoId? BodyOverlayPrototype;

    [DataField, AutoNetworkedField]
    public EntityUid? BodyOverlayEntity;

    [DataField]
    public int PilotRenderOrder = 1;

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

    [ViewVariables]
    public int DefaultWeaponEnsureAttempts;
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

    // Valid only within a single round; not safe across save/restart.
    [AutoNetworkedField]
    public EntityUid? LinkedMech;
}

[RegisterComponent]
public sealed partial class CombatMechUnderbarrelComponent : Component;

[RegisterComponent]
public sealed partial class CombatMechMeleeDamageMultiplierComponent : Component
{
    [DataField(required: true)]
    public float Multiplier = 1f;
}

[RegisterComponent]
public sealed partial class CombatMechBumpDamageableComponent : Component;

// Not [NetworkedComponent]: fields are static container-ID strings; actual fuel state is
// tracked through SolutionContainerManager (which is networked) on the tank entity.
[RegisterComponent]
public sealed partial class CombatMechWeaponFlamerTankComponent : Component
{
    [DataField]
    public string WeaponTankContainerId = CombatMechComponent.WeaponTankContainerId;

    // Most RX47 flamers keep the local RMCFlamerTank directly on the attachable; this is only
    // a fallback for attachables that store their local tank in a container.
    [DataField]
    public string LocalTankContainerId = CombatMechComponent.GunMagazineContainerId;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class InsideCombatVehicleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    // Runtime delta flags - not DataField: prototyping or save/restore with these
    // set would produce incorrect RestorePilotProtection behaviour.
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

    [DataField]
    [ViewVariables]
    public bool CollisionDisabled;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> OpenFaceplateDamageAt = new();

    [DataField]
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
