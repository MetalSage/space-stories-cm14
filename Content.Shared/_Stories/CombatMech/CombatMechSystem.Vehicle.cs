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
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stealth;
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
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
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
        var max = ent.Comp.MaxHealth;
        var health = Math.Clamp(100f - total / max * 100f, 0f, 100f);
        args.PushMarkup(Loc.GetString("stories-rx47-examine-health", ("health", (int) health)));

        if (GetPilot(ent) is { } pilot)
            args.PushMarkup(Loc.GetString("stories-rx47-examine-pilot", ("pilot", pilot)));
    }

    private void OnDamageChanged(Entity<CombatMechComponent> ent, ref DamageChangedEvent args)
    {
        var max = ent.Comp.MaxHealth;
        var health = Math.Clamp(100f - args.Damageable.TotalDamage.Float() / max * 100f, 0f, 100f);

        // Repair events are not damage increases, but they still need to re-arm the threshold alerts.
        if (health > 25f && ent.Comp.DamageAlert25)
        {
            ent.Comp.DamageAlert25 = false;
            Dirty(ent);
        }

        if (health > 10f && ent.Comp.DamageAlert10)
        {
            ent.Comp.DamageAlert10 = false;
            Dirty(ent);
        }

        if (!args.DamageIncreased || args.Damageable.TotalDamage <= 0)
            return;

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

        TryDamageBarricade(ent, args.OtherEntity);
    }

    private void OnMechMove(Entity<CombatMechComponent> ent, ref MoveEvent args)
    {
        if (_net.IsClient || GetPilot(ent) == null)
            return;

        if (!args.ParentChanged && (args.OldPosition.Position - args.NewPosition.Position).LengthSquared() < 0.0001f)
            return;

        ent.Comp.LastStepMoveAt = _timing.CurTime;
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

    private void OnForceEjectDoAfter(Entity<CombatMechComponent> ent, ref CombatMechForceEjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (GetPilot(ent) is not { } pilot || !TryComp(pilot, out BuckleComponent? buckle))
            return;

        SetFaceplate(ent, false);
        _buckle.TryUnbuckle(pilot, args.User, buckle, popup: false);
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
        if (!TryComp(ent, out StrapComponent? strap))
            return null;

        foreach (var buckled in strap.BuckledEntities)
            return buckled;

        return null;
    }

    private void UpdateAppearance(Entity<CombatMechComponent> ent)
    {
        _appearance.SetData(ent, CombatMechVisuals.HelmetClosed, ent.Comp.HelmetClosed);
        _appearance.SetData(ent, CombatMechVisuals.PrimaryWeapon, ent.Comp.PrimaryWeaponState);
        _appearance.SetData(ent, CombatMechVisuals.SecondaryWeapon, ent.Comp.SecondaryWeaponState);
        _appearance.SetData(ent, CombatMechVisuals.MarkingsColor, ent.Comp.MarkingsColorState);
        _appearance.SetData(ent, CombatMechVisuals.MarkingsSpecialty, ent.Comp.MarkingsSpecialtyState);
        _appearance.SetData(ent, CombatMechVisuals.HasTowLauncher, ent.Comp.HasTowLauncher);
    }

    private void SetFaceplate(Entity<CombatMechComponent> ent, bool closed)
    {
        if (ent.Comp.HelmetClosed == closed)
            return;

        ent.Comp.HelmetClosed = closed;
        Dirty(ent);
        UpdateAppearance(ent);

        if (GetPilot(ent) is { } pilot && TryComp(pilot, out InsideCombatVehicleComponent? inside))
            UpdatePilotProtection((pilot, inside));
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

    private void ProcessBarricadeBumpers()
    {
        var query = EntityQueryEnumerator<CombatMechComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mech, out var mover, out var xform))
        {
            if (GetPilot((uid, mech)) == null || _timing.CurTime < mech.NextBarricadeBumpAt)
                continue;

            var direction = mover.WishDir;
            if (direction.LengthSquared() <= 0.001f)
                direction = _mover.DirVecForButtons(mover.HeldMoveButtons);

            if (direction.LengthSquared() <= 0.001f)
                continue;

            direction = direction.Normalized();
            var coords = _transform.GetMapCoordinates(uid, xform);
            var check = new MapCoordinates(coords.Position + direction * mech.BarricadeBumperRange, coords.MapId);

            foreach (var barricade in _lookup.GetEntitiesInRange<BarricadeComponent>(check, 0.5f, LookupFlags.Uncontained))
            {
                var barricadePos = _transform.GetMapCoordinates(barricade.Owner).Position;
                if (Vector2.Dot((barricadePos - coords.Position).Normalized(), direction) < 0.35f)
                    continue;

                if (!TryDamageBarricade((uid, mech), barricade.Owner))
                    continue;

                mech.NextBarricadeBumpAt = _timing.CurTime + mech.BarricadeBumperCooldown;
                Dirty(uid, mech);
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

            PruneEntityTimeDictionary(mech.NextStepStunAt, time, removeExpired: true);

            if (time > mech.LastStepMoveAt + mech.StepActiveDuration)
                continue;

            _contacts.Clear();
            _physics.GetContactingEntities(uid, _contacts);
            if (_contacts.Count == 0)
                continue;

            var ev = new AttemptMobCollideEvent();
            RaiseLocalEvent(uid, ref ev);
            if (ev.Cancelled)
                continue;

            var ourAabb = _lookup.GetAABBNoContainer(uid, xform.LocalPosition, xform.LocalRotation);
            foreach (var target in _contacts)
            {
                if (!CanStepStunMarine(target) ||
                    mech.NextStepStunAt.TryGetValue(target, out var nextStepStun) && time < nextStepStun)
                {
                    continue;
                }

                var targetXform = Transform(target);
                var targetAabb = _lookup.GetAABBNoContainer(target, targetXform.LocalPosition, targetXform.LocalRotation);
                if (!ourAabb.Intersects(targetAabb))
                    continue;

                var intersect = Box2.Area(targetAabb.Intersect(ourAabb));
                var ratio = Math.Max(intersect / Box2.Area(targetAabb), intersect / Box2.Area(ourAabb));
                if (ratio < mech.StepStunOverlapRatio)
                    continue;

                var targetEv = new AttemptMobTargetCollideEvent();
                RaiseLocalEvent(target, ref targetEv);
                if (targetEv.Cancelled || !TryComp(target, out StatusEffectsComponent? status))
                    continue;

                mech.NextStepStunAt[target] = time + mech.StepStunCooldown;
                _damageable.TryChangeDamage(
                    target,
                    CreateBluntDamage(mech.StepDamage),
                    ignoreResistances: true,
                    origin: uid,
                    tool: uid);
                _stun.TryParalyze(target, mech.StepStunDuration, true, status, force: true);
            }
        }
    }

    private bool CanStepStunMarine(EntityUid target)
    {
        return HasComp<MarineComponent>(target) &&
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

            PruneEntityTimeDictionary(inside.OpenFaceplateDamageAt, time, removeExpired: true);

            if (IsPilotSealed(pilot) ||
                _mobState.IsDead(pilotUid) ||
                _mobState.IsCritical(pilotUid))
            {
                continue;
            }

            foreach (var contact in _physics.GetEntitiesIntersectingBody(inside.Vehicle, (int) CollisionGroup.AllMask))
            {
                if (!TryComp(contact, out DamageOverTimeComponent? damage) ||
                    !damage.AffectsCrit && _mobState.IsCritical(pilotUid) ||
                    !damage.AffectsDead && _mobState.IsDead(pilotUid))
                {
                    continue;
                }

                if (inside.OpenFaceplateDamageAt.TryGetValue(contact, out var next) && time < next)
                    continue;

                inside.OpenFaceplateDamageAt[contact] = time + damage.DamageEvery;

                if (damage.Damage != null)
                    _damageable.TryChangeDamage(pilotUid, damage.Damage, origin: contact, tool: contact);

                if (damage.ArmorPiercingDamage != null)
                    _damageable.TryChangeDamage(pilotUid, damage.ArmorPiercingDamage, ignoreResistances: true, origin: contact, tool: contact);

                break;
            }
        }
    }

    private void PruneEntityTimeDictionary(Dictionary<EntityUid, TimeSpan> dictionary, TimeSpan time, bool removeExpired)
    {
        if (dictionary.Count == 0)
            return;

        _staleDictionaryKeys.Clear();
        foreach (var (uid, until) in dictionary)
        {
            if (Deleted(uid) || removeExpired && time >= until)
                _staleDictionaryKeys.Add(uid);
        }

        foreach (var uid in _staleDictionaryKeys)
        {
            dictionary.Remove(uid);
        }

        _staleDictionaryKeys.Clear();
    }

    private bool TryDamageBarricade(Entity<CombatMechComponent> mech, EntityUid target)
    {
        if (!TryComp<BarricadeComponent>(target, out _))
            return false;

        _damageable.TryChangeDamage(
            target,
            CreateBluntDamage(mech.Comp.BarricadeCollisionDamage),
            ignoreResistances: true,
            origin: mech,
            tool: mech);

        return true;
    }

    private static DamageSpecifier CreateBluntDamage(float amount)
    {
        return new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = FixedPoint2.New(amount) },
        };
    }
}
