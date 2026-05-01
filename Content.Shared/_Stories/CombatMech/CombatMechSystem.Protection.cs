using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared._RMC14.Xenonids.Paralyzing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics;

namespace Content.Shared._Stories.CombatMech;


public sealed partial class CombatMechSystem
{
    private void OnCameraShakeStartup(Entity<RMCCameraShakingComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent.Owner, out InsideCombatVehicleComponent? inside) && IsPilotSealed((ent.Owner, inside)))
            RemCompDeferred<RMCCameraShakingComponent>(ent);
    }

    private void OnInsideVehicleAttackAttempt(Entity<InsideCombatVehicleComponent> ent, ref AttackAttemptEvent args)
    {
        // Mechs fight with their mounted guns; melee swings (including AltFireMelee right-clicks) would otherwise
        // race the underbarrel shoot input and steal the click.
        if (args.Cancelled || Deleted(ent.Comp.Vehicle))
            return;

        if (args.Weapon is { } weapon &&
            TryComp(weapon, out CombatMechWeaponComponent? weaponComp) &&
            IsMountedWeaponForPilot(args.Uid, (weapon, weaponComp)))
        {
            return;
        }

        // Gun shots call CanAttack(user) without a melee weapon/target context before AttemptShoot.
        if (args.Weapon == null && args.Target == null && !args.Disarm)
            return;

        args.Cancel();
    }

    private void OnInsideVehicleBeforeAttemptShoot(Entity<InsideCombatVehicleComponent> ent, ref BeforeAttemptShootEvent args)
    {
        if (args.Handled || Deleted(ent.Comp.Vehicle))
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

        if (!IsPilotSealed(ent))
            return;

        if (TryGetForwardedDamage(args.Damage, ent.Comp.Vehicle, out var forwardedDamage))
        {
            _damageable.TryChangeDamage(ent.Comp.Vehicle, forwardedDamage, origin: args.Origin, tool: args.Source);
        }

        args.Cancelled = true;
    }

    private void OnInsideVehiclePickupAttempt(Entity<InsideCombatVehicleComponent> ent, ref PickupAttemptEvent args)
    {
        if (HasLiveVehicle(ent))
            args.Cancel();
    }

    private void OnInsideVehicleDropAttempt(Entity<InsideCombatVehicleComponent> ent, ref DropAttemptEvent args)
    {
        if (HasLiveVehicle(ent))
            args.Cancel();
    }

    private void OnInsideVehicleNeurotoxinInjectAttempt(Entity<InsideCombatVehicleComponent> ent, ref NeurotoxinInjectAttemptEvent args)
    {
        if (IsPilotSealed(ent))
            args.Cancelled = true;
    }

    private void OnInsideVehicleCorroding(Entity<InsideCombatVehicleComponent> ent, ref CorrodingEvent args)
    {
        if (HasLiveVehicle(ent))
            args.Cancelled = true;
    }

    private void OnInsideVehicleBeforeStatusEffectAdded(Entity<InsideCombatVehicleComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (!IsPilotSealed(ent))
            return;

        if (IsProtectedStatus(ent, args.Effect.Id))
            args.Cancelled = true;
    }

    private void OnInsideVehicleStunned(Entity<InsideCombatVehicleComponent> ent, ref StunnedEvent args)
    {
        if (IsPilotSealed(ent))
            ClearProtectedStatuses(ent);
    }

    private void OnInsideVehicleKnockedDown(Entity<InsideCombatVehicleComponent> ent, ref KnockedDownEvent args)
    {
        if (IsPilotSealed(ent))
            ClearProtectedStatuses(ent);
    }

    private void OnInsideVehicleDazed(Entity<InsideCombatVehicleComponent> ent, ref DazedEvent args)
    {
        if (IsPilotSealed(ent))
            ClearProtectedStatuses(ent);
    }

    private void OnInsideVehicleGetSpeedModifierContactCap(Entity<InsideCombatVehicleComponent> ent, ref GetSpeedModifierContactCapEvent args)
    {
        if (HasLiveVehicle(ent))
            args.SetIfMax(1f, 1f);
    }

    private void OnInsideVehicleIgnitionImmunity(Entity<InsideCombatVehicleComponent> ent, ref GetIgnitionImmunityEvent args)
    {
        if (HasLiveVehicle(ent))
            args.Ignite = false;
    }

    private void OnInsideVehicleFireImmunity(Entity<InsideCombatVehicleComponent> ent, ref RMCGetFireImmunityEvent args)
    {
        if (!HasLiveVehicle(ent))
            return;

        args.Ignite = false;
        args.Immune = true;
    }

    private void OnInsideVehicleExplosionResistance(Entity<InsideCombatVehicleComponent> ent, ref GetExplosionResistanceEvent args)
    {
        if (IsPilotSealed(ent))
            args.DamageCoefficient = 0f;
    }

    private void UpdatePilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (_net.IsClient)
            return;

        if (!HasLiveVehicle(pilot))
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

        if (!HasComp<EntityTurnInvisibleComponent>(pilot))
        {
            var turnInvisible = EnsureComp<EntityTurnInvisibleComponent>(pilot);
            turnInvisible.Enabled = true;
            Dirty(pilot, turnInvisible);
            pilot.Comp.AddedTurnInvisible = true;
        }

        if (!HasComp<EntityActiveInvisibleComponent>(pilot))
        {
            var activeInvisible = EnsureComp<EntityActiveInvisibleComponent>(pilot);
            activeInvisible.Opacity = 0f;
            Dirty(pilot, activeInvisible);
            pilot.Comp.AddedActiveInvisible = true;
        }

        DisablePilotCollision(pilot);
        if (IsPilotSealed(pilot))
        {
            ClearProtectedOngoingEffects(pilot);
            ApplySealedPilotProtection(pilot);
            ClearProtectedStatuses(pilot);
            ClearProtectedMovementDebuffs(pilot);
        }
        else
        {
            RestoreSealedPilotProtection(pilot);
        }

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

        RestoreSealedPilotProtection(pilot);

        if (pilot.Comp.AddedActiveInvisible)
            RemComp<EntityActiveInvisibleComponent>(pilot);

        if (pilot.Comp.AddedTurnInvisible)
            RemComp<EntityTurnInvisibleComponent>(pilot);

        RestorePilotCollision(pilot);
        pilot.Comp.RemovedInfectable = false;
        pilot.Comp.RemovedAffectableByWeeds = false;
        pilot.Comp.AddedUnparalyzable = false;
        pilot.Comp.RemovedExplosionStun = false;
        pilot.Comp.AddedTurnInvisible = false;
        pilot.Comp.AddedActiveInvisible = false;
        Dirty(pilot);
    }

    private void ApplySealedPilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!HasComp<UnparalyzableComponent>(pilot))
        {
            EnsureComp<UnparalyzableComponent>(pilot);
            pilot.Comp.AddedUnparalyzable = true;
        }

        if (HasComp<StunOnExplosionReceivedComponent>(pilot))
        {
            RemComp<StunOnExplosionReceivedComponent>(pilot);
            pilot.Comp.RemovedExplosionStun = true;
        }
    }

    private void RestoreSealedPilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (pilot.Comp.AddedUnparalyzable)
            RemComp<UnparalyzableComponent>(pilot);

        if (pilot.Comp.RemovedExplosionStun && !HasComp<StunOnExplosionReceivedComponent>(pilot))
            EnsureComp<StunOnExplosionReceivedComponent>(pilot);

        pilot.Comp.AddedUnparalyzable = false;
        pilot.Comp.RemovedExplosionStun = false;
    }

    private bool HasLiveVehicle(Entity<InsideCombatVehicleComponent> pilot)
    {
        return !Deleted(pilot.Comp.Vehicle);
    }

    private bool IsPilotSealed(Entity<InsideCombatVehicleComponent> pilot)
    {
        return HasLiveVehicle(pilot) &&
               TryComp(pilot.Comp.Vehicle, out CombatMechComponent? mech) &&
               mech.HelmetClosed;
    }

    private void ClearProtectedStatuses(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!IsPilotSealed(pilot))
            return;

        if (!TryComp(pilot.Comp.Vehicle, out CombatMechComponent? mech))
            return;

        foreach (var status in mech.ProtectedStatusEffects)
        {
#pragma warning disable CS0618 // Existing protection cleanup still uses legacy status ids.
            _statusEffects.TryRemoveStatusEffect(pilot, status);
#pragma warning restore CS0618
        }
    }

    private void ClearProtectedOngoingEffects(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!HasLiveVehicle(pilot))
            return;

        if (HasComp<FlammableComponent>(pilot))
            _flammable.Extinguish(pilot.Owner);

        if (HasComp<NeurotoxinComponent>(pilot))
            RemCompDeferred<NeurotoxinComponent>(pilot);

        if (HasComp<UserDamageOverTimeComponent>(pilot))
            RemCompDeferred<UserDamageOverTimeComponent>(pilot);

        if (HasComp<RMCCameraShakingComponent>(pilot))
            RemCompDeferred<RMCCameraShakingComponent>(pilot);
    }

    private void DisablePilotCollision(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (pilot.Comp.CollisionDisabled)
            return;

        if (!TryComp(pilot, out FixturesComponent? fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            pilot.Comp.FixtureMasks[id] = fixture.CollisionMask;
            pilot.Comp.FixtureLayers[id] = fixture.CollisionLayer;
            _physics.SetCollisionMask(pilot, id, fixture, 0, fixtures);
            _physics.SetCollisionLayer(pilot, id, fixture, 0, fixtures);
        }

        pilot.Comp.CollisionDisabled = true;
        Dirty(pilot);
    }

    private void RestorePilotCollision(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!pilot.Comp.CollisionDisabled)
            return;

        if (TryComp(pilot, out FixturesComponent? fixtures))
        {
            foreach (var (id, mask) in pilot.Comp.FixtureMasks)
            {
                if (fixtures.Fixtures.TryGetValue(id, out var fixture))
                    _physics.SetCollisionMask(pilot, id, fixture, mask, fixtures);
            }

            foreach (var (id, layer) in pilot.Comp.FixtureLayers)
            {
                if (fixtures.Fixtures.TryGetValue(id, out var fixture))
                    _physics.SetCollisionLayer(pilot, id, fixture, layer, fixtures);
            }
        }

        pilot.Comp.FixtureMasks.Clear();
        pilot.Comp.FixtureLayers.Clear();
        pilot.Comp.CollisionDisabled = false;
        Dirty(pilot);
    }

    private void ClearProtectedMovementDebuffs(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!IsPilotSealed(pilot))
            return;

        if (HasComp<RMCSlowdownComponent>(pilot))
            RemCompDeferred<RMCSlowdownComponent>(pilot);

        if (HasComp<RMCSuperSlowdownComponent>(pilot))
            RemCompDeferred<RMCSuperSlowdownComponent>(pilot);

        if (HasComp<RMCRootedComponent>(pilot))
            RemCompDeferred<RMCRootedComponent>(pilot);
    }

    private bool IsProtectedStatus(Entity<InsideCombatVehicleComponent> pilot, string status)
    {
        return TryComp(pilot.Comp.Vehicle, out CombatMechComponent? mech) &&
               mech.ProtectedStatusEffects.Contains(status);
    }

    private bool TryGetForwardedDamage(DamageSpecifier damage, EntityUid vehicle, out DamageSpecifier forwarded)
    {
        forwarded = new DamageSpecifier();

        if (!TryComp(vehicle, out CombatMechComponent? mech))
            return false;

        foreach (var type in mech.ForwardedDamageTypes)
        {
            if (damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                forwarded.DamageDict[type] = amount;
        }

        return forwarded.GetTotal() > FixedPoint2.Zero;
    }
}
