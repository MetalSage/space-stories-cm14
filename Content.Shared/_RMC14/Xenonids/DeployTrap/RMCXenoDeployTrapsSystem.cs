using System.Numerics;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.AcidInsight;
using Content.Shared._RMC14.Xenonids.AcidMine;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.DeployTrap;

public sealed class RMCXenoDeployTrapsSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly RMCXenoAcidInsightSystem _acidInsight = default!;
    [Dependency] private readonly RMCXenoDeployAcidMineSystem _acidMine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCXenoDeployTrapsComponent, RMCXenoDeployTrapsActionEvent>(OnDeployTrap);
        SubscribeLocalEvent<RMCXenoDeployTrapsComponent, XenoProjectileHitUserEvent>(OnProjectileHit);
        SubscribeLocalEvent<RMCXenoBoilerTrapComponent, StartCollideEvent>(OnTrapStartCollide);
    }

    public bool IsTrapped(EntityUid entityUid)
    {
        return HasComp<RMCXenoTrappedComponent>(entityUid);
    }

    private void OnDeployTrap(Entity<RMCXenoDeployTrapsComponent> ent, ref RMCXenoDeployTrapsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var position = args.Target.Position;
        var start = position.Floored() + Vector2.One / 2;
        var delta = (position - Transform(ent).Coordinates.Position).Normalized();
        var axis = delta.X < delta.Y ? Vector2.UnitX : Vector2.UnitY;

        var prototypeId = ent.Comp.PrototypeId;
        if (_acidInsight.TryUseEmpower(ent.Owner))
        {
            prototypeId = ent.Comp.EmpoweredPrototypeId;
            _acidMine.Empower(ent.Owner);
        }

        for (var i = -ent.Comp.Additional; i <= ent.Comp.Additional; i++)
        {
            var trapUid = Spawn(prototypeId, new EntityCoordinates(args.Target.EntityId, start + axis * i));
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

    private void OnTrapStartCollide(Entity<RMCXenoBoilerTrapComponent> ent, ref StartCollideEvent args)
    {
        if (_hive.FromSameHive(args.OtherEntity, ent.Owner) || ent.Comp.Activated)
            return;

        var targetUid = args.OtherEntity;
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
}
