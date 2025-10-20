using System.Numerics;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Mortar;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Fortify;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem
{
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly ProtoId<DamageTypePrototype> _blunt = "Blunt";

    private EntityQuery<VehicleComponent> _vehicleQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<RecentlyVehicleCollidedComponent> _recentlyCollidedQuery;
    private EntityQuery<StandingStateComponent> _standingQuery;
    private EntityQuery<MarineComponent> _marineQuery;
    private EntityQuery<InputMoverComponent> _inputMoverQuery;
    private EntityQuery<VehicleMovementComponent> _movementQuery;

    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _vehicleBlockingContacts = new();

    private void InitializeMovement()
    {
        _vehicleQuery = GetEntityQuery<VehicleComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _xenoQuery = GetEntityQuery<XenoComponent>();
        _recentlyCollidedQuery = GetEntityQuery<RecentlyVehicleCollidedComponent>();
        _standingQuery = GetEntityQuery<StandingStateComponent>();
        _marineQuery = GetEntityQuery<MarineComponent>();
        _inputMoverQuery = GetEntityQuery<InputMoverComponent>();
        _movementQuery = GetEntityQuery<VehicleMovementComponent>();

        SubscribeLocalEvent<VehicleComponent, StartCollideEvent>(OnVehicleStartCollide);
        SubscribeLocalEvent<VehicleComponent, EndCollideEvent>(OnVehicleEndCollide);
        SubscribeLocalEvent<VehicleComponent, PreventCollideEvent>(OnVehiclePreventCollide);
        SubscribeLocalEvent<VehicleComponent, MoveInputEvent>(OnVehicleMoveInput);

        SubscribeLocalEvent<VehicleMovementComponent, ComponentInit>(OnMovementInit);
        SubscribeLocalEvent<VehicleMovementComponent, ComponentShutdown>(OnMovementShutdown);
        SubscribeLocalEvent<VehicleMovementComponent, RefreshMovementSpeedModifiersEvent>(OnMovementSpeedRefresh);
        SubscribeLocalEvent<VehicleMovementComponent, MoveEvent>(OnVehicleMove);
    }

    private void OnMovementInit(Entity<VehicleMovementComponent> ent, ref ComponentInit args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnMovementShutdown(Entity<VehicleMovementComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnMovementSpeedRefresh(Entity<VehicleMovementComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.CurrentMomentum <= 0)
            return;

        var speedBonus = 1 + ent.Comp.CurrentMomentum * ent.Comp.SpeedPerMomentum;
        speedBonus = Math.Min(speedBonus, 1 + ent.Comp.MaxMomentumSpeedBonus);
        args.ModifySpeed(speedBonus, speedBonus);
    }

    private void OnVehicleMove(Entity<VehicleMovementComponent> ent, ref MoveEvent args)
    {
        if (!_inputMoverQuery.TryComp(ent, out var mover))
            return;

        if (!args.OldPosition.TryDistance(EntityManager, _transform, args.NewPosition, out var distance))
            return;

        var absDistance = Math.Abs(distance);
        if (absDistance < 0.01f)
            return;

        ent.Comp.DistanceMoved += absDistance;
        ent.Comp.LastMovementTime = _timing.CurTime;

        var currentRotation = _transform.GetWorldRotation(ent);
        var moveDir = GetMoveDirection(ent, mover);

        if (ent.Comp.LastMoveDirection != Direction.Invalid &&
            ent.Comp.LastMoveDirection != moveDir)
        {
            var momentumLoss = (int)(ent.Comp.CurrentMomentum * ent.Comp.MomentumTurnLossFactor);
            ent.Comp.CurrentMomentum = Math.Max(0, ent.Comp.CurrentMomentum - momentumLoss);
            Dirty(ent);
        }

        ent.Comp.LastMoveDirection = moveDir;
        ent.Comp.LastRotation = currentRotation;

        if (ent.Comp.DistanceMoved < ent.Comp.StepIncrement)
            return;

        ent.Comp.Steps += 1;
        ent.Comp.DistanceMoved -= ent.Comp.StepIncrement;

        if (ent.Comp.Steps < ent.Comp.MinimumStepsForMomentum)
            return;

        if (ent.Comp.CurrentMomentum < ent.Comp.MaxMomentum)
        {
            ent.Comp.CurrentMomentum++;
            ent.Comp.SoundSteps += ent.Comp.StepIncrement;

            if (ent.Comp.CurrentMomentum == 1 || ent.Comp.SoundSteps >= ent.Comp.SoundEvery)
            {
                ent.Comp.SoundSteps = 0;
                if (_timing.InSimulation && ent.Comp.MovementSound != null)
                    PlayMovementSound(ent);
            }

            Dirty(ent);
            _movement.RefreshMovementSpeedModifiers(ent);
        }
    }

    private void PlayMovementSound(Entity<VehicleMovementComponent> ent)
    {
        if (ent.Comp.MovementSound == null)
            return;

        StopMovementSound(ent.Owner);

        var sound = _audio.PlayPredicted(
            ent.Comp.MovementSound,
            ent,
            ent,
            new AudioParams
            {
                Loop = true,
                MaxDistance = 15f,
                Volume = -5f
            }
        );

        if (sound != null)
        {
            ent.Comp.AudioStream = sound.Value.Entity;
            Dirty(ent);
        }
    }

    private void StopMovementSound(EntityUid vehicle, VehicleMovementComponent? comp = null)
    {
        if (!Resolve(vehicle, ref comp) || comp.AudioStream == null)
            return;

        _audio.Stop(comp.AudioStream);
        comp.AudioStream = null;
        Dirty(vehicle, comp);
    }

    private Direction GetMoveDirection(EntityUid vehicle, InputMoverComponent mover)
    {
        var moveDir = _mover.DirVecForButtons(mover.HeldMoveButtons, (vehicle, mover));
        if (moveDir.LengthSquared() < 0.01f)
            return Direction.Invalid;

        return moveDir.GetDir();
    }

    private void OnVehiclePreventCollide(Entity<VehicleComponent> vehicle, ref PreventCollideEvent args)
    {
        if (_standingQuery.TryComp(args.OtherEntity, out var standing) && !standing.Standing || _mobState.IsIncapacitated(args.OtherEntity))
        {
            args.Cancelled = true;
            return;
        }

        if (HasComp<XenoRestingComponent>(args.OtherEntity))
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp<XenoFortifyComponent>(args.OtherEntity, out var fortify) &&
            fortify.Fortified &&
            !fortify.CanMoveFortified &&
            vehicle.Comp.Class >= VehicleClass.Light)
        {
            args.Cancelled = true;
            return;
        }

        if (HasComp<ItemComponent>(args.OtherEntity))
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp<MobStateComponent>(args.OtherEntity, out var mobState))
        {
            if (_recentlyCollidedQuery.HasComp(args.OtherEntity))
                args.Cancelled = true;

            if (!_movementQuery.TryComp(vehicle, out var movement))
            {
                args.Cancelled = false;
                return;
            }

            var momentum = movement.CurrentMomentum;
            if (momentum < 1)
            {
                args.Cancelled = false;
                return;
            }

            var time = _timing.CurTime;
            if (_recentlyCollidedQuery.TryComp(args.OtherEntity, out var recent))
            {
                if (time < recent.ExpireAt)
                {
                    args.Cancelled = true;
                    return;
                }
            }

            bool shouldPassThrough = false;

            if (_xenoQuery.HasComp(args.OtherEntity))
            {
                shouldPassThrough = HandleXenoPreventCollision(vehicle, args.OtherEntity, momentum);
            }
            else if (_marineQuery.HasComp(args.OtherEntity))
            {
                shouldPassThrough = HandleHumanPreventCollision(vehicle, args.OtherEntity, momentum);
            }
            else
            {
                shouldPassThrough = HandleMobPreventCollision(vehicle, args.OtherEntity, momentum);
            }

            if (shouldPassThrough)
            {
                if (_recentlyCollidedQuery.TryComp(args.OtherEntity, out var target))
                {
                    target.LastBumpedAt = time;
                    target.ExpireAt = time + target.BumpCooldown;
                    Dirty(args.OtherEntity, target);
                }
                else
                {
                    var collisionTarget = EnsureComp<RecentlyVehicleCollidedComponent>(args.OtherEntity);
                    collisionTarget.LastBumpedAt = time;
                    collisionTarget.ExpireAt = time + collisionTarget.BumpCooldown;
                    Dirty(args.OtherEntity, collisionTarget);
                }

                args.Cancelled = true;
            }
            else
            {
                args.Cancelled = false;
            }
            return;
        }

        args.Cancelled = false;
    }

    private bool HandleMobPreventCollision(Entity<VehicleComponent> vehicle, EntityUid mob, float momentum)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return false;

        if (_mobState.IsIncapacitated(mob))
        {
            ApplyDamage(mob, FixedPoint2.New(7 + _random.Next(0, 6)), vehicle.Owner);
            return true;
        }

        var (stunTime, damage) = GetMobCollisionEffects(vehicle.Comp.Class, momentum, movement.MaxMomentum);

        _popup.PopupEntity(
            Loc.GetString("st-vehicle-rams-mob", ("vehicle", vehicle.Owner), ("target", mob)),
            mob,
            mob,
            PopupType.LargeCaution
        );

        bool wasStunned = false;
        if (stunTime > 0)
        {
            wasStunned = _stun.TryParalyze(mob, TimeSpan.FromSeconds(stunTime), true);
        }

        if (damage > 0)
        {
            ApplyDamage(mob, damage, vehicle.Owner);
        }

        var direction = _transform.GetWorldRotation(vehicle).ToWorldVec();
        ApplyKnockback(mob, direction, momentum);

        return wasStunned;
    }

    private bool HandleHumanPreventCollision(Entity<VehicleComponent> vehicle, EntityUid human, float momentum)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return false;

        if (!TryGetDriver(vehicle, out var driver))
            return false;

        var isFriendly = false;
        if (_gunIFF.TryGetUserFaction(vehicle.Owner, out var faction) && _gunIFF.IsInFaction(human, faction))
            isFriendly = true;

        var (stunTime, damage) = GetHumanCollisionEffects(vehicle.Comp.Class, isFriendly, momentum, movement.MaxMomentum);

        _popup.PopupEntity(
            Loc.GetString("st-vehicle-rams-mob", ("vehicle", vehicle.Owner), ("target", human)),
            human,
            human,
            PopupType.LargeCaution
        );

        if (damage > 0)
        {
            if (isFriendly)
            {
                _popup.PopupEntity(
                    Loc.GetString("st-vehicle-rammed-ally"),
                    driver!.Value,
                    driver!.Value,
                    PopupType.MediumCaution
                );
            }
            ApplyDamage(human, damage, vehicle.Owner);
        }

        bool wasStunned = false;
        if (stunTime > 0)
        {
            wasStunned = _stun.TryParalyze(human, TimeSpan.FromSeconds(stunTime), true);
        }

        var direction = _transform.GetWorldRotation(vehicle).ToWorldVec();
        ApplyKnockback(human, direction, momentum);

        return wasStunned;
    }

    private bool HandleXenoPreventCollision(Entity<VehicleComponent> vehicle, EntityUid xeno, float momentum)
    {
        if (!_xenoQuery.TryComp(xeno, out var xenoComp))
            return false;

        if (!TryComp<RMCSizeComponent>(xeno, out var xenoSize))
            return false;

        if (!_movementQuery.TryComp(vehicle, out var movement))
            return false;

        var isKnockedDown = false;
        var takesDamage = false;
        var blocked = false;
        var momentumPenalty = false;

        var isIncapacitated = _mobState.IsIncapacitated(xeno);
        var isResting = HasComp<XenoRestingComponent>(xeno);
        var isFortified = TryComp<XenoFortifyComponent>(xeno, out var fortify) && fortify.Fortified;

        if (xenoSize.Size >= RMCSizes.Immobile && !isIncapacitated && !isResting)
        {
            if (vehicle.Comp.Class != VehicleClass.Heavy)
            {
                var vehicleWorldPos = _transform.GetWorldPosition(vehicle);
                var xenoWorldPos = _transform.GetWorldPosition(xeno);
                var dirBetween = (vehicleWorldPos - xenoWorldPos).Normalized();
                var xenoDir = _transform.GetWorldRotation(xeno).ToWorldVec();

                if (vehicle.Comp.Class == VehicleClass.Weak)
                {
                    blocked = true;
                }
                else if (Vector2.Dot(dirBetween, xenoDir) > 0.8f)
                {
                    blocked = true;
                }
                else if (Vector2.Dot(dirBetween, -xenoDir) > 0.8f)
                {
                    takesDamage = true;
                }
                else if (HasComp<XenoEvolutionGranterComponent>(xeno))
                {
                    blocked = true;
                }
                else
                {
                    momentumPenalty = true;
                }
            }

            if (blocked)
            {
                _popup.PopupEntity(
                    Loc.GetString("st-xeno-blocks-vehicle", ("xeno", xeno), ("vehicle", vehicle.Owner)),
                    xeno,
                    PopupType.LargeCaution
                );

                ResetMomentum((vehicle.Owner, movement));
                return false;
            }
        }

        if (vehicle.Comp.Class == VehicleClass.Weak)
        {
            momentumPenalty = true;
            if (xenoSize.Size >= RMCSizes.Big)
            {
                ApplyDamage(vehicle.Owner, FixedPoint2.New(10));
                return false;
            }
        }
        else if (vehicle.Comp.Class == VehicleClass.Light)
        {
            takesDamage = true;
            momentumPenalty = true;
        }
        else if (vehicle.Comp.Class == VehicleClass.Medium)
        {
            takesDamage = true;
        }
        else if (vehicle.Comp.Class == VehicleClass.Heavy)
        {
            isKnockedDown = true;
            takesDamage = true;
        }

        _popup.PopupEntity(
            Loc.GetString("st-vehicle-rams-xeno", ("vehicle", vehicle.Owner), ("xeno", xeno)),
            xeno,
            PopupType.LargeCaution
        );

        bool wasStunned = false;
        if (isKnockedDown)
        {
            wasStunned = _stun.TryParalyze(xeno, TimeSpan.FromSeconds(3), true);
        }

        var mobMoved = false;
        if (!isIncapacitated)
        {
            var direction = _transform.GetWorldRotation(vehicle).ToWorldVec();
            ApplyKnockback(xeno, direction, momentum);
            mobMoved = true;
        }

        if (takesDamage)
        {
            var damagePercentage = xenoComp.Tier switch
            {
                1 => 22.5f,
                2 => 18f,
                3 => 13.5f,
                _ => 10f
            };

            damagePercentage *= 0.2f;

            _mobThreshold.TryGetIncapThreshold(xeno, out var xenoHealth);

            var damage = xenoHealth!.Value * (damagePercentage / 100f);
            ApplyDamage(xeno, damage, vehicle.Owner);
        }

        if (mobMoved && momentumPenalty)
        {
            ReduceMomentum((vehicle.Owner, movement), (int)(movement.CurrentMomentum * 0.2f));
        }

        return wasStunned || isIncapacitated;
    }

    private void OnVehicleStartCollide(Entity<VehicleComponent> vehicle, ref StartCollideEvent args)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (!_physicsQuery.TryComp(vehicle, out var vehiclePhysics))
            return;

        var target = args.OtherEntity;

        var vehicleVelocity = vehiclePhysics.LinearVelocity;
        if (vehicleVelocity.LengthSquared() < 0.01f)
            return;

        if (!_inputMoverQuery.TryComp(vehicle, out var input))
            return;

        if (IsWall(target))
        {
            HandleWallCollision(vehicle, target, movement.CurrentMomentum);
            return;
        }

        if (TryComp<DoorComponent>(target, out var door))
        {
            HandleDoorCollision(vehicle, target, door, movement.CurrentMomentum);
            return;
        }

        if (_vehicleQuery.HasComp(target))
        {
            HandleVehicleCollision(vehicle, target, movement.CurrentMomentum);
            return;
        }

        if (TryComp<VehicleStructureTargetComponent>(target, out var structure))
        {
            HandleStructureCollision(vehicle, target, movement.CurrentMomentum);
            return;
        }

        if (TryComp<RMCSizeComponent>(target, out var size) &&
            size.Size == RMCSizes.Immobile &&
            !_mobState.IsIncapacitated(target))
        {
            var moveDir = _mover.DirVecForButtons(input.HeldMoveButtons, (vehicle.Owner, input));
            if (moveDir.LengthSquared() > 0f)
            {
                var vehicleWorldPos = _transform.GetWorldPosition(vehicle);
                var targetWorldPos = _transform.GetWorldPosition(target);
                var toTarget = (targetWorldPos - vehicleWorldPos).Normalized();
                var dot = Vector2.Dot(moveDir, toTarget);

                if (dot > 0.5f)
                {
                    if (!_vehicleBlockingContacts.TryGetValue(vehicle, out var set))
                        set = _vehicleBlockingContacts[vehicle] = new();

                    set.Add(target);

                    if (!movement.Blocked)
                    {
                        movement.Blocked = true;
                        Dirty(vehicle, movement);
                        _physics.ResetDynamics(vehicle.Owner, vehiclePhysics);
                    }
                }
            }
        }
    }

    private void OnVehicleEndCollide(Entity<VehicleComponent> vehicle, ref EndCollideEvent args)
    {
        var target = args.OtherEntity;
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (!_vehicleBlockingContacts.TryGetValue(vehicle, out var set))
            return;

        if (set.Remove(target) && set.Count == 0)
        {
            movement.Blocked = false;
            Dirty(vehicle, movement);
        }

        if (set.Count == 0)
            _vehicleBlockingContacts.Remove(vehicle);
    }

    private void OnVehicleMoveInput(Entity<VehicleComponent> vehicle, ref MoveInputEvent args)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (!_inputMoverQuery.TryComp(vehicle, out var input))
            return;

        var moveDir = _mover.DirVecForButtons(input.HeldMoveButtons, (vehicle.Owner, input));

        if (!movement.Blocked)
            return;

        if (!_vehicleBlockingContacts.TryGetValue(vehicle, out var set) || set.Count == 0)
        {
            movement.Blocked = false;
            Dirty(vehicle, movement);
            return;
        }

        if (moveDir.LengthSquared() == 0f)
            return;

        bool movingAway = true;
        var vehicleWorldPos = _transform.GetWorldPosition(vehicle);
        foreach (var contact in set)
        {
            if (Deleted(contact))
                continue;

            var contactWorldPos = _transform.GetWorldPosition(contact);
            var toTarget = (contactWorldPos - vehicleWorldPos).Normalized();
            var dot = Vector2.Dot(moveDir, toTarget);

            if (dot > 0.2f)
            {
                movingAway = false;
                break;
            }
        }

        if (movingAway)
        {
            movement.Blocked = false;
            Dirty(vehicle, movement);
            set.Clear();
            _vehicleBlockingContacts.Remove(vehicle);
        }
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;

        var movementQuery = EntityQueryEnumerator<VehicleMovementComponent>();
        while (movementQuery.MoveNext(out var uid, out var movement))
        {
            if (movement.CurrentMomentum <= 0)
            {
                StopMovementSound(uid);
                continue;
            }

            if (time >= movement.LastMovementTime + movement.MomentumDecayDelay)
            {
                ResetMomentum((uid, movement));
            }
        }

        var targetsQuery = EntityQueryEnumerator<RecentlyVehicleCollidedComponent>();
        while (targetsQuery.MoveNext(out var uid, out var comp))
        {
            if (time < comp.ExpireAt)
                continue;

            RemCompDeferred<RecentlyVehicleCollidedComponent>(uid);
        }
    }

    private bool IsWall(EntityUid ent)
    {
        if (!_physicsQuery.TryComp(ent, out var body))
            return false;

        return body.BodyType == BodyType.Static &&
               body.Hard &&
               (body.CollisionLayer & (int)CollisionGroup.Impassable) != 0;
    }

    private void HandleWallCollision(Entity<VehicleComponent> vehicle, EntityUid wall, float momentum)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (TryComp<CorrodibleComponent>(wall, out var corrodible) && !corrodible.IsCorrodible)
        {
            ResetMomentum((vehicle.Owner, movement));
            return;
        }

        if (vehicle.Comp.Class == VehicleClass.Weak)
        {
            ResetMomentum((vehicle.Owner, movement));
            return;
        }

        var damage = vehicle.Comp.WallRamDamage * (momentum / movement.MaxMomentum);
        ApplyDamage(wall, damage, vehicle.Owner);
        ApplyDamage(vehicle.Owner, FixedPoint2.New(10));

        if (_net.IsServer)
        {
            _popup.PopupEntity(
                Loc.GetString("st-vehicle-rams", ("vehicle", vehicle.Owner), ("target", wall)),
                wall,
                PopupType.LargeCaution
            );
        }

        ReduceMomentum((vehicle.Owner, movement), 2);
    }

    private void HandleDoorCollision(Entity<VehicleComponent> vehicle, EntityUid door, DoorComponent doorComp, float momentum)
    {
        if (TryGetDriver(vehicle, out var driver) || !HasComp<RMCPodDoorComponent>(door))
        {
            if (_access.IsAllowed(driver!.Value, door))
            {
                _door.TryOpen(door, doorComp);
                return;
            }
        }

        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (vehicle.Comp.Class == VehicleClass.Weak)
        {
            ResetMomentum((vehicle.Owner, movement));
            return;
        }

        if (_net.IsServer)
        {
            _popup.PopupEntity(
                Loc.GetString("st-vehicle-pushes-over", ("vehicle", vehicle.Owner), ("target", door)),
                door,
                PopupType.LargeCaution
            );
            _destructible.DestroyEntity(door);
        }

        ReduceMomentum((vehicle.Owner, movement), 1);
    }

    private void HandleStructureCollision(Entity<VehicleComponent> vehicle, EntityUid structure, float momentum)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (!TryComp<VehicleStructureTargetComponent>(structure, out var collidable))
            return;

        if (vehicle.Comp.Class == VehicleClass.Weak)
        {
            ResetMomentum((vehicle.Owner, movement));
            return;
        }

        if (vehicle.Comp.Class < collidable.MinimumClassToDestroy)
        {
            if (collidable.StopOnFail)
                ResetMomentum((vehicle.Owner, movement));
            else
                ReduceMomentum((vehicle.Owner, movement), (int)(movement.CurrentMomentum * collidable.MomentumLossFactor));

            ApplyDamage(vehicle.Owner, FixedPoint2.New(collidable.DamageToVehicleOnFail));

            if (collidable.DestroySound != null)
                _audio.PlayPvs(collidable.DestroySound, structure);

            return;
        }

        _popup.PopupEntity(
            Loc.GetString("st-vehicle-crushes", ("vehicle", vehicle.Owner), ("target", structure)),
            structure,
            PopupType.MediumCaution
        );

        if (HasComp<MortarComponent>(structure))
        {
            _transform.Unanchor(structure);
            return;
        }

        if (_net.IsServer)
            _destructible.DestroyEntity(structure);

        ReduceMomentum((vehicle.Owner, movement), 1);
    }

    private void HandleVehicleCollision(Entity<VehicleComponent> vehicle, EntityUid otherVehicle, float momentum)
    {
        if (!_movementQuery.TryComp(vehicle, out var movement))
            return;

        if (!_movementQuery.TryComp(otherVehicle, out var otherMovement))
            return;

        var damage = 5 * (momentum + 1);

        ApplyDamage(vehicle.Owner, FixedPoint2.New(damage));
        ApplyDamage(otherVehicle, FixedPoint2.New(damage));

        _popup.PopupEntity(
            Loc.GetString("st-vehicle-crushes-vehicle", ("vehicle", vehicle.Owner), ("target", otherVehicle)),
            otherVehicle,
            PopupType.LargeCaution
        );

        ReduceMomentum((vehicle.Owner, movement), 2);
        ReduceMomentum((otherVehicle, otherMovement), 2);
    }

    private (float stunTime, float damage) GetMobCollisionEffects(VehicleClass vehicleClass, float momentum, int maxMomentum)
    {
        var momentumRatio = momentum / maxMomentum;

        return vehicleClass switch
        {
            VehicleClass.Weak => (0.5f * momentumRatio, 0f),
            VehicleClass.Light => (1f * momentumRatio, 5 + _random.Next(0, 10)),
            VehicleClass.Medium => (2f * momentumRatio, 10 + _random.Next(0, 10)),
            VehicleClass.Heavy => (3f * momentumRatio, 15 + _random.Next(0, 10)),
            _ => (0f, 0f)
        };
    }

    private (float stunTime, float damage) GetHumanCollisionEffects(VehicleClass vehicleClass, bool isFriendly, float momentum, int maxMomentum)
    {
        var momentumRatio = momentum / maxMomentum;

        return vehicleClass switch
        {
            VehicleClass.Weak => (0.5f * momentumRatio, 0f),
            VehicleClass.Light when isFriendly => (0.5f * momentumRatio, 5 + _random.Next(0, 5)),
            VehicleClass.Light => (2f * momentumRatio, 10 + _random.Next(0, 10)),
            VehicleClass.Medium => (3f * momentumRatio, 10 + _random.Next(0, 10)),
            VehicleClass.Heavy => (5f * momentumRatio, 15 + _random.Next(0, 10)),
            _ => (0f, 0f)
        };
    }

    private void ApplyDamage(EntityUid target, FixedPoint2 amount, EntityUid? origin = null)
    {
        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict[_blunt] = amount;
        _damageable.TryChangeDamage(target, damageSpec, origin: origin);
    }

    private void ApplyKnockback(EntityUid entity, Vector2 direction, float momentum)
    {
        if (!_physicsQuery.TryComp(entity, out var physics))
            return;

        var force = direction.Normalized() * momentum * 3f;
        _physics.SetLinearVelocity(entity, force, body: physics);
    }

    private void ResetMomentum(Entity<VehicleMovementComponent> movement)
    {
        movement.Comp.CurrentMomentum = 0;
        movement.Comp.Steps = 0;
        movement.Comp.DistanceMoved = 0;
        movement.Comp.SoundSteps = 0;
        Dirty(movement);
        _movement.RefreshMovementSpeedModifiers(movement);

        StopMovementSound(movement.Owner);
    }

    private void ReduceMomentum(Entity<VehicleMovementComponent> movement, int amount)
    {
        movement.Comp.CurrentMomentum = Math.Max(0, movement.Comp.CurrentMomentum - amount);
        if (movement.Comp.CurrentMomentum == 0)
        {
            movement.Comp.Steps = 0;
            movement.Comp.SoundSteps = 0;

            StopMovementSound(movement.Owner);
        }
        Dirty(movement);
        _movement.RefreshMovementSpeedModifiers(movement);
    }
}
