using System;
using Content.Shared._Stories.Attachables;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

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
        var user = args.User;
        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates == null)
            return;

        if (_holder.TryGetHolder(args.User, out var holder) || holder is not null)
            user = holder.Value;

        var shooterTransform = _transform.GetWorldPosition(user);
        var targetPosition = _transform.ToMapCoordinates(gun.ShootCoordinates.Value).Position;
        var shooterRotation = _transform.GetWorldRotation(user);

        var directionToTarget = (targetPosition - shooterTransform).Normalized();
        var targetAngle = directionToTarget.ToAngle();

        var angleDifference = GetAngleDifference(shooterRotation, targetAngle);

        if (Math.Abs(angleDifference.Theta) > ent.Comp.MaxAngleDeviation.Theta)
        {
            args.Cancelled = true;
            if (holder != null && _holder.TryGetUser(holder.Value, out var pilot) && pilot != null)
                _popup.PopupCursor(ent.Comp.RestrictionMessage, pilot.Value, PopupType.SmallCaution);
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
            diff -= 2 * Math.PI; // implicit conversion

        while (diff.Theta < -Math.PI)
            diff += 2 * Math.PI; // implicit conversion

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
