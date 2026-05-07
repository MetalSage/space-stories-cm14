using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Interaction;
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
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Shared._Stories.CombatMech;


public sealed partial class CombatMechSystem
{
    private void OnGetIFFGunUser(Entity<CombatMechComponent> ent, ref GetIFFGunUserEvent args)
    {
        args.GunUser ??= GetPilot(ent);
    }

    private void OnExamined(Entity<CombatMechComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp(ent, out DamageableComponent? damageable))
            return;

        var total = damageable.TotalDamage.Float();
        var health = GetHealthPercent(ent.Comp, total);
        if (health == null)
            return;

        args.PushMarkup(Loc.GetString("stories-rx47-examine-health", ("health", (int) health.Value)));

        if (GetPilot(ent) is { } pilot)
            args.PushMarkup(Loc.GetString("stories-rx47-examine-pilot", ("pilot", pilot)));
    }

    private void OnDamageChanged(Entity<CombatMechComponent> ent, ref DamageChangedEvent args)
    {
        if (_net.IsClient)
            return;

        var health = GetHealthPercent(ent.Comp, args.Damageable.TotalDamage.Float());
        if (health == null)
            return;
        var healthPercent = health.Value;

        // Repair events are not damage increases, but they still need to re-arm the threshold alerts.
        if (healthPercent > ent.Comp.DamagedAlertThreshold && ent.Comp.DamageAlert25)
            ent.Comp.DamageAlert25 = false;

        if (healthPercent > ent.Comp.CriticalAlertThreshold && ent.Comp.DamageAlert10)
            ent.Comp.DamageAlert10 = false;

        if (!args.DamageIncreased || args.Damageable.TotalDamage <= 0)
            return;

        FlashMechDamage(ent);

        var pilot = GetPilot(ent);
        if (pilot == null)
            return;

        // PlayEntity (not PlayPredicted) because OnDamageChanged runs server-only here - the
        // pilot has nothing to predict, so PlayPredicted would skip them as the supposed predictor.
        if (healthPercent <= ent.Comp.CriticalAlertThreshold && !ent.Comp.DamageAlert10)
        {
            ent.Comp.DamageAlert10 = true;
            _audio.PlayEntity(ent.Comp.DamageAlertSound, pilot.Value, ent);
            _popup.PopupClient(Loc.GetString("stories-rx47-alert-critical"), ent, pilot.Value, PopupType.LargeCaution);
            return;
        }

        if (healthPercent <= ent.Comp.DamagedAlertThreshold && !ent.Comp.DamageAlert25)
        {
            ent.Comp.DamageAlert25 = true;
            _audio.PlayEntity(ent.Comp.DamageAlertSound, pilot.Value, ent);
            _popup.PopupClient(Loc.GetString("stories-rx47-alert-damaged"), ent, pilot.Value, PopupType.MediumCaution);
        }
    }

    private void OnInteractUsing(Entity<CombatMechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CombatMechWeaponComponent>(args.Used))
            return;

        if (GetPilot(ent) != null)
        {
            args.Handled = true;
            PopupCannotModifyPiloted(ent, args.User);
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

    private void OnMechPickupAttempt(Entity<CombatMechComponent> ent, ref PickupAttemptEvent args)
    {
        if (GetPilot(ent) != null)
            args.Cancel();
    }

    private void OnMechDropAttempt(Entity<CombatMechComponent> ent, ref DropAttemptEvent args)
    {
        if (GetPilot(ent) != null)
            args.Cancel();
    }

    private void OnMechStartCollide(Entity<CombatMechComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient || args.OtherEntity == ent.Owner)
            return;

        if (GetPilot(ent) == null)
            return;

        TryStepStunContact(ent, args.OtherEntity, Transform(ent.Owner), _timing.CurTime);

        if (_timing.CurTime < ent.Comp.NextBarricadeBumpAt ||
            !TryDamageBumperTarget(ent, args.OtherEntity))
        {
            return;
        }

        ent.Comp.NextBarricadeBumpAt = _timing.CurTime + ent.Comp.BarricadeBumperCooldown;
    }

    private void OnMechPreventCollide(Entity<CombatMechComponent> ent, ref PreventCollideEvent args)
    {
        if (GetPilot(ent) == null)
            return;

        if (HasComp<ItemComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnMechMove(Entity<CombatMechComponent> ent, ref MoveEvent args)
    {
        if (_net.IsClient || GetPilot(ent) == null)
            return;

        // On parent change the position delta spans mixed coordinate spaces; always mark the
        // step as active instead of computing a potentially meaningless distance.
        if (!args.ParentChanged && (args.OldPosition.Position - args.NewPosition.Position).LengthSquared() < PositionMoveEpsilon)
            return;

        // Only count pilot-driven motion as a "step". Marines bumping the mech can nudge its
        // KinematicController position by physics resolution; without this gate those nudges
        // would replay the step-stun on idle bystanders.
        if (!TryComp(ent, out InputMoverComponent? mover) || !TryGetMovementDirection(mover, out _))
            return;

        ent.Comp.LastStepMoveAt = _timing.CurTime;
        TryProcessMarineStepStuns(ent, Transform(ent.Owner), _timing.CurTime);
    }

    private bool HasMovementInput(InputMoverComponent mover)
    {
        return TryGetMovementDirection(mover, out _);
    }

    private bool TryGetMovementDirection(InputMoverComponent mover, out Vector2 direction)
    {
        direction = mover.WishDir;
        if (direction.LengthSquared() <= DirectionEpsilon)
            direction = _mover.DirVecForButtons(mover.HeldMoveButtons);

        if (direction.LengthSquared() <= DirectionEpsilon)
        {
            direction = Vector2.Zero;
            return false;
        }

        direction = direction.Normalized();
        return true;
    }

    private void OnMechAttemptMobCollide(Entity<CombatMechComponent> ent, ref AttemptMobCollideEvent args)
    {
        // The RX47 applies its own step-stun/damage from actual movement. Letting the generic
        // mob-push resolver run on the kinematic mech allows idle marines to shove the mech body.
        args.Cancelled = true;
    }

    private void OnMechAttemptMobTargetCollide(Entity<CombatMechComponent> ent, ref AttemptMobTargetCollideEvent args)
    {
        if (!HasComp<MobCollisionComponent>(args.Entity) || HasComp<CombatMechComponent>(args.Entity))
            return;

        args.Cancelled = true;
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

                    SetFaceplate(ent, !ent.Comp.HelmetClosed);
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

        if (pilot != null && pilot != user && CanForceEject(ent, user))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-force-eject"),
                Act = () => StartForceEject(ent, user),
                Priority = 50,
            });
        }
    }

    private void OnForceEjectDoAfter(Entity<CombatMechComponent> ent, ref CombatMechForceEjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (GetPilot(ent) is not { } pilot || !TryComp(pilot, out BuckleComponent? buckle))
            return;

        if (!_interaction.InRangeUnobstructed(args.User, ent.Owner))
            return;

        if (!CanForceEject(ent, args.User))
            return;

        _forceEjectingPilots.Add(pilot);
        try
        {
            if (!_buckle.TryUnbuckle(pilot, args.User, buckle, popup: false))
                return;
        }
        finally
        {
            _forceEjectingPilots.Remove(pilot);
        }

        SetFaceplate(ent, false);
    }

    private void StartForceEject(Entity<CombatMechComponent> mech, EntityUid user)
    {
        // Pre-check prevents starting a doafter that would immediately cancel on completion.
        // OnForceEjectDoAfter re-checks because the user may have moved during the delay.
        if (!_interaction.InRangeUnobstructed(user, mech.Owner, popup: true))
            return;

        if (!CanForceEject(mech, user))
            return;

        var ev = new CombatMechForceEjectDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.ForceEjectDelay, ev, mech, mech)
        {
            NeedHand = true,
            BreakOnMove = true,
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
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

    private bool CanForceEject(Entity<CombatMechComponent> mech, EntityUid user)
    {
        // Event/admin rescue rule: any trained fireman can force a sealed pilot out.
        return _skills.HasSkill(user, mech.Comp.ForceEjectSkill, mech.Comp.ForceEjectSkillRequired);
    }

    private void OnMechTerminating(Entity<CombatMechComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.BodyOverlayEntity is { } overlay && !Deleted(overlay))
            QueueDel(overlay);

        if (ent.Comp.PilotEntity is not { } pilot ||
            !TryComp(pilot, out InsideCombatVehicleComponent? inside) ||
            inside.Vehicle != ent.Owner)
        {
            return;
        }

        RestorePilotProtection((pilot, inside));
        RestorePilotVisuals((pilot, inside));
        _pilotsInCombatMechs.Remove(pilot);
        RemCompDeferred<InsideCombatVehicleComponent>(pilot);
    }

    private void OnMechGetSpeedModifierContactCap(Entity<CombatMechComponent> ent, ref GetSpeedModifierContactCapEvent args)
    {
        args.SetIfMax(1f, 1f);
    }

    private void OnMechTileFriction(Entity<CombatMechComponent> ent, ref TileFrictionEvent args)
    {
        if (args.Modifier < 1f)
            args.Modifier = 1f;
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

    private EntityUid? GetPilot(Entity<CombatMechComponent> ent)
    {
        if (ent.Comp.PilotEntity is { } pilot &&
            !Deleted(pilot) &&
            TryComp(pilot, out InsideCombatVehicleComponent? inside) &&
            inside.Vehicle == ent.Owner)
        {
            return pilot;
        }

        return null;
    }

    private void UpdateAppearance(Entity<CombatMechComponent> ent)
    {
        _appearance.SetData(ent, CombatMechVisuals.PrimaryWeapon, ent.Comp.PrimaryWeaponState);
        _appearance.SetData(ent, CombatMechVisuals.SecondaryWeapon, ent.Comp.SecondaryWeaponState);
        _appearance.SetData(ent, CombatMechVisuals.MarkingsColor, ent.Comp.MarkingsColorState);
        _appearance.SetData(ent, CombatMechVisuals.MarkingsSpecialty, ent.Comp.MarkingsSpecialtyState);
        _appearance.SetData(ent, CombatMechVisuals.HasTowLauncher, ent.Comp.HasTowLauncher);
        UpdateBodyOverlayAppearance(ent);
    }

    private void UpdateBodyOverlayAppearance(Entity<CombatMechComponent> ent)
    {
        if (ent.Comp.BodyOverlayEntity is not { } overlay || Deleted(overlay))
            return;

        _appearance.SetData(overlay, CombatMechVisuals.HelmetClosed, ent.Comp.HelmetClosed);
    }

    private void FlashMechDamage(Entity<CombatMechComponent> ent)
    {
        var targets = new List<EntityUid> { ent.Owner };
        if (ent.Comp.BodyOverlayEntity is { } overlay && !Deleted(overlay))
            targets.Add(overlay);

        _colorFlash.RaiseEffect(Color.Red, targets, Filter.Pvs(ent.Owner, entityManager: EntityManager));
    }

    private void SetFaceplate(Entity<CombatMechComponent> ent, bool closed)
    {
        if (ent.Comp.HelmetClosed == closed)
            return;

        ent.Comp.HelmetClosed = closed;
        DirtyField(ent.Owner, ent.Comp, nameof(CombatMechComponent.HelmetClosed));
        UpdateAppearance(ent);

        if (GetPilot(ent) is { } pilot && TryComp(pilot, out InsideCombatVehicleComponent? inside))
            UpdatePilotProtection((pilot, inside));
    }

    private void OnRefreshSpeed(Entity<CombatMechComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (GetPilot(ent) is not { } pilot)
            return;

        var highestSkill = _skills.GetSkill(pilot, ent.Comp.WeaponSkill);

        if (highestSkill <= 0)
            return;

        var delay = Math.Max(
            ent.Comp.MinimumMoveDelay,
            ent.Comp.BaseMoveDelay - ent.Comp.MoveDelayReductionPerSkill * highestSkill);

        var speed = ent.Comp.BaseMoveDelay / delay;
        args.ModifySpeed(speed, speed);
    }

    private void ProcessBarricadeBumpers()
    {
        var query = EntityQueryEnumerator<CombatMechComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mech, out var mover, out var xform))
        {
            if (GetPilot((uid, mech)) == null || _timing.CurTime < mech.NextBarricadeBumpAt)
                continue;

            var direction = mover.WishDir;
            if (direction.LengthSquared() <= DirectionEpsilon)
                direction = _mover.DirVecForButtons(mover.HeldMoveButtons);

            if (direction.LengthSquared() <= DirectionEpsilon)
                continue;

            direction = direction.Normalized();
            var coords = _transform.GetMapCoordinates(uid, xform);
            var check = new MapCoordinates(coords.Position + direction * mech.BarricadeBumperRange, coords.MapId);

            _bumpDamageTargets.Clear();
            _lookup.GetEntitiesInRange(check.MapId, check.Position, BarricadeBumperProbeRadius, _bumpDamageTargets, LookupFlags.Uncontained);
            foreach (var target in _bumpDamageTargets)
            {
                if (!CanBumperDamageTarget(target))
                    continue;

                var targetPos = _transform.GetMapCoordinates(target).Position;
                var delta = targetPos - coords.Position;
                if (delta.LengthSquared() > PositionMoveEpsilon &&
                    Vector2.Dot(delta.Normalized(), direction) < BarricadeForwardDotMinimum)
                {
                    continue;
                }

                if (!TryDamageBumperTarget((uid, mech), target))
                    continue;

                mech.NextBarricadeBumpAt = _timing.CurTime + mech.BarricadeBumperCooldown;
                break;
            }
        }
    }

    private void ProcessMarineStepStuns()
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CombatMechComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mech, out var xform))
        {
            if (GetPilot((uid, mech)) == null)
                continue;

            PruneEntityTimeDictionary(mech.NextStepStunAt, time);

            if (time > mech.LastStepMoveAt + mech.StepActiveDuration)
                continue;

            TryProcessMarineStepStuns((uid, mech), xform, time);
        }
    }

    private void TryProcessMarineStepStuns(Entity<CombatMechComponent> mech, TransformComponent xform, TimeSpan time)
    {
        PruneEntityTimeDictionary(mech.Comp.NextStepStunAt, time);

        _contacts.Clear();
        _physics.GetContactingEntities(mech.Owner, _contacts);
        if (_contacts.Count == 0)
            return;

        var ourAabb = _lookup.GetAABBNoContainer(mech.Owner, xform.LocalPosition, xform.LocalRotation);
        var ourArea = Box2.Area(ourAabb);
        if (ourArea <= 0)
            return;

        foreach (var target in _contacts)
        {
            TryStepStunTarget(mech, target, ourAabb, ourArea, time);
        }
    }

    private bool TryStepStunContact(Entity<CombatMechComponent> mech, EntityUid target, TransformComponent xform, TimeSpan time)
    {
        if (time > mech.Comp.LastStepMoveAt + mech.Comp.StepActiveDuration)
            return false;

        var ourAabb = _lookup.GetAABBNoContainer(mech.Owner, xform.LocalPosition, xform.LocalRotation);
        var ourArea = Box2.Area(ourAabb);
        if (ourArea <= 0)
            return false;

        return TryStepStunTarget(mech, target, ourAabb, ourArea, time);
    }

    private bool TryStepStunTarget(
        Entity<CombatMechComponent> mech,
        EntityUid target,
        Box2 ourAabb,
        float ourArea,
        TimeSpan time)
    {
        if (!CanStepStunTarget(mech, target) ||
            mech.Comp.NextStepStunAt.TryGetValue(target, out var nextStepStun) && time < nextStepStun)
        {
            return false;
        }

        var targetXform = Transform(target);
        var targetAabb = _lookup.GetAABBNoContainer(target, targetXform.LocalPosition, targetXform.LocalRotation);
        if (!ourAabb.Intersects(targetAabb))
            return false;

        var intersect = Box2.Area(targetAabb.Intersect(ourAabb));
        var ratio = Math.Max(intersect / Box2.Area(targetAabb), intersect / ourArea);
        if (ratio < mech.Comp.StepStunOverlapRatio)
            return false;

        var targetEv = new AttemptMobTargetCollideEvent(mech.Owner);
        RaiseLocalEvent(target, ref targetEv);
        if (targetEv.Cancelled || !TryComp(target, out StatusEffectsComponent? status))
            return false;

        mech.Comp.NextStepStunAt[target] = time + mech.Comp.StepStunCooldown;
        _damageable.TryChangeDamage(
            target,
            CreateBluntDamage(mech.Comp.StepDamage),
            ignoreResistances: true,
            origin: mech.Owner,
            tool: mech.Owner);
        _stun.TryParalyze(target, mech.Comp.StepStunDuration, true, status, force: true);
        return true;
    }

    private bool CanStepStunTarget(Entity<CombatMechComponent> mech, EntityUid target)
    {
        return _whitelist.IsWhitelistPass(mech.Comp.StepTargetWhitelist, target) &&
               !HasComp<InsideCombatVehicleComponent>(target) &&
               !HasComp<CombatMechComponent>(target) &&
               !_mobState.IsDead(target) &&
               !_standingState.IsDown(target) &&
               HasComp<MobCollisionComponent>(target);
    }

    private void ProcessOpenFaceplateDamageOverTime()
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<InsideCombatVehicleComponent>();
        while (query.MoveNext(out var pilotUid, out var inside))
        {
            var pilot = (pilotUid, inside);
            if (!HasLiveVehicle(pilot))
                continue;

            PruneEntityTimeDictionary(inside.OpenFaceplateDamageAt, time);

            if (IsPilotSealed(pilot) ||
                _mobState.IsDead(pilotUid) ||
                _mobState.IsCritical(pilotUid))
            {
                continue;
            }

            var vehicleXform = Transform(inside.Vehicle);
            var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(vehicleXform);
            var bounds = _lookup.GetAABBNoContainer(inside.Vehicle, worldPosition, worldRotation);

            _damageContacts.Clear();
            _lookup.GetEntitiesIntersecting(vehicleXform.MapID, bounds, _damageContacts, LookupFlags.Uncontained | LookupFlags.Sensors);
            foreach (var contact in _damageContacts)
            {
                // DamageOverTime sources choose their own collision mask; acid smoke is MidImpassable via MobMask.
                if (!HasCollisionLayer(inside.Vehicle, contact.Comp.Collision) ||
                    !contact.Comp.AffectsCrit && _mobState.IsCritical(pilotUid) ||
                    !contact.Comp.AffectsDead && _mobState.IsDead(pilotUid) ||
                    !_whitelist.IsWhitelistPassOrNull(contact.Comp.Whitelist, pilotUid))
                {
                    continue;
                }

                if (inside.OpenFaceplateDamageAt.TryGetValue(contact.Owner, out var next) && time < next)
                    continue;

                inside.OpenFaceplateDamageAt[contact.Owner] = time + contact.Comp.DamageEvery;

                if (contact.Comp.Damage != null)
                    _damageable.TryChangeDamage(pilotUid, contact.Comp.Damage, origin: contact.Owner, tool: contact.Owner);

                if (contact.Comp.ArmorPiercingDamage != null)
                    _damageable.TryChangeDamage(pilotUid, contact.Comp.ArmorPiercingDamage, ignoreResistances: true, origin: contact.Owner, tool: contact.Owner);
            }
        }
    }

    private bool HasCollisionLayer(EntityUid uid, CollisionGroup layer)
    {
        if (!TryComp(uid, out FixturesComponent? fixtures))
            return false;

        var layerMask = (int)layer;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if ((fixture.CollisionLayer & layerMask) != 0)
                return true;
        }

        return false;
    }

    private void PruneEntityTimeDictionary(Dictionary<EntityUid, TimeSpan> dictionary, TimeSpan time)
    {
        if (dictionary.Count == 0)
            return;

        _staleDictionaryKeys.Clear();
        foreach (var (uid, until) in dictionary)
        {
            if (Deleted(uid) || time >= until)
                _staleDictionaryKeys.Add(uid);
        }

        foreach (var uid in _staleDictionaryKeys)
        {
            dictionary.Remove(uid);
        }

        _staleDictionaryKeys.Clear();
    }

    private bool TryDamageBumperTarget(Entity<CombatMechComponent> mech, EntityUid target)
    {
        if (!CanBumperDamageTarget(target))
            return false;

        // ignoreResistances: mech mass shoves aside barricades regardless of material/reinforcement.
        _damageable.TryChangeDamage(
            target,
            CreateBluntDamage(mech.Comp.BarricadeCollisionDamage),
            ignoreResistances: true,
            origin: mech,
            tool: mech);

        return true;
    }

    private bool CanBumperDamageTarget(EntityUid target)
    {
        return HasComp<BarricadeComponent>(target) ||
               HasComp<CombatMechBumpDamageableComponent>(target);
    }

    private static DamageSpecifier CreateBluntDamage(float amount)
    {
        return new DamageSpecifier
        {
            DamageDict = { [BluntDamageType] = FixedPoint2.New(amount) },
        };
    }

    private static float? GetHealthPercent(CombatMechComponent mech, float totalDamage)
    {
        if (mech.MaxHealth <= 0)
            return null;

        return Math.Clamp(100f - totalDamage / mech.MaxHealth * 100f, 0f, 100f);
    }
}
