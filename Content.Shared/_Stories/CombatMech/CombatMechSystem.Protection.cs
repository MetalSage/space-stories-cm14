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
using Content.Shared.Damage.Prototypes;
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
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.CombatMech;


public sealed partial class CombatMechSystem
{
    private void OnCameraShakeStartup(Entity<RMCCameraShakingComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        if (TryComp(ent.Owner, out InsideCombatVehicleComponent? inside) && IsPilotSealed((ent.Owner, inside)))
            RemCompDeferred<RMCCameraShakingComponent>(ent);
    }

    private void OnInsideVehicleAttackAttempt(Entity<InsideCombatVehicleComponent> ent, ref AttackAttemptEvent args)
    {
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
        if (args.Handled || !TryComp(ent.Comp.Vehicle, out TransformComponent? vehicleXform))
            return;

        var rotation = vehicleXform.LocalRotation;
        var rotatedOffset = rotation.RotateVec(args.Offset);
        args.Origin = vehicleXform.Coordinates.Offset(rotatedOffset);
        args.Handled = true;
    }

    private void OnInsideVehicleBeforeDamage(Entity<InsideCombatVehicleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        // Server-authoritative: a predicted client cascade would fire alert popups/sounds that get rolled back.
        if (_net.IsClient)
            return;

        if (args.Cancelled)
            return;

        if (!IsPilotSealed(ent))
            return;

        SplitForwardedDamage(args.Damage, ent.Comp.Vehicle, out var forwardedDamage, out var remainingDamage);
        if (!forwardedDamage.Empty && !Deleted(ent.Comp.Vehicle) && !_mobState.IsDead(ent.Comp.Vehicle))
        {
            _damageable.TryChangeDamage(ent.Comp.Vehicle, forwardedDamage, origin: args.Origin, tool: args.Source);
        }

        remainingDamage.TrimZeros();
        if (remainingDamage.Empty)
            args.Cancelled = true;
        else
            args.Damage = remainingDamage;
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
        if (IsPilotSealed(ent))
            args.Cancelled = true;
    }

    private void OnInsideVehicleBeforeStatusEffectAdded(Entity<InsideCombatVehicleComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (!IsPilotSealed(ent))
            return;

        if (IsProtectedStatus(ent, args.Effect))
            args.Cancelled = true;
    }

