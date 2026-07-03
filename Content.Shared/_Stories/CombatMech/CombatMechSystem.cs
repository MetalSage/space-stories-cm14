using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Suicide;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
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
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.CombatMech;

public sealed partial class CombatMechSystem : EntitySystem
{
    private const float ProtectionCleanupInterval = 0.25f;
    private const float PositionMoveEpsilon = 0.0001f;
    private const float DirectionEpsilon = 0.001f;
    private const float FiringTargetEpsilon = 0.01f;
    private static readonly TimeSpan StepStunMoveCheckInterval = TimeSpan.FromMilliseconds(100);
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    private float _protectionCleanupAccumulator;
    // Scratch buffers — only valid inside Update's sequential server pass.
    private readonly HashSet<EntityUid> _contacts = new();
    private readonly HashSet<Entity<DamageOverTimeComponent>> _damageContacts = new();
    private readonly HashSet<EntityUid> _bumpDamageTargets = new();
    private readonly HashSet<EntityUid> _forceEjectingPilots = new();
    private readonly List<EntityUid> _staleDictionaryKeys = new();
    private readonly List<EntityUid> _stalePilots = new();
    private readonly Queue<EntityUid> _pendingDefaultWeapons = new();
    private readonly Queue<EntityUid> _nextTickDefaultWeapons = new();
    private readonly List<EntityUid> _pilotsIterBuffer = new();
    private readonly List<EntityUid> _dotPilotsBuffer = new();

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StandingStateSystem _standingState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private EntityQuery<InputMoverComponent> _moverQuery;
    private EntityQuery<ItemComponent> _itemQuery;

    public override void Initialize()
    {
        _moverQuery = GetEntityQuery<InputMoverComponent>();
        _itemQuery = GetEntityQuery<ItemComponent>();

        SubscribeLocalEvent<CombatMechComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CombatMechComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<CombatMechComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<CombatMechComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<CombatMechComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<CombatMechComponent, AfterAutoHandleStateEvent>(OnMechState);
        SubscribeLocalEvent<CombatMechComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CombatMechComponent, GetIFFGunUserEvent>(OnGetIFFGunUser);
        SubscribeLocalEvent<CombatMechComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CombatMechComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CombatMechComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CombatMechComponent, DropAttemptEvent>(OnMechDropAttempt);
        SubscribeLocalEvent<CombatMechComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CombatMechComponent, PickupAttemptEvent>(OnMechPickupAttempt);
        SubscribeLocalEvent<CombatMechComponent, StartCollideEvent>(OnMechStartCollide);
        SubscribeLocalEvent<CombatMechComponent, PreventCollideEvent>(OnMechPreventCollide);
        SubscribeLocalEvent<CombatMechComponent, MoveEvent>(OnMechMove);
        SubscribeLocalEvent<CombatMechComponent, AttemptMobCollideEvent>(OnMechAttemptMobCollide);
        SubscribeLocalEvent<CombatMechComponent, AttemptMobTargetCollideEvent>(OnMechAttemptMobTargetCollide);
        SubscribeLocalEvent<CombatMechComponent, GetSpeedModifierContactCapEvent>(OnMechGetSpeedModifierContactCap);
        SubscribeLocalEvent<CombatMechComponent, TileFrictionEvent>(OnMechTileFriction);
        SubscribeLocalEvent<CombatMechComponent, CombatMechInstallWeaponDoAfterEvent>(OnInstallWeaponDoAfter);
        SubscribeLocalEvent<CombatMechComponent, CombatMechDetachWeaponDoAfterEvent>(OnDetachWeaponDoAfter);
        SubscribeLocalEvent<CombatMechComponent, CombatMechForceEjectDoAfterEvent>(OnForceEjectDoAfter);
        SubscribeLocalEvent<CombatMechComponent, EntityTerminatingEvent>(OnMechTerminating);

        SubscribeLocalEvent<CombatMechMeleeDamageMultiplierComponent, MeleeHitEvent>(OnCombatMechMeleeHit);

        SubscribeLocalEvent<CombatMechWeaponComponent, AttemptShootEvent>(OnWeaponAttemptShoot, before: [typeof(SharedRMCFlamerSystem)]);
        SubscribeLocalEvent<CombatMechWeaponComponent, GetIFFGunUserEvent>(OnWeaponGetIFFGunUser);
        SubscribeLocalEvent<CombatMechWeaponComponent, ContainerIsRemovingAttemptEvent>(OnWeaponContainerRemoveAttempt);
        SubscribeLocalEvent<CombatMechWeaponComponent, InteractUsingEvent>(OnWeaponInteractUsing);
        SubscribeLocalEvent<CombatMechWeaponComponent, ItemSlotEjectAttemptEvent>(OnWeaponItemSlotEjectAttempt);
        SubscribeLocalEvent<CombatMechWeaponComponent, RMCTryAmmoEjectEvent>(OnWeaponTryAmmoEject);
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
        SubscribeLocalEvent<InsideCombatVehicleComponent, AfterAutoHandleStateEvent>(OnInsideVehicleState);
        SubscribeLocalEvent<InsideCombatVehicleComponent, EntityTerminatingEvent>(OnInsideVehicleTerminating);
        SubscribeLocalEvent<InsideCombatVehicleComponent, ComponentShutdown>(OnInsideVehicleShutdown);
        SubscribeLocalEvent<InsideCombatVehicleComponent, MoveEvent>(OnInsideVehicleMove);
        SubscribeLocalEvent<InsideCombatVehicleComponent, GetEyeOffsetAttemptEvent>(OnInsideVehicleGetEyeOffsetAttempt);
        SubscribeLocalEvent<InsideCombatVehicleComponent, RMCSuicideDoAfterEvent>(
            OnInsideVehicleSuicideAttempt,
            before: [typeof(RMCSuicideSystem)]);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _forceEjectingPilots.Clear();
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        while (_pendingDefaultWeapons.TryDequeue(out var pending))
        {
            if (Deleted(pending) || !TryComp(pending, out CombatMechComponent? mech))
                continue;

            mech.DefaultWeaponEnsureQueued = false;
            mech.DefaultWeaponEnsureAttempts++;
            var oldPrimary = mech.PrimaryWeaponEntity;
            var oldSecondary = mech.SecondaryWeaponEntity;
            var oldPrimaryState = mech.PrimaryWeaponState;
            var oldSecondaryState = mech.SecondaryWeaponState;

            var primaryReady = EnsureWeapon((pending, mech), WeaponSlot.Primary);
            var secondaryReady = EnsureWeapon((pending, mech), WeaponSlot.Secondary);
            if (!primaryReady || !secondaryReady)
            {
                if (mech.DefaultWeaponEnsureAttempts < mech.DefaultWeaponEnsureMaxAttempts)
                {
                    mech.DefaultWeaponEnsureQueued = true;
                    _nextTickDefaultWeapons.Enqueue(pending);
                }
                else
                {
                    Log.Warning($"RX47 {ToPrettyString(pending)} failed to spawn default weapons after {mech.DefaultWeaponEnsureAttempts} attempts.");
                    mech.DefaultWeaponEnsureAttempts = 0;
                    mech.DefaultWeaponEnsureQueued = false;
                }
            }
            else
            {
                mech.DefaultWeaponEnsureAttempts = 0;
            }

            if (primaryReady && secondaryReady ||
                oldPrimary != mech.PrimaryWeaponEntity ||
                oldSecondary != mech.SecondaryWeaponEntity ||
                oldPrimaryState != mech.PrimaryWeaponState ||
                oldSecondaryState != mech.SecondaryWeaponState)
            {
                UpdateAppearance((pending, mech));
            }
        }

        while (_nextTickDefaultWeapons.TryDequeue(out var deferred))
            _pendingDefaultWeapons.Enqueue(deferred);

        _protectionCleanupAccumulator += frameTime;
        if (_protectionCleanupAccumulator < ProtectionCleanupInterval)
            return;

        _protectionCleanupAccumulator -= ProtectionCleanupInterval;

        ProcessBarricadeBumpers();
        ProcessMarineStepStuns();
        ProcessOpenFaceplateDamageOverTime();

        // ClearProtectedStatuses can re-enter and remove InsideCombatVehicleComponent (e.g. eject mid-cleanup),
        // so iterate a snapshot rather than the live query.
        _pilotsIterBuffer.Clear();
        var pilotQuery = EntityQueryEnumerator<InsideCombatVehicleComponent>();
        while (pilotQuery.MoveNext(out var pilotUid, out _))
            _pilotsIterBuffer.Add(pilotUid);

        _stalePilots.Clear();
        foreach (var uid in _pilotsIterBuffer)
        {
            if (!TryComp(uid, out InsideCombatVehicleComponent? inside) ||
                !HasLiveVehicle((uid, inside)))
            {
                _stalePilots.Add(uid);
                continue;
            }

            if (!IsPilotSealed((uid, inside)))
                continue;

            ClearProtectedStatuses((uid, inside));
            ClearProtectedMovementDebuffs((uid, inside));
            ClearProtectedOngoingEffects((uid, inside));
        }

        foreach (var uid in _stalePilots)
        {
            RemCompDeferred<InsideCombatVehicleComponent>(uid);
        }
    }

