using Content.Shared._Stories.Attachables;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class GunArcRestrictionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly VehicleAttachableHolderSystem _holder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunArcRestrictionComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<GunArcRestrictionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnAttemptShoot(Entity<GunArcRestrictionComponent> ent, ref AttemptShootEvent args)
    {
        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates == null)
            return;

        var shooterEntity = args.User;
        if (_holder.TryGetHolder(ent.Owner, out var holder) && holder is not null)
            shooterEntity = holder.Value;

        var shooterPos = _transform.GetWorldPosition(shooterEntity);
        var targetPos = _transform.ToMapCoordinates(gun.ShootCoordinates.Value).Position;

        var shooterRotation = _transform.GetWorldRotation(shooterEntity);
        var forward = shooterRotation.ToWorldVec();

        var directionToTarget = (targetPos - shooterPos).Normalized();
        var targetAngle = directionToTarget.ToAngle();

        var angleDifference = GetAngleDifference(forward.ToAngle(), targetAngle);

        if (Math.Abs(angleDifference.Theta) > ent.Comp.MaxAngleDeviation.Theta)
        {
            args.Cancelled = true;

            if (_holder.TryGetAttachableUser(ent.Owner, out var pilot) && pilot != null)
            {
                _popup.PopupCursor(ent.Comp.RestrictionMessage, pilot.Value, PopupType.SmallCaution);
            }
        }
    }

    private void OnExamined(EntityUid uid, GunArcRestrictionComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var degreesDeviation = component.MaxAngleDeviation.Degrees;
        var totalArc = degreesDeviation * 2;

        args.PushMarkup(Loc.GetString("gun-arc-restriction-examine",
            ("degrees", Math.Round(totalArc, 1))));
    }

    private static Angle GetAngleDifference(Angle from, Angle to)
    {
        var diff = to - from;

        while (diff.Theta > Math.PI)
            diff -= 2 * Math.PI;

        while (diff.Theta < -Math.PI)
            diff += 2 * Math.PI;

        return diff;
    }


    public void SetMaxAngleDeviation(EntityUid uid, Angle maxDeviation, GunArcRestrictionComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.MaxAngleDeviation = maxDeviation;
        Dirty(uid, component);
    }
}
