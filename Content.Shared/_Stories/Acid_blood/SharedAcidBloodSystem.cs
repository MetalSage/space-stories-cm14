using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Xenonids.Projectile;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Maps;


namespace Content.Shared._Stories.AcidBlood;

public sealed class XenoAcidBloodSystem : EntitySystem
{
    [Dependency] private readonly XenoProjectileSystem _xenoProjectile = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMAcidBloodComponent, DamageModifyAfterResistEvent>(OnDamage);
    }

    private void OnDamage(Entity<CMAcidBloodComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;
        // var coords = _transform.GetMoverCoordinates(ent);
        var xform = Transform(ent);
        if (xform.GridUid == null)
        {
            return;
        }
        if (!TryComp<MapGridComponent>(xform.GridUid, out var mapGrid))
            return;
        var directionPos = xform.Coordinates.Offset(xform.LocalRotation.ToWorldVec().Normalized());
        if (!directionPos.TryGetTileRef(out var tileReference, EntityManager, _mapManager))
            return;
        var tileIndex = tileReference.Value.GridIndices;
        EntityCoordinates coords = _mapSystem.GridTileToLocal(xform.GridUid.Value, mapGrid, tileIndex + (1, 0));

        if (_xenoProjectile.TryShoot(
            ent,
            coords,
            FixedPoint2.Zero,
            ent.Comp.ProjectileId,
            null,
            1,
            Angle.FromDegrees(360),
            15,
            target: null
        ))
            return;

    }
}
