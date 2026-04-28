using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.CombatMech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CombatMechComponent : Component
{
    [DataField(required: true)]
    public EntProtoId PrimaryWeapon;

    [DataField(required: true)]
    public EntProtoId SecondaryWeapon;

    [DataField, AutoNetworkedField]
    public string PrimaryWeaponState = string.Empty;

    [DataField, AutoNetworkedField]
    public string SecondaryWeaponState = string.Empty;

    [DataField, AutoNetworkedField]
    public bool HelmetClosed;

    [DataField, AutoNetworkedField]
    public string MarkingsColorState = string.Empty;

    [DataField, AutoNetworkedField]
    public string MarkingsSpecialtyState = string.Empty;

    [DataField, AutoNetworkedField]
    public bool HasTowLauncher;

    [DataField]
    public float MaxHealth = 3000f;

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

    [ViewVariables]
    public bool DamageAlert25;

    [ViewVariables]
    public bool DamageAlert10;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CombatMechWeaponComponent : Component
{
    [DataField]
    public float FiringArc = 150f;

    [DataField(required: true)]
    public string ArmState = string.Empty;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedMech;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InsideCombatVehicleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    [ViewVariables]
    public bool RemovedInfectable;

    [ViewVariables]
    public bool AddedUnparalyzable;

    [ViewVariables]
    public bool RemovedExplosionStun;

    [ViewVariables]
    public bool AddedTurnInvisible;

    [ViewVariables]
    public bool AddedActiveInvisible;

    [ViewVariables]
    public bool RemovedAffectableByWeeds;

    [ViewVariables]
    public bool CollisionDisabled;

    [ViewVariables]
    public readonly Dictionary<string, int> FixtureMasks = new();

    [ViewVariables]
    public readonly Dictionary<string, int> FixtureLayers = new();
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
