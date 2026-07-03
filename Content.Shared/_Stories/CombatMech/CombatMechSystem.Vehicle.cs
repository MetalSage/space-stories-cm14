using System.Numerics;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
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
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.GameStates;
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

        if (healthPercent > ent.Comp.DamagedAlertThreshold && ent.Comp.DamagedAlertTriggered)
            ent.Comp.DamagedAlertTriggered = false;

        if (healthPercent > ent.Comp.CriticalAlertThreshold && ent.Comp.CriticalAlertTriggered)
            ent.Comp.CriticalAlertTriggered = false;

        if (!args.DamageIncreased)
            return;

        var pilot = GetPilot(ent);
        if (pilot == null)
            return;

        if (healthPercent <= ent.Comp.CriticalAlertThreshold && !ent.Comp.CriticalAlertTriggered)
        {
            ent.Comp.CriticalAlertTriggered = true;
            ent.Comp.DamagedAlertTriggered = true;
            _audio.PlayEntity(ent.Comp.DamageAlertSound, pilot.Value, ent);
            // Server-only path: PopupClient is a no-op on the server.
            _popup.PopupEntity(Loc.GetString("stories-rx47-alert-critical"), ent, pilot.Value, PopupType.LargeCaution);
            return;
        }

        if (healthPercent <= ent.Comp.DamagedAlertThreshold && !ent.Comp.DamagedAlertTriggered)
        {
            ent.Comp.DamagedAlertTriggered = true;
            _audio.PlayEntity(ent.Comp.DamageAlertSound, pilot.Value, ent);
            _popup.PopupEntity(Loc.GetString("stories-rx47-alert-damaged"), ent, pilot.Value, PopupType.MediumCaution);
        }
    }

    private void OnInteractUsing(Entity<CombatMechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CombatMechWeaponComponent>(args.Used))
            return;

        if (_mobState.IsDead(ent))
        {
            args.Handled = true;
            return;
        }

        if (GetPilot(ent) != null)
        {
            args.Handled = true;
            PopupCannotModifyPiloted(ent, args.User);
            return;
        }

        args.Handled = true;

        if (GetWeapon(ent, WeaponSlot.Primary) == null)
        {
            StartInstallWeapon(ent, args.User, args.Used, WeaponSlot.Primary);
            return;
        }

        if (GetWeapon(ent, WeaponSlot.Secondary) == null)
        {
            StartInstallWeapon(ent, args.User, args.Used, WeaponSlot.Secondary);
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

        if (_itemQuery.HasComp(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnMechMove(Entity<CombatMechComponent> ent, ref MoveEvent args)
    {
        if (GetPilot(ent) is not { } pilot)
            return;

        // The buckled pilot gets no MoveEvent of its own when the mech moves or turns.
        UpdatePilotVisualOffset(pilot, ent);

        if (_net.IsClient)
            return;

        if (args.ParentChanged)
            return;

        if ((args.OldPosition.Position - args.NewPosition.Position).LengthSquared() < PositionMoveEpsilon)
            return;

        // Skip physics-resolution nudges from marines bumping the mech — those would replay the step-stun on bystanders.
        if (!_moverQuery.TryComp(ent, out var mover) || !TryGetMovementDirection(mover, out _))
            return;

        ent.Comp.LastStepMoveAt = _timing.CurTime;

        if (_timing.CurTime < ent.Comp.NextStepStunCheckAfter)
            return;

        ent.Comp.NextStepStunCheckAfter = _timing.CurTime + StepStunMoveCheckInterval;
        TryProcessMarineStepStuns(ent, Transform(ent.Owner), _timing.CurTime);
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
        args.Cancelled = true;
    }

    private void OnMechAttemptMobTargetCollide(Entity<CombatMechComponent> ent, ref AttemptMobTargetCollideEvent args)
    {
        if (HasComp<CombatMechComponent>(args.Entity))
            return;

        args.Cancelled = true;
    }

    private void OnGetAlternativeVerbs(Entity<CombatMechComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (_mobState.IsDead(ent))
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
                Act = () => StartInstallWeapon(ent, user, held, WeaponSlot.Primary),
                Priority = 70,
            });

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-install-right-weapon"),
                Act = () => StartInstallWeapon(ent, user, held, WeaponSlot.Secondary),
                Priority = 69,
            });
        }

        if (pilot == null && GetWeapon(ent, WeaponSlot.Primary) is { } primary)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-detach-left-weapon", ("weapon", Name(primary))),
                Act = () => StartDetachWeapon(ent, user, WeaponSlot.Primary),
                Priority = 60,
            });
        }

        if (pilot == null && GetWeapon(ent, WeaponSlot.Secondary) is { } secondary)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("stories-rx47-verb-detach-right-weapon", ("weapon", Name(secondary))),
                Act = () => StartDetachWeapon(ent, user, WeaponSlot.Secondary),
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
        return mech.Comp.HelmetClosed &&
               _skills.HasSkill(user, mech.Comp.ForceEjectSkill, mech.Comp.ForceEjectSkillRequired);
    }

    private void OnMechTerminating(Entity<CombatMechComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.BodyOverlayEntity is { } overlay && !Deleted(overlay))
            QueueDel(overlay);

        if (_net.IsServer)
            QueueDeleteMountedWeapons(ent);

        if (ent.Comp.PilotEntity is not { } pilot ||
            !TryComp(pilot, out InsideCombatVehicleComponent? inside) ||
            inside.Vehicle != ent.Owner)
        {
            return;
        }

        RemCompDeferred<InsideCombatVehicleComponent>(pilot);
    }

    private void QueueDeleteMountedWeapons(Entity<CombatMechComponent> ent)
    {
        var primary = ent.Comp.PrimaryWeaponEntity;
        var secondary = ent.Comp.SecondaryWeaponEntity;

        QueueDeleteMountedWeapon(primary);
        if (secondary != primary)
            QueueDeleteMountedWeapon(secondary);
    }

    private void QueueDeleteMountedWeapon(EntityUid? weapon)
    {
        if (weapon is not { } weaponUid || Deleted(weaponUid))
            return;

        QueueDel(weaponUid);
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

    private void OnMechState(Entity<CombatMechComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisualStack(ent);
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

        if (_net.IsServer)
            EnsureBodyOverlay(ent);

        UpdateVisualStack(ent);
    }

    private void UpdateVisualStack(Entity<CombatMechComponent> ent)
    {
        UpdateBodyOverlayAppearance(ent);

        if (GetVisualPilot(ent) is not { } pilot)
            return;

        ApplyPilotVisuals(pilot, ent);
    }

    private EntityUid? GetVisualPilot(Entity<CombatMechComponent> ent)
    {
        if (ent.Comp.PilotEntity is not { } pilot || Deleted(pilot))
            return null;

        if (TryComp(pilot, out InsideCombatVehicleComponent? inside))
            return inside.Vehicle == ent.Owner ? pilot : null;

        return _net.IsClient ? pilot : null;
    }

    private void UpdateBodyOverlayAppearance(Entity<CombatMechComponent> ent)
    {
        if (ent.Comp.BodyOverlayEntity is not { } overlay || Deleted(overlay))
            return;

        _appearance.SetData(overlay, CombatMechVisuals.HelmetClosed, ent.Comp.HelmetClosed);
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

            if (!TryGetMovementDirection(mover, out var direction))
                continue;

            var coords = _transform.GetMapCoordinates(uid, xform);
            var check = new MapCoordinates(coords.Position + direction * mech.BarricadeBumperRange, coords.MapId);

            _bumpDamageTargets.Clear();
            _lookup.GetEntitiesInRange(check.MapId, check.Position, mech.BarricadeBumperProbeRadius, _bumpDamageTargets, LookupFlags.Uncontained);
            foreach (var target in _bumpDamageTargets)
            {
                if (!CanBumperDamageTarget(target))
                    continue;

                var targetPos = _transform.GetMapCoordinates(target).Position;
                var delta = targetPos - coords.Position;
                if (delta.LengthSquared() > PositionMoveEpsilon &&
                    Vector2.Dot(delta.Normalized(), direction) < mech.BarricadeForwardDotMinimum)
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

        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(xform);
        var ourAabb = _lookup.GetAABBNoContainer(mech.Owner, worldPosition, worldRotation);
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

        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(xform);
        var ourAabb = _lookup.GetAABBNoContainer(mech.Owner, worldPosition, worldRotation);
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
        var (targetWorldPosition, targetWorldRotation) = _transform.GetWorldPositionRotation(targetXform);
        var targetAabb = _lookup.GetAABBNoContainer(target, targetWorldPosition, targetWorldRotation);
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
        // Fresh DamageSpecifier per hit — DamageModifyAfterResistEvent subscribers mutate DamageDict in place.
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

        // Snapshot first — TryChangeDamage below may synchronously remove InsideCombatVehicleComponent.
        _dotPilotsBuffer.Clear();
        var query = EntityQueryEnumerator<InsideCombatVehicleComponent>();
        while (query.MoveNext(out var pilotUid, out _))
            _dotPilotsBuffer.Add(pilotUid);

        foreach (var pilotUid in _dotPilotsBuffer)
        {
            if (!TryComp(pilotUid, out InsideCombatVehicleComponent? inside))
                continue;

            var pilot = (pilotUid, inside);
            if (!HasLiveVehicle(pilot) ||
                IsPilotSealed(pilot) ||
                _mobState.IsDead(pilotUid) ||
                _mobState.IsCritical(pilotUid))
            {
                continue;
            }

            PruneEntityTimeDictionary(inside.OpenFaceplateDamageAt, time);

            var vehicleXform = Transform(inside.Vehicle);
            var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(vehicleXform);
            var bounds = _lookup.GetAABBNoContainer(inside.Vehicle, worldPosition, worldRotation);

            TryComp(inside.Vehicle, out FixturesComponent? vehicleFixtures);

            _damageContacts.Clear();
            _lookup.GetEntitiesIntersecting(vehicleXform.MapID, bounds, _damageContacts, LookupFlags.Uncontained | LookupFlags.Sensors);
            foreach (var contact in _damageContacts)
            {
                if (!FixturesOverlapLayer(vehicleFixtures, contact.Comp.Collision) ||
                    (!contact.Comp.AffectsCrit && _mobState.IsCritical(pilotUid)) ||
                    (!contact.Comp.AffectsDead && _mobState.IsDead(pilotUid)) ||
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

    private static bool FixturesOverlapLayer(FixturesComponent? fixtures, CollisionGroup layer)
    {
        if (fixtures == null)
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

    public void SetMaxHealth(Entity<CombatMechComponent> ent, float maxHealth)
    {
        if (ent.Comp.MaxHealth == maxHealth)
            return;

        ent.Comp.MaxHealth = maxHealth;
        DirtyField(ent.Owner, ent.Comp, nameof(CombatMechComponent.MaxHealth));
    }
}
