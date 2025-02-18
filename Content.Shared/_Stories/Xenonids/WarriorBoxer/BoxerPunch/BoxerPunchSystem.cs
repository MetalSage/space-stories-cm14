using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.WarriorBoxer.BoxerPunch;

public sealed class BoxerPunchSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedKOCounterSystem _koCounter = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerPunchComponent, BoxerPunchActionEvent>(OnBoxerPunchAction);
    }

    private void OnBoxerPunchAction(Entity<BoxerPunchComponent> xeno, ref BoxerPunchActionEvent args)
    {
        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        if (args.Handled)
            return;

        if (!TryComp<KOComponent>(args.Performer, out var koComp))
            return;

        args.Handled = true;

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        var targetId = args.Target;
        _rmcPulling.TryStopAllPullsFromAndOn(targetId);

        var damage = _damageable.TryChangeDamage(targetId, xeno.Comp.Damage);
        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(targetId, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetId }, filter);
        }

        _rmcMelee.DoLunge(xeno, targetId);
        _slow.TrySlowdown(targetId, xeno.Comp.SlowDuration);

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.Effect, targetId.ToCoordinates());

        if (koComp.LastHitTarget == targetId)
        {
            _koCounter.ResetKO(args.Performer, koComp);
            return;
        }
        else
            koComp.KOCounter = MathF.Min(koComp.KOCounter + 1f, koComp.MaxKOCounter);

        koComp.LastHitTarget = targetId;
        koComp.LastHitTime = _timing.CurTime;
    }
}
