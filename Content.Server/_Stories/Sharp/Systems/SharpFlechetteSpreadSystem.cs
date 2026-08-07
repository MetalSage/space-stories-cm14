using System;
using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._Stories.Sharp;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Stories.Sharp;

public sealed class SharpFlechetteSpreadSystem : EntitySystem
{
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpFlechetteSpreadComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<SharpFlechetteSpreadComponent, ProjectileFixedDistanceStopEvent>(OnFixedStop);
    }

    private void OnProjectileHit(EntityUid uid, SharpFlechetteSpreadComponent comp, ref ProjectileHitEvent args)
    {
        if (!TryTrigger(uid, comp))
            return;

        args.Handled = true;
    }

    private void OnFixedStop(EntityUid uid, SharpFlechetteSpreadComponent comp, ref ProjectileFixedDistanceStopEvent args)
    {
        TryTrigger(uid, comp);
    }

    private bool TryTrigger(EntityUid uid, SharpFlechetteSpreadComponent comp)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        if (comp.Fired)
            return false;

        comp.Fired = true;

        if (!TryComp(uid, out ProjectileComponent? sourceProjectile))
        {
            QueueDel(uid);
            return false;
        }

        var sourceCoords = Transform(uid).Coordinates;
        var direction = GetDirection(uid);
        var count = Math.Max(1, comp.Count);
        var halfSpread = comp.SpreadAngle / 2f;
        var applyDamageMultiplier = comp.SourceProjectileProto != null &&
                                    MetaData(uid).EntityPrototype?.ID == comp.SourceProjectileProto;

        for (var i = 0; i < count; i++)
        {
            float offset;
            if (count == 1)
            {
                offset = 0f;
            }
            else if (comp.EvenSpread)
            {
                var t = i / (float) (count - 1);
                offset = -halfSpread + t * comp.SpreadAngle;
            }
            else
            {
                offset = _random.NextFloat(-halfSpread, halfSpread);
            }

            var firedDirection = Angle.FromDegrees(offset).RotateVec(direction).Normalized();
            var payload = Spawn(comp.ProjectileProto, sourceCoords);
            if (applyDamageMultiplier && TryComp(payload, out ProjectileComponent? payloadProjectile))
                payloadProjectile.Damage *= comp.ProjectileDamageMultiplier;

            var extraVelocity = _random.NextVector2(comp.MinVelocity, comp.MaxVelocity);
            _gun.ShootProjectile(payload,
                firedDirection,
                extraVelocity,
                sourceProjectile.Weapon,
                sourceProjectile.Shooter,
                comp.ProjectileSpeed);
        }

        QueueDel(uid);
        return true;
    }

    private Vector2 GetDirection(EntityUid uid)
    {
        if (TryComp(uid, out PhysicsComponent? body) && !body.LinearVelocity.IsLengthZero())
            return body.LinearVelocity.Normalized();

        var worldRotation = _transform.GetWorldRotation(uid);
        var direction = worldRotation.ToWorldVec();
        if (direction == Vector2.Zero)
            return Vector2.UnitX;

        return direction.Normalized();
    }
}
