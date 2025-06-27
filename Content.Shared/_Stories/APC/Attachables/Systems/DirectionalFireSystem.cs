using System;
using System.Numerics;
using Robust.Shared.Map.Components;
using Robust.Shared.Log;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared._Stories.Attachables;

public sealed class DirectionalFireSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DirectionalFireComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<DirectionalFireComponent> ent, ref AttemptShootEvent args)
    {
        Log.Debug($"OnAttemptShoot START for entity {ent}");

        if (args.Cancelled)
        {
            Log.Debug("AttemptShootEvent уже отменён, выходим");
            return;
        }

        if (!TryComp<GunComponent>(ent, out var gun))
        {
            Log.Debug("У сущности нет GunComponent, выходим");
            return;
        }

        var xform = Transform(ent);
        var shootCoords = gun.ShootCoordinates;

        if (shootCoords == null)
        {
            Log.Warning($"DirectionalFireSystem: ShootCoordinates is null for entity {ent}");
            return;
        }

        var worldPos = _transform.GetWorldPosition(xform);
        var worldRot = _transform.GetWorldRotation(xform);

        Log.Debug($"World Position: {worldPos}");
        Log.Debug($"World Rotation (degrees): {worldRot.Degrees}");

        var facingDir = worldRot.ToVec();
        Log.Debug($"Facing Direction Vector: {facingDir}");

        var targetWorld = shootCoords.Value.Position;
        Log.Debug($"Target World Position: {targetWorld}");

        var toTargetVector = targetWorld - worldPos;
        Log.Debug($"Vector to Target (not normalized): {toTargetVector}");

        var toTarget = Vector2.Normalize(toTargetVector);
        Log.Debug($"Normalized Vector to Target: {toTarget}");

        // Угол через Angle.ShortestDistance
        var angleBetweenRaw = Angle.ShortestDistance(
            Angle.FromWorldVec(facingDir),
            Angle.FromWorldVec(toTarget));
        var angleBetween = Math.Abs(angleBetweenRaw.Degrees);
        Log.Debug($"Angle between facingDir и toTarget (Angle.ShortestDistance): {angleBetweenRaw.Degrees} (raw), {angleBetween} (abs)");

        // Дополнительная проверка с dot product
        float dot = Vector2.Dot(facingDir, toTarget);
        dot = Math.Clamp(dot, -1f, 1f);
        float angleDegDot = MathF.Acos(dot) * (180f / MathF.PI);
        Log.Debug($"Angle between facingDir и toTarget (через dot product): {angleDegDot}");

        Log.Debug($"MaxFireAngle (degrees): {ent.Comp.MaxFireAngle.Degrees}");

        if (angleBetween > ent.Comp.MaxFireAngle.Degrees)
        {
            args.Message = Loc.GetString("stories-gun-out-of-angle");
            args.Cancelled = true;
            Log.Debug($"Отмена выстрела: угол {angleBetween} превышает MaxFireAngle {ent.Comp.MaxFireAngle.Degrees}");
        }
        else
        {
            Log.Debug($"Выстрел разрешён: угол {angleBetween} в пределах MaxFireAngle {ent.Comp.MaxFireAngle.Degrees}");
        }

        Log.Debug("OnAttemptShoot END");
    }

}
