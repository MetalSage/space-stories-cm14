using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem
{
    public bool CanRotate(
        EntityUid uid, 
        Angle targetRotation, 
        TransformComponent xform, 
        PhysicsComponent body,
        FixturesComponent fixtures)
    {
        var localBounds = Box2.UnitCentered;
        var first = true;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard || fixture.CollisionLayer == 0)
                continue;

            var fixtureTransform = new Transform(xform.WorldPosition, targetRotation);
            var aabb = fixture.Shape.ComputeAABB(fixtureTransform, 0);
            localBounds = first ? aabb : localBounds.Union(aabb);
            first = false;
        }

        var rotatedBounds = new Box2Rotated(localBounds, targetRotation, xform.WorldPosition);
        var potentialCollisions = _physics.GetCollidingEntities(xform.MapID, rotatedBounds);

        foreach (var other in potentialCollisions)
        {
            var otherUid = other.Owner;
            if (otherUid == uid)
                continue;

            if (HasComp<MobStateComponent>(otherUid))
                continue;

            if (HasComp<ItemComponent>(otherUid))
                continue;

            if (IsWall(otherUid)) 
                return false;

            if (HasComp<VehicleComponent>(otherUid))
                return false;

            if (_physics.IsHardCollidable(uid, otherUid))
                return false;
        }

        return true;
    }
}

/* TODO
        if (wishDir != Vector2.Zero)
        {
            if (!NoRotateQuery.HasComponent(uid))
            {
                // TODO apparently this results in a duplicate move event because "This should have its event run during
                // island solver"??. So maybe SetRotation needs an argument to avoid raising an event?
                var worldRot = _transform.GetWorldRotation(xform);
                // Stories-Vehicle-Movement-Tweak-Start
                var delta = xform.LocalRotation + wishDir.ToWorldAngle() - worldRot;

                if (VehicleMoveQuery.HasComponent(uid))
                {
                    var currentDir = worldRot.GetCardinalDir();
                    var wishAngle = wishDir.ToWorldAngle();
                    var wishCardinal = wishAngle.GetCardinalDir();

                    if (currentDir.GetOpposite() != wishCardinal)
                    {
                        var cardinalAngle = wishCardinal.ToAngle();
                        var cardinalDelta = xform.LocalRotation + cardinalAngle - worldRot;

                        var targetWorldRotation = worldRot + (cardinalDelta - xform.LocalRotation); 
                        var targetWorldRotCardinal = wishCardinal.ToAngle();

                        if (TryComp<FixturesComponent>(uid, out var fixtures) &&
                            _vehicle.CanRotate(uid, targetWorldRotCardinal, xform, physicsComponent, fixtures))
                        {
                             _transform.SetLocalRotationNoLerp(uid, cardinalDelta, xform);
                        }
                    }
                }
                else
                {
                    _transform.SetLocalRotation(uid, delta, xform);
                }
                // Stories-Vehicle-Movement-Tweak-End
            }
*/