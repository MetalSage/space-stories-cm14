using System.Numerics;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared._RMC14.Xenonids.Paralyzing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.CombatMech;

public sealed partial class CombatMechSystem : EntitySystem
{
    private const string UnderbarrelSlot = "rmc-aslot-underbarrel";
    private const float ProtectionCleanupInterval = 0.25f;

    private static readonly HashSet<string> ProtectedStatusEffects = new()
    {
        "Blinded",
        "Dazed",
        "Drunk",
        "Flashed",
        "KnockedDown",
        "SlowedDown",
        "Stun",
    };

    private float _protectionCleanupAccumulator;
    private readonly HashSet<EntityUid> _contacts = new();
    private readonly HashSet<Entity<BarricadeComponent>> _barricades = new();
    private readonly HashSet<EntityUid> _forceEjectingPilots = new();
    private readonly List<EntityUid> _staleDictionaryKeys = new();

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StandingStateSystem _standingState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
#pragma warning disable CS0618 // Existing status protection still uses the legacy status effect system.
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
#pragma warning restore CS0618
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CombatMechComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CombatMechComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<CombatMechComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<CombatMechComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<CombatMechComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<CombatMechComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CombatMechComponent, GetIFFGunUserEvent>(OnGetIFFGunUser);
        SubscribeLocalEvent<CombatMechComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CombatMechComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CombatMechComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CombatMechComponent, DropAttemptEvent>(OnMechDropAttempt);
        SubscribeLocalEvent<CombatMechComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CombatMechComponent, GetIgnitionImmunityEvent>(OnMechIgnitionImmunity);
        SubscribeLocalEvent<CombatMechComponent, RMCGetFireImmunityEvent>(OnMechFireImmunity);
        SubscribeLocalEvent<CombatMechComponent, PickupAttemptEvent>(OnMechPickupAttempt);
        SubscribeLocalEvent<CombatMechComponent, StartCollideEvent>(OnMechStartCollide);
        SubscribeLocalEvent<CombatMechComponent, MoveEvent>(OnMechMove);
        SubscribeLocalEvent<CombatMechComponent, AttemptMobTargetCollideEvent>(OnMechAttemptMobTargetCollide);
        SubscribeLocalEvent<CombatMechComponent, GetSpeedModifierContactCapEvent>(OnMechGetSpeedModifierContactCap);
        SubscribeLocalEvent<CombatMechComponent, TileFrictionEvent>(OnMechTileFriction);
        SubscribeLocalEvent<CombatMechComponent, CombatMechInstallWeaponDoAfterEvent>(OnInstallWeaponDoAfter);
        SubscribeLocalEvent<CombatMechComponent, CombatMechDetachWeaponDoAfterEvent>(OnDetachWeaponDoAfter);
        SubscribeLocalEvent<CombatMechComponent, CombatMechForceEjectDoAfterEvent>(OnForceEjectDoAfter);
        SubscribeAllEvent<CombatMechUnderbarrelShootEvent>(OnCombatMechUnderbarrelShoot);