    // Legacy StatusEffectsSystem.TryAddStatusEffect does not raise BeforeStatusEffectAddedEvent,
    // so catch stun/knockdown/daze post-application and strip them.
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
        if (IsPilotSealed(ent))
            args.Ignite = false;
    }

    private void OnInsideVehicleFireImmunity(Entity<InsideCombatVehicleComponent> ent, ref RMCGetFireImmunityEvent args)
    {
        if (!IsPilotSealed(ent))
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
            return;

        if (TryComp(pilot, out InfectableComponent? infectable) && !infectable.BeingInfected)
        {
            pilot.Comp.InfectableSound = new(infectable.Sound);
            RemComp<InfectableComponent>(pilot);
            pilot.Comp.RemovedInfectable = true;
        }

        if (TryComp(pilot, out AffectableByWeedsComponent? weeds))
        {
            pilot.Comp.OnXenoWeeds = weeds.OnXenoWeeds;
            pilot.Comp.OnFriendlyWeeds = weeds.OnFriendlyWeeds;
            pilot.Comp.OnXenoSlowResin = weeds.OnXenoSlowResin;
            pilot.Comp.OnXenoFastResin = weeds.OnXenoFastResin;
            RemComp<AffectableByWeedsComponent>(pilot);
            pilot.Comp.RemovedAffectableByWeeds = true;
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
    }

    private void RestorePilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (_net.IsClient)
            return;

        if (pilot.Comp.RemovedInfectable && !HasComp<InfectableComponent>(pilot))
        {
            var infectable = EnsureComp<InfectableComponent>(pilot);
            if (pilot.Comp.InfectableSound != null)
                infectable.Sound = new(pilot.Comp.InfectableSound);
            Dirty(pilot, infectable);
        }

        if (pilot.Comp.RemovedAffectableByWeeds && !HasComp<AffectableByWeedsComponent>(pilot))
        {
            var weeds = EnsureComp<AffectableByWeedsComponent>(pilot);
#pragma warning disable RA0002
            weeds.OnXenoWeeds = pilot.Comp.OnXenoWeeds;
            weeds.OnFriendlyWeeds = pilot.Comp.OnFriendlyWeeds;
            weeds.OnXenoSlowResin = pilot.Comp.OnXenoSlowResin;
            weeds.OnXenoFastResin = pilot.Comp.OnXenoFastResin;
#pragma warning restore RA0002
            Dirty(pilot, weeds);
        }

        RestoreSealedPilotProtection(pilot);

        RestorePilotCollision(pilot);
        pilot.Comp.RemovedInfectable = false;
        pilot.Comp.InfectableSound = null;
        pilot.Comp.RemovedAffectableByWeeds = false;
        pilot.Comp.OnXenoWeeds = false;
        pilot.Comp.OnFriendlyWeeds = false;
        pilot.Comp.OnXenoSlowResin = false;
        pilot.Comp.OnXenoFastResin = false;
    }

    private void ApplySealedPilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!HasComp<UnparalyzableComponent>(pilot))
        {
            EnsureComp<UnparalyzableComponent>(pilot);
            pilot.Comp.AddedUnparalyzable = true;
        }

        if (TryComp(pilot, out StunOnExplosionReceivedComponent? explosionStun))
        {
            pilot.Comp.ExplosionStunWeak = explosionStun.Weak;
            pilot.Comp.ExplosionStunBlindTime = explosionStun.BlindTime;
            pilot.Comp.ExplosionStunBlurTime = explosionStun.BlurTime;
            RemComp<StunOnExplosionReceivedComponent>(pilot);
            pilot.Comp.RemovedExplosionStun = true;
        }
    }

    private void RestoreSealedPilotProtection(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (pilot.Comp.AddedUnparalyzable)
            RemComp<UnparalyzableComponent>(pilot);

        if (pilot.Comp.RemovedExplosionStun && !HasComp<StunOnExplosionReceivedComponent>(pilot))
        {
            var explosionStun = EnsureComp<StunOnExplosionReceivedComponent>(pilot);
#pragma warning disable RA0002
            explosionStun.Weak = pilot.Comp.ExplosionStunWeak;
            explosionStun.BlindTime = pilot.Comp.ExplosionStunBlindTime;
            explosionStun.BlurTime = pilot.Comp.ExplosionStunBlurTime;
#pragma warning restore RA0002
            Dirty(pilot, explosionStun);
        }

        pilot.Comp.AddedUnparalyzable = false;
        pilot.Comp.RemovedExplosionStun = false;
        pilot.Comp.ExplosionStunWeak = false;
        pilot.Comp.ExplosionStunBlindTime = default;
        pilot.Comp.ExplosionStunBlurTime = default;
    }

    private bool HasLiveVehicle(Entity<InsideCombatVehicleComponent> pilot)
    {
        return !Deleted(pilot.Comp.Vehicle) && !_mobState.IsDead(pilot.Comp.Vehicle);
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
            _statusEffects.TryRemoveStatusEffect(pilot, status);
        }
    }

    private void ClearProtectedOngoingEffects(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!HasLiveVehicle(pilot))
            return;

        if (HasComp<FlammableComponent>(pilot))
            _flammable.Extinguish(pilot.Owner);

        RemCompDeferred<NeurotoxinComponent>(pilot);
        RemCompDeferred<UserDamageOverTimeComponent>(pilot);
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
            pilot.Comp.Fixtures[id] = new CombatMechFixtureCollisionState(fixture.CollisionMask, fixture.CollisionLayer);
            _physics.SetCollisionMask(pilot, id, fixture, 0, fixtures);
            _physics.SetCollisionLayer(pilot, id, fixture, 0, fixtures);
        }

        pilot.Comp.CollisionDisabled = true;
    }

    private void RestorePilotCollision(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!pilot.Comp.CollisionDisabled)
            return;

        if (TryComp(pilot, out FixturesComponent? fixtures))
        {
            foreach (var (id, fixtureState) in pilot.Comp.Fixtures)
            {
                if (fixtures.Fixtures.TryGetValue(id, out var fixture))
                {
                    _physics.SetCollisionMask(pilot, id, fixture, fixtureState.Mask, fixtures);
                    _physics.SetCollisionLayer(pilot, id, fixture, fixtureState.Layer, fixtures);
                }
            }
        }

        pilot.Comp.Fixtures.Clear();
        pilot.Comp.CollisionDisabled = false;
    }

    private void ClearProtectedMovementDebuffs(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!IsPilotSealed(pilot))
            return;

        RemCompDeferred<RMCSlowdownComponent>(pilot);
        RemCompDeferred<RMCSuperSlowdownComponent>(pilot);
        RemCompDeferred<RMCRootedComponent>(pilot);
    }

    // StatusEffectNew EntProtoId is matched by string id against the legacy protected set —
    // both systems share the same id namespace.
    private bool IsProtectedStatus(Entity<InsideCombatVehicleComponent> pilot, EntProtoId status)
    {
        return TryComp(pilot.Comp.Vehicle, out CombatMechComponent? mech) &&
               mech.ProtectedStatusEffects.Contains(status.Id);
    }

    private void SplitForwardedDamage(
        DamageSpecifier damage,
        EntityUid vehicle,
        out DamageSpecifier forwarded,
        out DamageSpecifier remaining)
    {
        forwarded = new DamageSpecifier();
        remaining = new DamageSpecifier();

        if (!TryComp(vehicle, out CombatMechComponent? mech))
        {
            remaining = new DamageSpecifier(damage);
            return;
        }

        var hasContainer = TryGetDamageContainer(vehicle, out var container);
        foreach (var (type, amount) in damage.DamageDict)
        {
            if (amount == FixedPoint2.Zero)
                continue;

            // Healing (negative amounts) stays on the pilot — meds must not repair the mech instead.
            var canForward = amount > FixedPoint2.Zero &&
                             (hasContainer
                                 ? DamageContainerSupportsType(container!, type)
                                 : mech.ForwardedDamageTypes.Contains(type));

            if (canForward)
                forwarded.DamageDict[type] = amount;
            else
                remaining.DamageDict[type] = amount;
        }
    }

    private bool TryGetDamageContainer(EntityUid vehicle, out DamageContainerPrototype? container)
    {
        container = null;
        if (!TryComp(vehicle, out DamageableComponent? damageable) ||
            damageable.DamageContainerID == null)
        {
            return false;
        }

        return _proto.TryIndex<DamageContainerPrototype>(damageable.DamageContainerID, out container);
    }

    private bool DamageContainerSupportsType(DamageContainerPrototype container, string type)
    {
        if (container.SupportedTypes.Contains(type))
            return true;

        foreach (var groupId in container.SupportedGroups)
        {
            if (!_proto.TryIndex<DamageGroupPrototype>(groupId, out var group))
                continue;

            if (group.DamageTypes.Contains(type))
                return true;
        }

        return false;
    }
}