    private void OnMapInit(Entity<CombatMechComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
        {
            UpdateAppearance(ent);
            return;
        }

        UpdateAppearance(ent);

        if (ent.Comp.DefaultWeaponEnsureQueued)
            return;

        // GiveHands finishes after MapInit; defer one tick so hand containers exist before mounting.
        ent.Comp.DefaultWeaponEnsureQueued = true;
        _pendingDefaultWeapons.Enqueue(ent.Owner);
    }

    private void EnsureBodyOverlay(Entity<CombatMechComponent> ent)
    {
        var proto = ent.Comp.BodyOverlayPrototype;
        if (string.IsNullOrEmpty(proto.Id))
            return;

        var slot = _container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.BodyOverlayContainerId, out var alreadyExisted);

        // ContainerSlot field writes do not dirty the manager; re-apply and dirty explicitly so
        // PVS does not ship engine defaults (ShowContents=false) and hide the overlay on clients.
        if (!alreadyExisted || slot.ShowContents != true || slot.OccludesLight != false)
        {
            slot.ShowContents = true;
            slot.OccludesLight = false;
            Dirty(ent.Owner, Comp<ContainerManagerComponent>(ent.Owner));
        }

        if (ent.Comp.BodyOverlayEntity is { } existing && !Deleted(existing))
        {
            if (slot.ContainedEntity == existing)
                return;

            QueueDel(existing);
        }

        var overlay = Spawn(proto, Transform(ent.Owner).Coordinates);
        if (!_container.Insert(overlay, slot))
        {
            QueueDel(overlay);
            return;
        }

        ent.Comp.BodyOverlayEntity = overlay;
        DirtyField(ent.Owner, ent.Comp, nameof(CombatMechComponent.BodyOverlayEntity));
        UpdateVisualStack(ent);
    }

}
