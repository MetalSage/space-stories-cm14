using System.Numerics;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared._RMC14.Xenonids.Paralyzing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
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
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._Stories.CombatMech;

public sealed class CombatMechSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedXenoAcidSystem _xenoAcid = default!;

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
        SubscribeLocalEvent<CombatMechComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CombatMechComponent, GetIgnitionImmunityEvent>(OnMechIgnitionImmunity);
        SubscribeLocalEvent<CombatMechComponent, RMCGetFireImmunityEvent>(OnMechFireImmunity);
        SubscribeLocalEvent<CombatMechComponent, CorrodingEvent>(OnMechCorroding);
        SubscribeLocalEvent<CombatMechComponent, CombatMechInstallWeaponDoAfterEvent>(OnInstallWeaponDoAfter);
        SubscribeLocalEvent<CombatMechComponent, CombatMechDetachWeaponDoAfterEvent>(OnDetachWeaponDoAfter);
        SubscribeLocalEvent<CombatMechComponent, CombatMechForceEjectDoAfterEvent>(OnForceEjectDoAfter);

        SubscribeLocalEvent<CombatMechWeaponComponent, AttemptShootEvent>(OnWeaponAttemptShoot);
        SubscribeLocalEvent<CombatMechWeaponComponent, ContainerIsRemovingAttemptEvent>(OnWeaponContainerRemoveAttempt);
        SubscribeLocalEvent<CombatMechWeaponComponent, InteractUsingEvent>(OnWeaponInteractUsing);
        SubscribeLocalEvent<CombatMechWeaponComponent, ItemSlotEjectAttemptEvent>(OnWeaponItemSlotEjectAttempt);
        SubscribeLocalEvent<CombatMechWeaponComponent, RMCTryAmmoEjectEvent>(OnWeaponTryAmmoEject);
        SubscribeLocalEvent<CombatMechWeaponComponent, UseInHandEvent>(OnWeaponUseInHand);
        SubscribeLocalEvent<CombatMechWeaponComponent, GetVerbsEvent<AlternativeVerb>>(OnWeaponGetAlternativeVerbs);
        SubscribeLocalEvent<RMCCameraShakingComponent, ComponentStartup>(OnCameraShakeStartup);
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

        var query = EntityQueryEnumerator<InsideCombatVehicleComponent>();
        while (query.MoveNext(out var uid, out var inside))
        {
            if (Deleted(inside.Vehicle))
            {
                RestorePilotProtection((uid, inside));
                RemCompDeferred<InsideCombatVehicleComponent>(uid);
                continue;
            }

            if (!IsProtectedPilot((uid, inside)))
                continue;

            ClearProtectedStatuses((uid, inside));
            ClearProtectedSlowOrStatus(uid);
            ClearProtectedOngoingEffects((uid, inside));
        }

        var mechQuery = EntityQueryEnumerator<CombatMechComponent>();
        while (mechQuery.MoveNext(out var uid, out _))
        {
            if (HasComp<FlammableComponent>(uid))
                _flammable.Extinguish(uid);

            if (_xenoAcid.IsMelted(uid))
                _xenoAcid.RemoveAcid(uid);
        }
    }

    private void OnMapInit(Entity<CombatMechComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);

        if (_net.IsClient)
            return;

        EnsureWeapon(ent, true);
        EnsureWeapon(ent, false);
        UpdateAppearance(ent);
    }

    private void OnStrapAttempt(Entity<CombatMechComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.User is not { } user)
            return;

        if (!_skills.HasSkill(user, ent.Comp.WeaponSkill, ent.Comp.WeaponSkillRequired))
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("stories-rx47-not-trained-pilot"), ent, user, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        if (GetWeapon(ent, true) == null || GetWeapon(ent, false) == null)
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("stories-rx47-missing-weapons"), ent, user, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        if (!TryComp(user, out HandsComponent? hands) || _hands.CountFreeHands((user, hands)) < 2)
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("stories-rx47-need-both-hands"), ent, user, PopupType.MediumCaution);
            args.Cancelled = true;
        }
    }

    private void OnUnstrapAttempt(Entity<CombatMechComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.HelmetClosed)
            return;

        if (args.User != args.Buckle.Owner)
            return;

        if (args.Popup)
            _popup.PopupClient(Loc.GetString("stories-rx47-faceplate-blocks-exit"), ent, args.Buckle.Owner, PopupType.MediumCaution);

        args.Cancelled = true;
    }

    private void OnStrapped(Entity<CombatMechComponent> ent, ref StrappedEvent args)
    {
        var pilot = args.Buckle.Owner;

        var inside = EnsureComp<InsideCombatVehicleComponent>(pilot);
        inside.Vehicle = ent;
        Dirty(pilot, inside);
        UpdatePilotProtection((pilot, inside));

        _mover.SetRelay(pilot, ent);
        var relay = EnsureComp<InteractionRelayComponent>(pilot);
        _interaction.SetRelay(pilot, ent, relay);

        _audio.PlayPredicted(ent.Comp.EnterSound, ent, pilot);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (_net.IsServer)
        {
            TransferWeaponToPilot(ent, pilot, true);
            TransferWeaponToPilot(ent, pilot, false);
        }

        UpdateAppearance(ent);
    }

    private void OnUnstrapped(Entity<CombatMechComponent> ent, ref UnstrappedEvent args)
    {
        var pilot = args.Buckle.Owner;

        if (TryComp(pilot, out InsideCombatVehicleComponent? inside))
            RestorePilotProtection((pilot, inside));

        RemCompDeferred<InsideCombatVehicleComponent>(pilot);
        RemComp<RelayInputMoverComponent>(pilot);
        RemComp<MovementRelayTargetComponent>(ent);
        RemCompDeferred<InteractionRelayComponent>(pilot);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (_net.IsServer)
        {
            TransferWeaponToMech(ent, pilot, true);
            TransferWeaponToMech(ent, pilot, false);
        }

        _audio.PlayPredicted(ent.Comp.EnterSound, ent, pilot);
        UpdateAppearance(ent);
    }

    private void TransferWeaponToPilot(Entity<CombatMechComponent> ent, EntityUid pilot, bool primary)
    {
        if (GetWeapon(ent, primary) is not { } weapon)
            return;

        if (!TryComp(pilot, out HandsComponent? pilotHands))
            return;

        var hand = FindHand(pilot, pilotHands, primary ? HandLocation.Left : HandLocation.Right);
        if (hand == null)
            return;

        if (_hands.IsHolding(ent.Owner, weapon))
            _hands.TryDrop(ent.Owner, weapon, Transform(pilot).Coordinates, checkActionBlocker: false, doDropInteraction: false);

        LinkWeaponToMech(weapon, ent);

        RemComp<UnremoveableComponent>(weapon);
        if (_hands.TryPickup(pilot, weapon, hand, checkActionBlocker: false, animate: false, handsComp: pilotHands))
            EnsureComp<UnremoveableComponent>(weapon);
    }

    private void TransferWeaponToMech(Entity<CombatMechComponent> ent, EntityUid pilot, bool primary)
    {
        if (GetWeapon(ent, primary) is not { } weapon)
            return;

        RemComp<UnremoveableComponent>(weapon);

        if (_hands.IsHolding(pilot, weapon))
            _hands.TryDrop(pilot, weapon, Transform(ent).Coordinates, checkActionBlocker: false, doDropInteraction: false);

        if (!TryComp(ent.Owner, out HandsComponent? mechHands))
            return;

        var hand = FindHand(ent.Owner, mechHands, primary ? HandLocation.Left : HandLocation.Right);
        if (hand == null)
            return;

        LinkWeaponToMech(weapon, ent);

        if (_hands.TryPickup(ent.Owner, weapon, hand, checkActionBlocker: false, animate: false, handsComp: mechHands))
            EnsureComp<UnremoveableComponent>(weapon);
    }

    private void LinkWeaponToMech(EntityUid weapon, Entity<CombatMechComponent> mech)
    {
        if (!TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return;

        weaponComp.LinkedMech = mech;
        Dirty(weapon, weaponComp);
    }

    private void OnGetIFFGunUser(Entity<CombatMechComponent> ent, ref GetIFFGunUserEvent args)
    {
        args.GunUser ??= GetPilot(ent);
    }

    private void OnExamined(Entity<CombatMechComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp(ent, out DamageableComponent? damageable))
            return;

        var total = damageable.TotalDamage.Float();
        var max = ent.Comp.MaxHealth;
        var health = Math.Clamp(100f - total / max * 100f, 0f, 100f);
        args.PushMarkup(Loc.GetString("stories-rx47-examine-health", ("health", (int) health)));

        if (GetPilot(ent) is { } pilot)
            args.PushMarkup(Loc.GetString("stories-rx47-examine-pilot", ("pilot", pilot)));
    }

    private void OnDamageChanged(Entity<CombatMechComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Damageable.TotalDamage <= 0)
            return;

        var max = ent.Comp.MaxHealth;
        var health = Math.Clamp(100f - args.Damageable.TotalDamage.Float() / max * 100f, 0f, 100f);
        var pilot = GetPilot(ent);
        if (pilot == null)
            return;

        if (health <= 10f && !ent.Comp.DamageAlert10)
        {
            ent.Comp.DamageAlert10 = true;
            Dirty(ent);
            _audio.PlayPredicted(ent.Comp.DamageAlertSound, ent, pilot.Value);
            _popup.PopupClient(Loc.GetString("stories-rx47-alert-critical"), ent, pilot.Value, PopupType.LargeCaution);
            return;
        }

        if (health <= 25f && !ent.Comp.DamageAlert25)
        {
            ent.Comp.DamageAlert25 = true;
            Dirty(ent);
            _audio.PlayPredicted(ent.Comp.DamageAlertSound, ent, pilot.Value);
            _popup.PopupClient(Loc.GetString("stories-rx47-alert-damaged"), ent, pilot.Value, PopupType.MediumCaution);
        }
    }

    private void OnInteractUsing(Entity<CombatMechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CombatMechWeaponComponent>(args.Used))
            return;

        if (GetPilot(ent) != null)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        if (ent.Comp.PrimaryWeaponEntity == null || Deleted(ent.Comp.PrimaryWeaponEntity.Value))
        {
            StartInstallWeapon(ent, args.User, args.Used, true);
            return;
        }

        if (ent.Comp.SecondaryWeaponEntity == null || Deleted(ent.Comp.SecondaryWeaponEntity.Value))
        {
            StartInstallWeapon(ent, args.User, args.Used, false);
            return;
        }

        _popup.PopupClient(Loc.GetString("stories-rx47-weapon-slots-full"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnGetAlternativeVerbs(Entity<CombatMechComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var pilot = GetPilot(ent);

        if (pilot == user)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ent.Comp.HelmetClosed
                    ? "stories-rx47-verb-open-faceplate"
                    : "stories-rx47-verb-close-faceplate"),
                Act = () =>
                {
                    if (_net.IsClient)
                        return;

                    ent.Comp.HelmetClosed = !ent.Comp.HelmetClosed;
                    Dirty(ent);
                    UpdateAppearance(ent);
                    if (TryComp(user, out InsideCombatVehicleComponent? inside))
                        UpdatePilotProtection((user, inside));

                    var msg = ent.Comp.HelmetClosed
                        ? "stories-rx47-faceplate-closed"
                        : "stories-rx47-faceplate-opened";
                    _popup.PopupEntity(Loc.GetString(msg), ent, user);
                },
                Priority = 80,
            });
        }

        if (pilot == null && TryGetHeldMechWeapon(user, out var held))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-install-left-weapon"),
                Act = () => StartInstallWeapon(ent, user, held, true),
                Priority = 70,
            });

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-install-right-weapon"),
                Act = () => StartInstallWeapon(ent, user, held, false),
                Priority = 69,
            });
        }

        if (pilot == null && GetWeapon(ent, true) is { } primary)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-detach-left-weapon", ("weapon", primary)),
                Act = () => StartDetachWeapon(ent, user, true),
                Priority = 60,
            });
        }

        if (pilot == null && GetWeapon(ent, false) is { } secondary)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-detach-right-weapon", ("weapon", secondary)),
                Act = () => StartDetachWeapon(ent, user, false),
                Priority = 59,
            });
        }

        if (pilot != null && pilot != user)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-force-eject"),
                Act = () => StartForceEject(ent, user),
                Priority = 50,
            });
        }
    }

    private void OnInstallWeaponDoAfter(Entity<CombatMechComponent> ent, ref CombatMechInstallWeaponDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used == null)
            return;

        args.Handled = true;
        InstallWeapon(ent, args.User, args.Used.Value, args.Primary);
    }

    private void OnDetachWeaponDoAfter(Entity<CombatMechComponent> ent, ref CombatMechDetachWeaponDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        DetachWeapon(ent, args.User, args.Primary);
    }

    private void OnForceEjectDoAfter(Entity<CombatMechComponent> ent, ref CombatMechForceEjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (GetPilot(ent) is not { } pilot || !TryComp(pilot, out BuckleComponent? buckle))
            return;

        _buckle.TryUnbuckle(pilot, args.User, buckle, popup: false);
    }

    private void StartInstallWeapon(Entity<CombatMechComponent> mech, EntityUid user, EntityUid weapon, bool primary)
    {
        if (!CanModifyWeapons(mech, user) || !TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return;

        if (weaponComp.LinkedMech != null && !Deleted(weaponComp.LinkedMech.Value))
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-already-linked"), mech, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CombatMechInstallWeaponDoAfterEvent { Primary = primary };
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.WeaponInstallDelay, ev, mech, mech, used: weapon)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
            _popup.PopupPredicted(Loc.GetString("stories-rx47-weapon-install-start-self", ("slot", slot)),
                Loc.GetString("stories-rx47-weapon-install-start-others", ("user", user), ("slot", slot)),
                user,
                user);
        }
    }

    private void StartDetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, bool primary)
    {
        if (!CanModifyWeapons(mech, user))
            return;

        if (GetWeapon(mech, primary) == null)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-slot-empty"), mech, user, PopupType.MediumCaution);
            return;
        }

        if (_hands.CountFreeHands(user) <= 0)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-need-free-hand"), mech, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CombatMechDetachWeaponDoAfterEvent { Primary = primary };
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.WeaponDetachDelay, ev, mech, mech)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
            _popup.PopupPredicted(Loc.GetString("stories-rx47-weapon-detach-start-self", ("slot", slot)),
                Loc.GetString("stories-rx47-weapon-detach-start-others", ("user", user), ("slot", slot)),
                user,
                user);
        }
    }

    private void StartForceEject(Entity<CombatMechComponent> mech, EntityUid user)
    {
        var ev = new CombatMechForceEjectDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.ForceEjectDelay, ev, mech, mech)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupPredicted(Loc.GetString("stories-rx47-force-eject-start-self"),
                Loc.GetString("stories-rx47-force-eject-start-others", ("user", user)),
                user,
                user);
        }
    }

    private bool InstallWeapon(Entity<CombatMechComponent> mech, EntityUid user, EntityUid weapon, bool primary)
    {
        if (!CanModifyWeapons(mech, user) || !TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return false;

        if (weaponComp.LinkedMech != null && !Deleted(weaponComp.LinkedMech.Value))
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-already-linked"), mech, user, PopupType.MediumCaution);
            return false;
        }

        DetachWeapon(mech, user, primary, false);

        if (_hands.IsHolding(user, weapon))
            _hands.TryDrop(user, weapon, Transform(mech).Coordinates, checkActionBlocker: false, doDropInteraction: false);

        SetWeapon(mech, primary, weapon);

        weaponComp.LinkedMech = mech;
        Dirty(weapon, weaponComp);

        if (TryComp(mech, out HandsComponent? hands))
        {
            var hand = FindHand(mech, hands, primary ? HandLocation.Left : HandLocation.Right);
            if (hand != null && _hands.TryPickup(mech, weapon, hand, checkActionBlocker: false, animate: false, handsComp: hands))
                EnsureComp<UnremoveableComponent>(weapon);
        }

        UpdateAppearance(mech);

        var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
        _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-installed", ("weapon", weapon), ("slot", slot)), mech, user);
        return true;
    }

    private bool DetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, bool primary, bool pickup = true)
    {
        if (GetWeapon(mech, primary) is not { } weapon)
            return false;

        RemComp<UnremoveableComponent>(weapon);

        if (TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
        {
            weaponComp.LinkedMech = null;
            Dirty(weapon, weaponComp);
        }

        if (_hands.IsHolding(mech.Owner, weapon))
            _hands.TryDrop(mech.Owner, weapon, Transform(mech).Coordinates, checkActionBlocker: false, doDropInteraction: false);
        else
            _transform.SetCoordinates(weapon, Transform(mech).Coordinates);

        SetWeapon(mech, primary, null);

        if (pickup)
            _hands.TryPickup(user, weapon, checkActionBlocker: false, animate: false);

        UpdateAppearance(mech);

        if (pickup)
        {
            var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
            _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-detached", ("weapon", weapon), ("slot", slot)), mech, user);
        }

        return true;
    }

    private void OnWeaponGetAlternativeVerbs(Entity<CombatMechWeaponComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (!TryComp(user, out InsideCombatVehicleComponent? inside) ||
            Deleted(inside.Vehicle) ||
            !TryComp(inside.Vehicle, out CombatMechComponent? mech) ||
            !IsMountedWeapon((inside.Vehicle, mech), ent.Owner))
        {
            return;
        }

        const string underbarrel = "rmc-aslot-underbarrel";
        if (!HasComp<AttachableHolderComponent>(ent.Owner) ||
            !_container.TryGetContainer(ent.Owner, underbarrel, out var container))
        {
            return;
        }

        foreach (var attachable in container.ContainedEntities)
        {
            if (!TryComp(attachable, out AttachableToggleableComponent? toggleable))
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = toggleable.ActionName,
                IconEntity = GetNetEntity(attachable),
                Act = () =>
                {
                    var ev = new AttachableToggleStartedEvent(ent.Owner, user, underbarrel);
                    RaiseLocalEvent(attachable, ref ev);
                },
                Priority = 90,
            });

            return;
        }
    }

    private void OnWeaponAttemptShoot(Entity<CombatMechWeaponComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryResolveWeaponMech(ent, args.User, out var mech))
        {
            if (_net.IsClient &&
                TryComp(args.User, out InsideCombatVehicleComponent? inside) &&
                !Deleted(inside.Vehicle))
            {
                return;
            }

            ClearWeaponMechLink(ent);
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-not-linked");
            return;
        }

        if (args.User != mech.Owner &&
            (!TryComp(args.User, out InsideCombatVehicleComponent? pilot) || pilot.Vehicle != mech.Owner))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-not-linked");
            return;
        }

        if (!InFiringArc(mech.Owner, ent.Comp.FiringArc, args.ToCoordinates))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-out-of-arc");
        }
    }

    private void OnWeaponContainerRemoveAttempt(Entity<CombatMechWeaponComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != "gun_magazine" && args.Container.ID != "gun_chamber")
            return;

        if (ent.Comp.LinkedMech == null || Deleted(ent.Comp.LinkedMech.Value))
            return;

        args.Cancel();
    }

    private void OnWeaponInteractUsing(Entity<CombatMechWeaponComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnWeaponItemSlotEjectAttempt(Entity<CombatMechWeaponComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is not { } user || !IsMountedWeaponForPilot(user, ent))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, user, PopupType.MediumCaution);
    }

    private void OnWeaponTryAmmoEject(Entity<CombatMechWeaponComponent> ent, ref RMCTryAmmoEjectEvent args)
    {
        if (!IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnWeaponUseInHand(Entity<CombatMechWeaponComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnCameraShakeStartup(Entity<RMCCameraShakingComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent.Owner, out InsideCombatVehicleComponent? inside) && IsProtectedPilot((ent.Owner, inside)))
            RemCompDeferred<RMCCameraShakingComponent>(ent);
    }

    private void OnInsideVehicleBeforeAttemptShoot(Entity<InsideCombatVehicleComponent> ent, ref BeforeAttemptShootEvent args)
    {
        if (Deleted(ent.Comp.Vehicle))
            return;

        var rotation = _transform.GetWorldRotation(ent.Comp.Vehicle);
        var rotatedOffset = rotation.RotateVec(args.Offset);
        args.Origin = Transform(ent.Comp.Vehicle).Coordinates.Offset(rotatedOffset);
        args.Handled = true;
    }

    private void OnInsideVehicleBeforeDamage(Entity<InsideCombatVehicleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled)
            return;

        if (Deleted(ent.Comp.Vehicle))
            return;

        if (args.Damage.GetTotal() > FixedPoint2.Zero && ShouldRedirectDamageToVehicle(args))
            _damageable.TryChangeDamage(ent.Comp.Vehicle, args.Damage, origin: args.Origin, tool: args.Source);

        args.Cancelled = true;
    }

    private void OnInsideVehiclePickupAttempt(Entity<InsideCombatVehicleComponent> ent, ref PickupAttemptEvent args)
    {
        if (IsProtectedPilot(ent))
            args.Cancel();
    }

    private void OnInsideVehicleDropAttempt(Entity<InsideCombatVehicleComponent> ent, ref DropAttemptEvent args)
    {
        if (IsProtectedPilot(ent))
            args.Cancel();
    }

    private void OnInsideVehicleNeurotoxinInjectAttempt(Entity<InsideCombatVehicleComponent> ent, ref NeurotoxinInjectAttemptEvent args)
    {
        if (IsProtectedPilot(ent))
            args.Cancelled = true;
    }

    private void OnInsideVehicleCorroding(Entity<InsideCombatVehicleComponent> ent, ref CorrodingEvent args)
    {
        if (IsProtectedPilot(ent))
            args.Cancelled = true;
    }

    private void OnMechCorroding(Entity<CombatMechComponent> ent, ref CorrodingEvent args)
    {
        args.Cancelled = true;
    }

    private void OnInsideVehicleBeforeStatusEffectAdded(Entity<InsideCombatVehicleComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (!IsProtectedPilot(ent))
            return;

        switch (args.Effect.Id)
        {
            case "Blinded":
            case "Dazed":
            case "KnockedDown":
            case "SlowedDown":
            case "Stun":
                args.Cancelled = true;
                break;
        }
    }

    private void OnInsideVehicleStunned(Entity<InsideCombatVehicleComponent> ent, ref StunnedEvent args)
    {
        ClearProtectedStatuses(ent);
    }

    private void OnInsideVehicleKnockedDown(Entity<InsideCombatVehicleComponent> ent, ref KnockedDownEvent args)
    {
        ClearProtectedStatuses(ent);
    }

    private void OnInsideVehicleDazed(Entity<InsideCombatVehicleComponent> ent, ref DazedEvent args)
    {
        ClearProtectedStatuses(ent);
    }

    private void OnInsideVehicleGetSpeedModifierContactCap(Entity<InsideCombatVehicleComponent> ent, ref GetSpeedModifierContactCapEvent args)
    {
        if (IsProtectedPilot(ent))
            args.SetIfMax(1f, 1f);
    }

    private void OnMechIgnitionImmunity(Entity<CombatMechComponent> ent, ref GetIgnitionImmunityEvent args)
    {
        args.Ignite = false;
    }

    private void OnMechFireImmunity(Entity<CombatMechComponent> ent, ref RMCGetFireImmunityEvent args)
    {
        args.Ignite = false;
        args.Immune = true;
    }

    private void OnInsideVehicleIgnitionImmunity(Entity<InsideCombatVehicleComponent> ent, ref GetIgnitionImmunityEvent args)
    {
        if (IsProtectedPilot(ent))
            args.Ignite = false;
    }

    private void OnInsideVehicleFireImmunity(Entity<InsideCombatVehicleComponent> ent, ref RMCGetFireImmunityEvent args)
    {
        if (!IsProtectedPilot(ent))
            return;

        args.Ignite = false;
        args.Immune = true;
    }

    private void OnInsideVehicleExplosionResistance(Entity<InsideCombatVehicleComponent> ent, ref GetExplosionResistanceEvent args)
    {
        if (IsProtectedPilot(ent))
            args.DamageCoefficient = 0f;
    }

    private void EnsureWeapon(Entity<CombatMechComponent> mech, bool primary)
    {
        ref var weapon = ref (primary ? ref mech.Comp.PrimaryWeaponEntity : ref mech.Comp.SecondaryWeaponEntity);
        if (weapon != null && !Deleted(weapon.Value))
            return;

        var proto = primary ? mech.Comp.PrimaryWeapon : mech.Comp.SecondaryWeapon;
        var spawned = Spawn(proto, Transform(mech).Coordinates);
        weapon = spawned;

        var weaponComp = EnsureComp<CombatMechWeaponComponent>(spawned);
        weaponComp.LinkedMech = mech;
        Dirty(spawned, weaponComp);

        var state = weaponComp.ArmState;
        if (primary)
            mech.Comp.PrimaryWeaponState = $"weapon_{state}_left";
        else
            mech.Comp.SecondaryWeaponState = $"weapon_{state}_right";

        Dirty(mech);

        if (!TryComp(mech, out HandsComponent? hands))
            return;

        var hand = FindHand(mech, hands, primary ? HandLocation.Left : HandLocation.Right);
        if (hand != null && _hands.TryPickup(mech, spawned, hand, checkActionBlocker: false, animate: false, handsComp: hands))
            EnsureComp<UnremoveableComponent>(spawned);
    }

    private string? FindHand(EntityUid uid, HandsComponent hands, HandLocation location)
    {
        foreach (var hand in _hands.EnumerateHands((uid, hands)))
        {
            if (!_hands.TryGetHand(uid, hand, out var data))
                continue;

            if (data.Value.Location == location)
                return hand;
        }

        return null;
    }

    private EntityUid? GetPilot(Entity<CombatMechComponent> ent)
    {
        if (!TryComp(ent, out StrapComponent? strap))
            return null;

        foreach (var buckled in strap.BuckledEntities)
            return buckled;

        return null;
    }

    private EntityUid? GetWeapon(Entity<CombatMechComponent> ent, bool primary)
    {
        var weapon = primary ? ent.Comp.PrimaryWeaponEntity : ent.Comp.SecondaryWeaponEntity;
        if (weapon == null || Deleted(weapon.Value))
            return null;

        return weapon.Value;
    }

    private bool IsMountedWeapon(Entity<CombatMechComponent> mech, EntityUid weapon)
    {
        return GetWeapon(mech, true) == weapon || GetWeapon(mech, false) == weapon;
    }

    private bool IsMountedWeaponForPilot(EntityUid user, Entity<CombatMechWeaponComponent> weapon)
    {
        if (!TryComp(user, out InsideCombatVehicleComponent? inside) ||
            Deleted(inside.Vehicle) ||
            !TryComp(inside.Vehicle, out CombatMechComponent? mech))
        {
            return false;
        }

        return IsMountedWeapon((inside.Vehicle, mech), weapon);
    }

    private bool TryResolveWeaponMech(
        Entity<CombatMechWeaponComponent> weapon,
        EntityUid user,
        out Entity<CombatMechComponent> mech)
    {
        if (weapon.Comp.LinkedMech is { } linked &&
            !Deleted(linked) &&
            TryComp(linked, out CombatMechComponent? linkedComp) &&
            IsMountedWeapon((linked, linkedComp), weapon.Owner))
        {
            mech = (linked, linkedComp);
            return true;
        }

        if (TryComp(user, out InsideCombatVehicleComponent? inside) &&
            !Deleted(inside.Vehicle) &&
            TryComp(inside.Vehicle, out CombatMechComponent? insideComp))
        {
            if (IsMountedWeapon((inside.Vehicle, insideComp), weapon.Owner))
            {
                LinkWeaponToMech(weapon, (inside.Vehicle, insideComp));
                mech = (inside.Vehicle, insideComp);
                return true;
            }

            if (_net.IsClient && IsHolding(user, weapon.Owner))
            {
                mech = (inside.Vehicle, insideComp);
                return true;
            }
        }

        mech = default;
        return false;
    }

    private void ClearWeaponMechLink(Entity<CombatMechWeaponComponent> weapon)
    {
        if (weapon.Comp.LinkedMech == null)
            return;

        weapon.Comp.LinkedMech = null;
        Dirty(weapon);
    }

    private bool IsHolding(EntityUid user, EntityUid item)
    {
        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (held == item)
                return true;
        }

        return false;
    }

    private void SetWeapon(Entity<CombatMechComponent> mech, bool primary, EntityUid? weapon)
    {
        if (primary)
            mech.Comp.PrimaryWeaponEntity = weapon;
        else
            mech.Comp.SecondaryWeaponEntity = weapon;

        var state = string.Empty;
        if (weapon != null && TryComp(weapon.Value, out CombatMechWeaponComponent? weaponComp))
            state = $"weapon_{weaponComp.ArmState}_{(primary ? "left" : "right")}";

        if (primary)
            mech.Comp.PrimaryWeaponState = state;
        else
            mech.Comp.SecondaryWeaponState = state;

        Dirty(mech);
    }

    private bool CanModifyWeapons(Entity<CombatMechComponent> mech, EntityUid user)
    {
        if (_skills.HasSkill(user, mech.Comp.WeaponSkill, mech.Comp.WeaponSkillRequired))
            return true;

        _popup.PopupClient(Loc.GetString("stories-rx47-weapon-not-trained"), mech, user, PopupType.MediumCaution);
        return false;
    }

    private bool TryGetHeldMechWeapon(EntityUid user, out EntityUid weapon)
    {
        weapon = EntityUid.Invalid;

        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (!HasComp<CombatMechWeaponComponent>(held))
                continue;

            weapon = held;
            return true;
        }

        return false;
    }

    private void UpdateAppearance(Entity<CombatMechComponent> ent)
    {
        _appearance.SetData(ent, CombatMechVisuals.HelmetClosed, ent.Comp.HelmetClosed);
        _appearance.SetData(ent, CombatMechVisuals.PrimaryWeapon, ent.Comp.PrimaryWeaponState);
        _appearance.SetData(ent, CombatMechVisuals.SecondaryWeapon, ent.Comp.SecondaryWeaponState);
    }

    private void UpdatePilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (_net.IsClient)
            return;

        if (!IsProtectedPilot(pilot))
        {
            RestorePilotProtection(pilot);
            return;
        }

        if (TryComp(pilot, out InfectableComponent? infectable) && !infectable.BeingInfected)
        {
            RemComp<InfectableComponent>(pilot);
            pilot.Comp.RemovedInfectable = true;
        }

        if (HasComp<AffectableByWeedsComponent>(pilot))
        {
            RemComp<AffectableByWeedsComponent>(pilot);
            pilot.Comp.RemovedAffectableByWeeds = true;
        }

        if (!HasComp<UnparalyzableComponent>(pilot))
        {
            EnsureComp<UnparalyzableComponent>(pilot);
            pilot.Comp.AddedUnparalyzable = true;
        }

        ClearProtectedOngoingEffects(pilot);
        ClearProtectedStatuses(pilot);
        ClearProtectedSlowOrStatus(pilot);
        Dirty(pilot);
    }

    private void RestorePilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (_net.IsClient)
            return;

        if (pilot.Comp.RemovedInfectable && !HasComp<InfectableComponent>(pilot))
            EnsureComp<InfectableComponent>(pilot);

        if (pilot.Comp.RemovedAffectableByWeeds && !HasComp<AffectableByWeedsComponent>(pilot))
            EnsureComp<AffectableByWeedsComponent>(pilot);

        if (pilot.Comp.AddedUnparalyzable)
            RemComp<UnparalyzableComponent>(pilot);

        pilot.Comp.RemovedInfectable = false;
        pilot.Comp.RemovedAffectableByWeeds = false;
        pilot.Comp.AddedUnparalyzable = false;
        Dirty(pilot);
    }

    private bool IsProtectedPilot(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (Deleted(pilot.Comp.Vehicle))
            return false;

        return true;
    }

    private void OnRefreshSpeed(Entity<CombatMechComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp(ent, out StrapComponent? strap))
            return;

        var highestSkill = 0;
        foreach (var buckled in strap.BuckledEntities)
        {
            var skill = _skills.GetSkill(buckled, ent.Comp.WeaponSkill);
            if (skill > highestSkill)
                highestSkill = skill;
        }

        if (highestSkill <= 0)
            return;

        var delay = Math.Max(
            ent.Comp.MinimumMoveDelay,
            ent.Comp.BaseMoveDelay - ent.Comp.MoveDelayReductionPerSkill * highestSkill);

        var speed = ent.Comp.BaseMoveDelay / delay;
        args.ModifySpeed(speed, speed);
    }

    private void ClearProtectedStatuses(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!IsProtectedPilot(pilot))
            return;

        _statusEffects.TryRemoveStatusEffect(pilot, "Blinded");
        _statusEffects.TryRemoveStatusEffect(pilot, "Dazed");
        _statusEffects.TryRemoveStatusEffect(pilot, "KnockedDown");
        _statusEffects.TryRemoveStatusEffect(pilot, "SlowedDown");
        _statusEffects.TryRemoveStatusEffect(pilot, "Stun");
    }

    private void ClearProtectedOngoingEffects(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!IsProtectedPilot(pilot))
            return;

        if (HasComp<FlammableComponent>(pilot))
            _flammable.Extinguish(pilot.Owner);

        if (HasComp<NeurotoxinComponent>(pilot))
            RemCompDeferred<NeurotoxinComponent>(pilot);

        if (HasComp<RMCCameraShakingComponent>(pilot))
            RemCompDeferred<RMCCameraShakingComponent>(pilot);
    }

    private bool ShouldRedirectDamageToVehicle(BeforeDamageChangedEvent args)
    {
        if (args.Source is not { } source)
            return true;

        return !HasComp<DamageOnCollideComponent>(source) && !HasComp<DamageOverTimeComponent>(source);
    }

    private void ClearProtectedSlowOrStatus(
        EntityUid uid,
        string? status = null,
        bool removeSlow = false,
        bool removeSuperSlow = false,
        bool removeRoot = false)
    {
        if (!TryComp(uid, out InsideCombatVehicleComponent? inside) || !IsProtectedPilot((uid, inside)))
            return;

        if (status != null)
            _statusEffects.TryRemoveStatusEffect(uid, status);

        if (removeSlow || HasComp<RMCSlowdownComponent>(uid))
            RemCompDeferred<RMCSlowdownComponent>(uid);

        if (removeSuperSlow || HasComp<RMCSuperSlowdownComponent>(uid))
            RemCompDeferred<RMCSuperSlowdownComponent>(uid);

        if (removeRoot || HasComp<RMCRootedComponent>(uid))
            RemCompDeferred<RMCRootedComponent>(uid);
    }

    private bool InFiringArc(EntityUid mech, float arc, EntityCoordinates? target)
    {
        if (target == null)
            return false;

        var from = _transform.GetMapCoordinates(mech);
        var to = target.Value.ToMapPos(EntityManager, _transform);
        var diff = to - from.Position;
        if (diff.LengthSquared() < 0.01f)
            return false;

        var facing = _transform.GetWorldRotation(mech);
        var targetAngle = diff.ToWorldAngle();
        var delta = Math.Abs(Angle.ShortestDistance(facing, targetAngle).Degrees);
        return delta <= arc / 2f;
    }

}
