using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Shared._Stories.Xenonids.Crusher;

public sealed class STCrusherSplashSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCrusherSplashComponent, MeleeHitEvent>(OnMeleeHit, after: [typeof(SharedRMCMeleeWeaponSystem)]);
    }

    private void OnMeleeHit(Entity<STCrusherSplashComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        EntityUid? mainTarget = null;
        foreach (var ent in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, ent))
                continue;

            mainTarget = ent;
            break;
        }

        if (mainTarget == null)
            return;

        var totalDamage = args.BaseDamage + args.BonusDamage;
        var splashDamage = totalDamage * xeno.Comp.DamagePercent;

        var currHits = 0;
        foreach (var extra in _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(mainTarget.Value), xeno.Comp.Range))
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, extra) || _mobState.IsDead(extra))
                continue;

            if (args.HitEntities.Contains(extra.Owner))
                continue;

            currHits++;

            var modified = _xeno.ApplyXenoMeleeDamageModifiers(xeno, extra, splashDamage);
            var myDamage = _damageable.TryChangeDamage(extra, modified, origin: xeno, tool: xeno);

            if (myDamage?.GetTotal() > FixedPoint2.Zero)
            {
                var filter = Filter.Pvs(extra, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { extra }, filter);
            }

            var splashEv = new STCrusherSplashHitEvent(xeno.Owner, mainTarget.Value);
            RaiseLocalEvent(extra, ref splashEv);

            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.Effect, extra.Owner.ToCoordinates());

            if (xeno.Comp.MaxTargets != null && currHits >= xeno.Comp.MaxTargets)
                break;
        }
    }
}
