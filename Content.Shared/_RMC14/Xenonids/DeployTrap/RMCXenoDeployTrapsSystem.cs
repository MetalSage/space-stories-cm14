using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.AcidInsight;
using Content.Shared._RMC14.Xenonids.AcidMine;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.DeployTrap;

public sealed class RMCXenoDeployTrapsSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly RMCXenoAcidInsightSystem _acidInsight = default!;
    [Dependency] private readonly RMCXenoDeployAcidMineSystem _acidMine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCXenoDeployTrapsComponent, RMCXenoDeployTrapsActionEvent>(OnDeployTrap);
        SubscribeLocalEvent<RMCXenoDeployTrapsComponent, XenoProjectileHitUserEvent>(OnProjectileHit);

        SubscribeLocalEvent<RMCXenoBoilerTrapComponent, ComponentStartup>(OnTrapStartup);
        SubscribeLocalEvent<RMCXenoBoilerTrapComponent, StartCollideEvent>(OnTrapStartCollide);
        SubscribeLocalEvent<RMCXenoBoilerTrapComponent, EndCollideEvent>(OnTrapEndCollide);
    }

    public bool IsTrapped(EntityUid entityUid)
    {
        return HasComp<RMCXenoTrappedComponent>(entityUid);
    }

    private void OnDeployTrap(Entity<RMCXenoDeployTrapsComponent> ent, ref RMCXenoDeployTrapsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        var position = args.Target.Position;
        var start = position.Floored() + Vector2.One / 2;
        var delta = (position - Transform(ent).Coordinates.Position).Normalized();

        Vector2 axis;
        if (delta.Equals(Vector2.Zero))
            axis = Vector2.UnitX;
        else
        {
            var absX = Math.Abs(delta.X);
            var absY = Math.Abs(delta.Y);
            var signX = Math.Sign(delta.X);
            var signY = Math.Sign(delta.Y);

            if (absY > absX * 2.414f)
                axis = new Vector2(signX != 0 ? signX : 1, 0);
            else if (absX > absY * 2.414f)
                axis = new Vector2(0, signY != 0 ? signY : 1);
            else
                axis = new Angle(Math.PI /2 ).RotateVec(new Vector2(signX, signY));
        }

        var prototypeId = ent.Comp.PrototypeId;
        if (_acidInsight.TryUseEmpower(ent.Owner))
        {
            prototypeId = ent.Comp.EmpoweredPrototypeId;
            _acidMine.Empower(ent.Owner);
        }

        if (_net.IsClient)
            return;

        for (var i = -ent.Comp.Additional; i <= ent.Comp.Additional; i++)
        {
            var gridId = _transform.GetGrid(ent.Owner);
            if (!TryComp(gridId, out MapGridComponent? grid))
                continue;

            var target = new EntityCoordinates(args.Target.EntityId, start + axis * i);
            var tile = _map.GetTileRef(gridId.Value, grid, target);
            if (_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;

            var trapUid = Spawn(prototypeId, target);

            _hive.SetSameHive(ent.Owner, trapUid);
        }
    }

    private void OnProjectileHit(Entity<RMCXenoDeployTrapsComponent> ent, ref XenoProjectileHitUserEvent args)
    {
        if (!TryComp<RMCXenoTrappedComponent>(args.Hit, out var deployedTrappedComponent))
            return;

        if (!TryComp<ProjectileComponent>(args.Projectile, out var projectileComponent))
            return;

        var damage = projectileComponent.Damage * (deployedTrappedComponent.DamageBonus - 1);
        _damageable.TryChangeDamage(args.Hit, damage, projectileComponent.IgnoreResistances);
    }

    private void OnTrapStartup(Entity<RMCXenoBoilerTrapComponent> ent, ref ComponentStartup args)
    {
        foreach (var targetUid in _entityLookup.GetEntitiesIntersecting(ent))
        {
            if (!HasComp<MobStateComponent>(targetUid))
                continue;

            ent.Comp.Ignore.Add(targetUid);
        }
    }

    private void OnTrapStartCollide(Entity<RMCXenoBoilerTrapComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.Ignore.Contains(args.OtherEntity))
            return;

        if (ent.Comp.Activated)
            return;

        if (_hive.FromSameHive(ent.Owner, args.OtherEntity))
            return;

        var targetUid = args.OtherEntity;
        if (HasComp<RMCRootedComponent>(targetUid))
            return;

        _slow.TryRoot(targetUid, ent.Comp.RootDuration);

        EnsureComp<RMCXenoTrappedComponent>(targetUid);

        ent.Comp.Activated = true;
        DirtyField(ent, ent.Comp, nameof(RMCXenoBoilerTrapComponent.Activated));

        if (_net.IsClient)
            return;

        Timer.Spawn(ent.Comp.RootDuration, () =>
        {
            RemCompDeferred<RMCXenoTrappedComponent>(targetUid);
        });

        QueueDel(ent);
    }

    private void OnTrapEndCollide(Entity<RMCXenoBoilerTrapComponent> ent, ref EndCollideEvent args)
    {
        if (!ent.Comp.Ignore.Contains(args.OtherEntity))
            return;

        ent.Comp.Ignore.Remove(args.OtherEntity);
    }
}