        SubscribeLocalEvent<CombatMechWeaponComponent, AttemptShootEvent>(OnWeaponAttemptShoot, before: [typeof(SharedRMCFlamerSystem)]);
        SubscribeLocalEvent<CombatMechWeaponComponent, GetIFFGunUserEvent>(OnWeaponGetIFFGunUser);
        SubscribeLocalEvent<CombatMechWeaponComponent, ContainerIsRemovingAttemptEvent>(OnWeaponContainerRemoveAttempt);
        SubscribeLocalEvent<CombatMechWeaponComponent, InteractUsingEvent>(OnWeaponInteractUsing);
        SubscribeLocalEvent<CombatMechWeaponComponent, ItemSlotEjectAttemptEvent>(OnWeaponItemSlotEjectAttempt);
        SubscribeLocalEvent<CombatMechWeaponComponent, RMCTryAmmoEjectEvent>(OnWeaponTryAmmoEject);
        SubscribeLocalEvent<CombatMechWeaponComponent, UseInHandEvent>(OnWeaponUseInHand);
        SubscribeLocalEvent<CombatMechWeaponComponent, GetVerbsEvent<AlternativeVerb>>(OnWeaponGetAlternativeVerbs);
        SubscribeLocalEvent<CombatMechUnderbarrelComponent, AttemptShootEvent>(OnMountedAttachableAttemptShoot, before: [typeof(SharedRMCFlamerSystem)]);
        SubscribeLocalEvent<CombatMechWeaponFlamerTankComponent, AttemptShootEvent>(OnWeaponFlamerAttemptShoot, before: [typeof(SharedRMCFlamerSystem)]);
        SubscribeLocalEvent<CombatMechWeaponFlamerTankComponent, GetAmmoCountEvent>(OnWeaponFlamerGetAmmoCount, before: [typeof(SharedRMCFlamerSystem)]);
        SubscribeLocalEvent<CombatMechWeaponFlamerTankComponent, GunShotEvent>(OnWeaponFlamerGunShot);
        SubscribeLocalEvent<RMCCameraShakingComponent, ComponentStartup>(OnCameraShakeStartup);
        SubscribeLocalEvent<InsideCombatVehicleComponent, AttackAttemptEvent>(OnInsideVehicleAttackAttempt);
        SubscribeLocalEvent<InsideCombatVehicleComponent, BeforeAttemptShootEvent>(OnInsideVehicleBeforeAttemptShoot);
        SubscribeLocalEvent<InsideCombatVehicleComponent, BeforeDamageChangedEvent>(OnInsideVehicleBeforeDamage);
        SubscribeLocalEvent<InsideCombatVehicleComponent, BeforeStatusEffectAddedEvent>(OnInsideVehicleBeforeStatusEffectAdded);
        SubscribeLocalEvent<InsideCombatVehicleComponent, CorrodingEvent>(OnInsideVehicleCorroding);
        SubscribeLocalEvent<InsideCombatVehicleComponent, DazedEvent>(OnInsideVehicleDazed);
        SubscribeLocalEvent<InsideCombatVehicleComponent, GetSpeedModifierContactCapEvent>(OnInsideVehicleGetSpeedModifierContactCap);
        SubscribeLocalEvent<InsideCombatVehicleComponent, KnockedDownEvent>(OnInsideVehicleKnockedDown);
        SubscribeLocalEvent<InsideCombatVehicleComponent, NeurotoxinInjectAttemptEvent>(OnInsideVehicleNeurotoxinInjectAttempt);
        SubscribeLocalEvent<InsideCombatVehicleComponent, StunnedEvent>(OnInsideVehicleStunned);
        SubscribeLocalEvent<InsideCombatVehicleComponent, GetIgnitionImmunityEvent>(OnInsideVehicleIgnitionImmunity);
        SubscribeLocalEvent<InsideCombatVehicleComponent, RMCGetFireImmunityEvent>(OnInsideVehicleFireImmunity);
        SubscribeLocalEvent<InsideCombatVehicleComponent, GetExplosionResistanceEvent>(OnInsideVehicleExplosionResistance);
        SubscribeLocalEvent<InsideCombatVehicleComponent, DropAttemptEvent>(OnInsideVehicleDropAttempt);
        SubscribeLocalEvent<InsideCombatVehicleComponent, PickupAttemptEvent>(OnInsideVehiclePickupAttempt);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        _protectionCleanupAccumulator += frameTime;
        var cleanupProtection = _protectionCleanupAccumulator >= ProtectionCleanupInterval;
        if (cleanupProtection)
            _protectionCleanupAccumulator = 0f;

        if (cleanupProtection)
        {
            ProcessBarricadeBumpers();
            ProcessMarineStepStuns();
            ProcessOpenFaceplateDamageOverTime();
        }

        var query = EntityQueryEnumerator<InsideCombatVehicleComponent>();
        while (query.MoveNext(out var uid, out var inside))
        {
            if (Deleted(inside.Vehicle))
            {
                RestorePilotProtection((uid, inside));
                RemCompDeferred<InsideCombatVehicleComponent>(uid);
                continue;
            }

            if (!cleanupProtection || !IsPilotSealed((uid, inside)))
                continue;

            // Most effects are blocked by events; this slower pass catches late-added components without ticking every frame.
            ClearProtectedStatuses((uid, inside));
            ClearProtectedMovementDebuffs((uid, inside));
            ClearProtectedOngoingEffects((uid, inside));
        }
    }

    private void OnMapInit(Entity<CombatMechComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);

        if (_net.IsClient)
            return;

        // GiveHands finishes after MapInit; defer one tick so the mech hand containers exist before mounting weapons.
        Timer.Spawn(0, () =>
        {
            if (Deleted(ent.Owner) || !TryComp(ent.Owner, out CombatMechComponent? mech))
                return;

            EnsureWeapon((ent.Owner, mech), true);
            EnsureWeapon((ent.Owner, mech), false);
            UpdateAppearance((ent.Owner, mech));
        });
    }

}
