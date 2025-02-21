using System.Linq;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerUppercut;

public sealed class BoxerUppercutSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBoxerKOSystem _koSystem = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamage = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerUppercutComponent, BoxerUppercutActionEvent>(OnBoxerUppercutAction);
    }

    private void OnBoxerUppercutAction(Entity<BoxerUppercutComponent> xeno, ref BoxerUppercutActionEvent args)
    {

        if (!_timing.IsFirstTimePredicted || args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<XenoBoxerKOComponent>(xeno, out var koComp))
            return;

        if (!TryComp<XenoBoxerKORecentlyComponent>(xeno, out var recently))
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        var comp = xeno.Comp;
        var tracker = recently.Trackers.GetValueOrDefault(args.Target);

        var popupPower = "weak";

        if (_net.IsServer)
            _audio.PlayPvs(comp.Sound, xeno);

        var targetId = args.Target;
        var damageModificator = Math.Min(tracker.Count * comp.DamageModificator, 150);

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.GetMapCoordinates(targetId);
        var diff = target.Position - origin.Position;
        diff = diff.Normalized() * (tracker.Count / comp.Range);

        var damage = _damageable.TryChangeDamage(targetId, new DamageSpecifier(
            _proto.Index<DamageTypePrototype>("Blunt"), damageModificator));

        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(targetId, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetId }, filter);
            popupPower = "good";
        }

        if (TryComp<DamageableComponent>(xeno, out var damageable) &&
        TryComp<MobThresholdsComponent>(xeno, out var mobThreshold))
        {

            var heal = (mobThreshold.Thresholds.Keys.Max().Value - damageable.TotalDamage) *
            (Math.Clamp(tracker.Count, 0, koComp.MaxKO) * comp.HealPerStack);

            var amount = -_rmcDamage.DistributeTypesTotal(xeno.Owner, heal);
            _damageable.TryChangeDamage(xeno, amount);

            Logger.Debug($"mobThreshold = {mobThreshold.Thresholds.Keys.Max().Value}, totalDamage = {damageable.TotalDamage}, mathClamp = {Math.Clamp(tracker.Count, 0, koComp.MaxKO) * comp.HealPerStack}, totalHeal = {heal}, and after rmc: {amount}");
            if (_net.IsServer)
                SpawnAttachedTo(comp.HealEffect, xeno.Owner.ToCoordinates());
        }

        _rmcPulling.TryStopAllPullsFromAndOn(targetId);

        if (tracker.Count <= 5)
        {
            _throwing.TryThrow(targetId, diff, 10);
            popupPower = "powerful";
        }
        else if (tracker.Count <= 10)
        {
            _throwing.TryThrow(targetId, diff, 10);
            _stun.TryParalyze(targetId, xeno.Comp.ParalyzeTime, true);
            popupPower = "gigantic";
        }
        else
        {
            _throwing.TryThrow(targetId, diff, 10);
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(targetId, comp.StatusEffectKey,
                comp.StatusEffectTime, false);
            _stun.TryParalyze(targetId, xeno.Comp.StatusEffectTime, true);

            if (_net.IsServer)
                _audio.PlayPvs(comp.GongSound, xeno);

            //_popup.PopupEntity("K.O!", xeno, PopupType.LargeCaution);
            EnsureComp<KOLabelComponent>(targetId);
            Timer.Spawn(TimeSpan.FromSeconds(4), () =>
            {
                RemCompDeferred<KOLabelComponent>(targetId);
            });
            popupPower = "titanic";
        }

        var message = Loc.GetString("xeno-boxer-strain-other-uppercut-" + popupPower, ("target", targetId), ("xeno", xeno.Owner));
        _popup.PopupPredicted(null, message, xeno, xeno, PopupType.LargeCaution);

        _rmcMelee.DoLunge(xeno, targetId);

        if (_net.IsServer)
            SpawnAttachedTo(comp.Effect, targetId.ToCoordinates());

        foreach (var (actionId, action) in _action.GetActions(xeno))
        {
            if (action.BaseEvent is BoxerPunchActionEvent or BoxerJabActionEvent)
                _action.SetCooldown(actionId, comp.Cooldown);
        }

        recently.Trackers.Clear();
        Dirty(xeno, recently);
    }
}
