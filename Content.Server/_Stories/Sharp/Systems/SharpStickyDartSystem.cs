using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._Stories.Sharp;
using Content.Shared.Explosion.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Sharp;

public sealed class SharpStickyDartSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GunIFFSystem _gunIFF = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntProtoId<IFFFactionComponent>> _iffBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpStickyDartComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<SharpStickyDartComponent, ProjectileHitEvent>(OnHit, before: new[] { typeof(SharedProjectileSystem) });
        SubscribeLocalEvent<SharpStickyDartComponent, ProjectileFixedDistanceStopEvent>(OnFixedStop);
        SubscribeLocalEvent<SharpFuseModeComponent, AmmoShotEvent>(OnSharpAmmoShot);
    }

    private void OnPreventCollide(EntityUid uid, SharpStickyDartComponent comp, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (ShouldPassThroughTarget(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnHit(EntityUid uid, SharpStickyDartComponent comp, ref ProjectileHitEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(uid))
            return;

        if (IsFriendlyTarget(uid, args.Target))
        {
            args.Handled = true;
            DropIffRecoveryAndDelete(uid, comp);
            return;
        }

        if (ShouldPassThroughTarget(args.Target))
        {
            args.Handled = true;
            return;
        }

        if (CanStickToTarget(args.Target))
            return;

        args.Handled = true;
        SpawnMineAndDelete(uid, comp);
    }

    private void OnFixedStop(EntityUid uid, SharpStickyDartComponent comp, ref ProjectileFixedDistanceStopEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (TryComp<EmbeddableProjectileComponent>(uid, out var emb) && emb.EmbeddedIntoUid != null)
            return;

        SnapToPlannedStop(uid);

        SpawnMineAndDelete(uid, comp);
    }

    private void SpawnMineAndDelete(EntityUid uid, SharpStickyDartComponent comp)
    {
        if (comp.MineSpawned || TerminatingOrDeleted(uid))
            return;

        comp.MineSpawned = true;
        Dirty(uid, comp);

        var mineCoords = _transform.GetMapCoordinates(uid);

        _transform.AttachToGridOrMap(uid);

        var mine = Spawn(comp.MineProto, mineCoords);
        TransferMineFactions(uid, mine);

        QueueDel(uid);
    }

    private void DropIffRecoveryAndDelete(EntityUid uid, SharpStickyDartComponent comp)
    {
        if (comp.MineSpawned || TerminatingOrDeleted(uid))
            return;

        comp.MineSpawned = true;
        Dirty(uid, comp);

        var coords = _transform.GetMapCoordinates(uid);

        _transform.AttachToGridOrMap(uid);

        if (comp.IffDropProto is { } dropProto)
            Spawn(dropProto, coords);

        QueueDel(uid);
    }

    private void OnSharpAmmoShot(EntityUid uid, SharpFuseModeComponent comp, ref AmmoShotEvent args)
    {
        var hasPlannedStop = TryGetShotTarget(uid, out var targetMap, out var fixedPoint);

        foreach (var projectile in args.FiredProjectiles)
        {
            if (TerminatingOrDeleted(projectile) || !TryComp<SharpStickyDartComponent>(projectile, out var sticky))
                continue;

            sticky.SelectedDelay = comp.LongMode ? sticky.LongDelay : sticky.ShortDelay;

            if (!hasPlannedStop || !TryGetPlannedStop(projectile, targetMap, fixedPoint, out var stopMap))
                continue;

            var stopComp = EnsureComp<SharpStickyDartStopPointComponent>(projectile);
            stopComp.Coordinates = stopMap;
        }
    }

    private bool TryGetShotTarget(
        EntityUid uid,
        out MapCoordinates targetMap,
        out ShootAtFixedPointComponent fixedPoint)
    {
        targetMap = default;
        fixedPoint = default!;

        if (!TryComp(uid, out GunComponent? gun) ||
            gun.ShootCoordinates is not { } targetCoords ||
            !TryComp<ShootAtFixedPointComponent>(uid, out var fixedPointComp))
        {
            return false;
        }

        fixedPoint = fixedPointComp!;
        targetMap = _transform.ToMapCoordinates(targetCoords);
        return true;
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

            if (!sticky.Armed)
            {
                sticky.Armed = true;

                var delay = sticky.SelectedDelay ?? sticky.LongDelay;
                sticky.DetonateAt = _timing.CurTime + TimeSpan.FromSeconds(delay);
                Dirty(uid, sticky);
            }
            else if (_timing.CurTime >= sticky.DetonateAt)
            {
                if (IsFriendlyTarget(uid, target))
                {
                    DropIffRecoveryAndDelete(uid, sticky);
                }
                else
                {
                    _transform.AttachToGridOrMap(uid);

                    if (HasComp<TileFireOnTriggerComponent>(uid))
                    {
                        var fireEv = new RMCTriggerEvent();
                        RaiseLocalEvent(uid, ref fireEv);
                    }

                    if (TryComp<ExplosiveComponent>(uid, out var explosive))
                    {
                        _explosion.TriggerExplosive(uid,
                            explosive,
                            delete: true,
                            radius: sticky.ExplosionRadius,
                            user: proj.Shooter);
                    }
                    else
                    {
                        QueueDel(uid);
                    }
                }
            }
        }
    }

    private bool IsFriendlyTarget(EntityUid projectileUid, EntityUid target)
    {
        if (!TryGetProjectileFactions(projectileUid, _iffBuffer))
            return false;

        foreach (var faction in _iffBuffer)
        {
            if (_gunIFF.IsInFaction(target, faction))
                return true;
        }

        return false;
    }

    private bool TryGetProjectileFactions(
        EntityUid projectileUid,
        HashSet<EntProtoId<IFFFactionComponent>> factions)
    {
        factions.Clear();

        if (TryComp<ProjectileIFFComponent>(projectileUid, out var projectileIff) &&
            projectileIff.Enabled &&
            projectileIff.Factions.Count > 0)
        {
            factions.UnionWith(projectileIff.Factions);
            return true;
        }

        if (projectileIff != null)
            return false;

        if (!TryComp(projectileUid, out ProjectileComponent? projectile) ||
            projectile.Shooter is not { } shooter)
        {
            return false;
        }

        return _gunIFF.TryGetFactions((shooter, CompOrNull<UserIFFComponent>(shooter)), factions, SlotFlags.IDCARD);
    }

    private void TransferMineFactions(EntityUid projectileUid, EntityUid mineUid)
    {
        if (!TryGetProjectileFactions(projectileUid, _iffBuffer))
            return;

        if (!TryComp<SharpMineComponent>(mineUid, out var mine))
            return;

        mine.IffFactions.Clear();

        foreach (var faction in _iffBuffer)
        {
            mine.IffFactions.Add(faction);
        }
    }

    private bool ShouldPassThroughTarget(EntityUid target)
    {
        return HasComp<SharpMineComponent>(target);
    }

    private bool CanStickToTarget(EntityUid target)
    {
        return HasComp<MobStateComponent>(target);
    }
}
