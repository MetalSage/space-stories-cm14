using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;

public sealed class BoxerPunchSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly SharedBoxerKOSystem _koSystem = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerPunchComponent, BoxerPunchActionEvent>(OnBoxerPunchAction);
    }

    private void OnBoxerPunchAction(Entity<BoxerPunchComponent> xeno, ref BoxerPunchActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        args.Handled = true;

        if (!TryComp<XenoBoxerKOComponent>(xeno, out var koComp))
            return;

        var comp = xeno.Comp;
        var targetId = args.Target;

        _rmcPulling.TryStopAllPullsFromAndOn(targetId);

        var damage = _damageable.TryChangeDamage(targetId, comp.Damage);
        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(targetId, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetId }, filter);
        }

        _rmcMelee.DoLunge(xeno, targetId);
        _slow.TrySlowdown(targetId, comp.SlowDuration);

        if (_net.IsServer)
        {
            SpawnAttachedTo(comp.Effect, targetId.ToCoordinates());
            _audio.PlayPvs(comp.Sound, xeno);
        }

        _koSystem.UpdateKOTracker(xeno, koComp, args.Target, comp.KOIncrease);
        if (!TryComp<XenoBoxerKORecentlyComponent>(xeno, out var recently))
            return;
        var tracker = recently.Trackers.GetValueOrDefault(args.Target);

        foreach (var (actionId, action) in _action.GetActions(xeno))
        {
            if (action.BaseEvent is BoxerJabActionEvent && tracker.Count != koComp.MaxKO)
                _action.SetCooldown(actionId, comp.Cooldown);
        }
    }
}
