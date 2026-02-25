using System;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._Stories.Sharp;
using Content.Shared.Explosion.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Weapons.Ranged;
namespace Content.Server._Stories.Sharp;

/// <summary>
/// SERVER ONLY.
/// Key points for "no phantom":
/// - Never delete on client; server always deletes.
/// - Do NOT detach to nullspace before delete (can drop out of PVS before the delete replicates).
/// - Before spawning mine / deleting, ensure the dart is attached to grid/map.
/// </summary>
public sealed class SharpStickyDartSystem : EntitySystem
{
    private const float SharpExplosionRadius = 3f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpStickyDartComponent, ProjectileHitEvent>(OnHit);
        SubscribeLocalEvent<SharpStickyDartComponent, ProjectileFixedDistanceStopEvent>(OnFixedStop);
        SubscribeLocalEvent<SharpFuseModeComponent, AmmoShotEvent>(OnSharpAmmoShot);
    }

    private void OnHit(EntityUid uid, SharpStickyDartComponent comp, ref ProjectileHitEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        // Hit a mob -> embedding happens; timer handled in Update().
        if (HasComp<MobStateComponent>(args.Target))
            return;

        // Hit wall/floor -> replace with mine and delete dart (server).
        SpawnMineAndDelete(uid, comp);
    }

    private void OnFixedStop(EntityUid uid, SharpStickyDartComponent comp, ref ProjectileFixedDistanceStopEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        // Already embedded into a mob -> ignore.
        if (TryComp<EmbeddableProjectileComponent>(uid, out var emb) && emb.EmbeddedIntoUid != null)
            return;

        SnapToPlannedStop(uid);

        // Reached max distance -> replace with mine and delete dart (server).
        SpawnMineAndDelete(uid, comp);
    }

    private void SpawnMineAndDelete(EntityUid uid, SharpStickyDartComponent comp)
    {
        if (comp.MineSpawned || TerminatingOrDeleted(uid))
            return;

        comp.MineSpawned = true;
        Dirty(uid, comp);

        var mineCoords = _transform.GetMapCoordinates(uid);

        // IMPORTANT: keep it in the world so delete replicates to clients.
        _transform.AttachToGridOrMap(uid);

        Spawn(comp.MineProto, mineCoords);

        QueueDel(uid);
    }

    private void DropIffRecoveryAndDelete(EntityUid uid, SharpStickyDartComponent comp)
    {
        if (comp.MineSpawned || TerminatingOrDeleted(uid))
            return;

        comp.MineSpawned = true;
        Dirty(uid, comp);

        var coords = _transform.GetMapCoordinates(uid);

        // Keep it in-world so delete is replicated.
        _transform.AttachToGridOrMap(uid);

        if (comp.IffDropProto is { } dropProto)
            Spawn(dropProto, coords);

        QueueDel(uid);
    }

    private void OnSharpAmmoShot(EntityUid uid, SharpFuseModeComponent comp, ref AmmoShotEvent args)
    {
        if (!TryComp(uid, out GunComponent? gun) ||
            gun.ShootCoordinates is not { } targetCoords ||
            !TryComp(uid, out ShootAtFixedPointComponent? fixedPoint))
        {
            return;
        }

        var targetMap = _transform.ToMapCoordinates(targetCoords);

        foreach (var projectile in args.FiredProjectiles)
        {
            if (TerminatingOrDeleted(projectile) || !HasComp<SharpStickyDartComponent>(projectile))
                continue;

            if (!TryGetPlannedStop(projectile, targetMap, fixedPoint, out var stopMap))
                continue;

            var stopComp = EnsureComp<SharpStickyDartStopPointComponent>(projectile);
            stopComp.Coordinates = stopMap;
        }
    }

    private bool TryGetPlannedStop(
        EntityUid projectile,
        MapCoordinates targetMap,
        ShootAtFixedPointComponent fixedPoint,
        out MapCoordinates stopMap)
    {
        stopMap = default;

        var fromMap = _transform.GetMapCoordinates(projectile);
        if (fromMap.MapId != targetMap.MapId)
            return false;

        var direction = targetMap.Position - fromMap.Position;
        if (direction == Vector2.Zero)
        {
            stopMap = fromMap;
            return true;
        }

        var distance = direction.Length();
        if (fixedPoint.MaxFixedRange is { } maxFixedRange)
            distance = Math.Min(distance, maxFixedRange);

        if (TryComp(projectile, out ProjectileComponent? projectileComp) &&
            projectileComp.MaxFixedRange is { } projectileMaxRange &&
            projectileMaxRange > 0f)
        {
            distance = Math.Min(distance, projectileMaxRange);
        }

        if (distance <= 0f)
            return false;

        stopMap = new MapCoordinates(fromMap.Position + direction.Normalized() * distance, fromMap.MapId);
        return true;
    }

    private void SnapToPlannedStop(EntityUid uid)
    {
        if (TryComp(uid, out SharpStickyDartStopPointComponent? stop))
            _transform.SetMapCoordinates(uid, stop.Coordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SharpStickyDartComponent, EmbeddableProjectileComponent, ProjectileComponent>();
        while (query.MoveNext(out var uid, out var sticky, out var emb, out var proj))
        {
            if (TerminatingOrDeleted(uid) || emb.EmbeddedIntoUid == null)
                continue;

            var target = emb.EmbeddedIntoUid.Value;
            var rt = EnsureComp<SharpStickyDartRuntimeComponent>(uid);

            if (!rt.Armed)
            {
                rt.Armed = true;

                var delay = sticky.LongDelay;
                if (proj.Weapon != null && TryComp<SharpFuseModeComponent>(proj.Weapon.Value, out var mode))
                    delay = mode.LongMode ? sticky.LongDelay : sticky.ShortDelay;

                rt.DetonateAt = _timing.CurTime + TimeSpan.FromSeconds(delay);
            }
            else if (_timing.CurTime >= rt.DetonateAt)
            {
                if (HasIff(target))
                {
                    // Target has IFF: after fuse time, drop recoverable dart entity and remove projectile.
                    DropIffRecoveryAndDelete(uid, sticky);
                }
                else
                {
                    // Ensure stable coords before trigger/explosion.
                    _transform.AttachToGridOrMap(uid);

                    // Target has no IFF: optional fire trigger then explode (delete: true).
                    if (HasComp<TileFireOnTriggerComponent>(uid))
                    {
                        var fireEv = new RMCTriggerEvent();
                        RaiseLocalEvent(uid, ref fireEv);
                    }

                    if (TryComp<ExplosiveComponent>(uid, out var explosive))
                    {
                        // delete:true guarantees server-side deletion
                        _explosion.TriggerExplosive(uid, explosive, delete: true, radius: SharpExplosionRadius, user: proj.Shooter);
                    }
                    else
                    {
                        QueueDel(uid);
                    }
                }
            }
        }
    }

    private bool HasIff(EntityUid target)
    {
        var targetEv = new GetIFFFactionEvent(SlotFlags.IDCARD, new());
        RaiseLocalEvent(target, ref targetEv);
        return targetEv.Factions.Count > 0;
    }

    private bool TerminatingOrDeleted(EntityUid uid)
    {
        if (Deleted(uid))
            return true;

        if (TryComp<MetaDataComponent>(uid, out var meta))
            return meta.EntityLifeStage >= EntityLifeStage.Terminating;

        return false;
    }
}
