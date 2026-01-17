using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Stories.Hunter.Projectiles;

public sealed class HunterPlasmaCasterProjectileSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterStunBoltComponent, ProjectileHitEvent>(OnStunBoltHit);

        SubscribeLocalEvent<HunterAreaStunOnHitComponent, ProjectileHitEvent>(OnAreaStunHit);
        SubscribeLocalEvent<HunterAreaStunOnHitComponent, StartCollideEvent>(OnAreaStunCollide);
    }

    private void OnStunBoltHit(Entity<HunterStunBoltComponent> ent, ref ProjectileHitEvent args)
    {
        ApplyStunBolt(ent, args.Target);
    }

    private void ApplyStunBolt(Entity<HunterStunBoltComponent> ent, EntityUid target)
    {
        if (!HasComp<MobStateComponent>(target))
            return;

        if (HasComp<HunterComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("st-hunter-projectile-immune"), target, target);
            return;
        }

        var duration = ent.Comp.StunTime;

        if (duration > TimeSpan.Zero)
        {
            _stun.TryParalyze(target, duration, true);

            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("st-hunter-projectile-stun-feedback"),
                    target,
                    target,
                    PopupType.MediumCaution);
        }
    }

    private void OnAreaStunHit(Entity<HunterAreaStunOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        ExplodeAreaStun(ent);
    }

    private void OnAreaStunCollide(Entity<HunterAreaStunOnHitComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.Detonated)
            return;

        ExplodeAreaStun(ent);
    }

    private void ExplodeAreaStun(Entity<HunterAreaStunOnHitComponent> ent)
    {
        if (ent.Comp.Detonated)
            return;

        ent.Comp.Detonated = true;
        var coords = _transform.GetMapCoordinates(ent);
        var targets = _lookup.GetEntitiesInRange(coords, ent.Comp.Radius);

        foreach (var target in targets)
        {
            if (!HasComp<MobStateComponent>(target))
                continue;

            var duration = ent.Comp.StunTime;

            if (HasComp<HunterComponent>(target))
            {
                duration -= ent.Comp.HunterReductionTime;
                if (duration < TimeSpan.Zero)
                    duration = TimeSpan.Zero;
            }

            if (duration > TimeSpan.Zero)
            {
                _stun.TryParalyze(target, duration, true);

                if (_net.IsServer)
                    _popup.PopupEntity(Loc.GetString("st-hunter-projectile-aoe-stun-feedback"),
                        target,
                        target,
                        PopupType.LargeCaution);
            }
        }

        if (_net.IsServer)
            QueueDel(ent);
    }
}
