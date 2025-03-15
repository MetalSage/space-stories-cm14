using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
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
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamage = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedBoxerKOSystem _koSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerUppercutComponent, BoxerUppercutActionEvent>(OnBoxerUppercutAction);
    }

    private void OnBoxerUppercutAction(Entity<BoxerUppercutComponent> xeno, ref BoxerUppercutActionEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted || _net.IsClient)
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        args.Handled = true;

        if (!TryComp(xeno, out XenoBoxerKOComponent? koComp) ||
            !TryComp(xeno, out XenoBoxerKORecentlyComponent? recently))
            return;

        var targetId = args.Target;
        var comp = xeno.Comp;
        var tracker = recently.Trackers.GetValueOrDefault(args.Target);
        var popupPower = "weak";

        _audio.PlayPvs(comp.Sound, xeno);

        var damageModificator = Math.Min(tracker.Count * comp.DamageModificator, 150);
        var damage = _damageable.TryChangeDamage(targetId, new DamageSpecifier(
            _proto.Index<DamageTypePrototype>("Blunt"), damageModificator), true);

        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(targetId, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetId }, filter);
            popupPower = "good";
        }

        DoHeal(xeno, koComp, tracker);
        popupPower = DoUppercut(xeno, targetId, tracker);

        var messageOther = Loc.GetString("stories-xeno-boxer-strain-other-uppercut-" + popupPower,
        ("target", Identity.Entity(targetId, EntityManager)), ("boxer", Identity.Entity(xeno, EntityManager)));

        var messageSelf = Loc.GetString("stories-xeno-boxer-strain-self-uppercut-" + popupPower,
        ("target", Identity.Entity(targetId, EntityManager)), ("boxer", Identity.Entity(xeno, EntityManager)));

        _koSystem.ResetTracker(xeno, recently);
        _popup.PopupPredicted(messageSelf, messageOther, xeno, xeno, PopupType.LargeCaution);
        _rmcMelee.DoLunge(xeno, targetId);
        SpawnAttachedTo(comp.Effect, targetId.ToCoordinates());
        DoCooldown(xeno);
    }

    private void DoHeal(Entity<BoxerUppercutComponent> xeno, XenoBoxerKOComponent koComp, XenoBoxerKOTracker tracker)
    {
        if (!TryComp(xeno, out MobThresholdsComponent? mobThreshold))
            return;

        if (!_mobThresholdSystem.TryGetDeadThreshold(xeno, out var threshold, mobThreshold))
            return;

        var amount = threshold.Value *
        (Math.Clamp(tracker.Count, 0, koComp.MaxKO) * xeno.Comp.HealPerStack);

        var heal = -_rmcDamage.DistributeTypesTotal(xeno.Owner, amount);

        _damageable.TryChangeDamage(xeno, heal, true);
        SpawnAttachedTo(xeno.Comp.HealEffect, xeno.Owner.ToCoordinates());
    }

    private string DoUppercut(Entity<BoxerUppercutComponent> xeno, EntityUid target, XenoBoxerKOTracker tracker)
    {
        var origin = _transform.GetMapCoordinates(xeno);
        var targetCoordinates = _transform.GetMapCoordinates(target);
        var diff = targetCoordinates.Position - origin.Position;
        diff = diff.Normalized() * (tracker.Count / xeno.Comp.Range);
        _rmcPulling.TryStopAllPullsFromAndOn(target);

        if (tracker.Count <= 5)
        {
            _throwing.TryThrow(target, diff, 10);
            return "powerful";
        }
        else if (tracker.Count <= 10)
        {
            _throwing.TryThrow(target, diff, 10);
            _stun.TryParalyze(target, xeno.Comp.ParalyzeTime, true);
            return "gigantic";
        }
        else
        {
            _throwing.TryThrow(target, diff, 10);
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, xeno.Comp.StatusEffectKey,
                xeno.Comp.StatusEffectTime, false);
            _stun.TryParalyze(target, xeno.Comp.TitanicParalyzeTime, true);
            _audio.PlayEntity(xeno.Comp.GongSound, Filter.Entities(xeno), xeno, true);
            EnsureComp<KOLabelComponent>(target);
            Timer.Spawn(TimeSpan.FromSeconds(4), () =>
            {
                RemCompDeferred<KOLabelComponent>(target);
            });
            return "titanic";
        }

        return "weak";
    }

    private void DoCooldown(Entity<BoxerUppercutComponent> xeno)
    {
        foreach (var (actionId, action) in _action.GetActions(xeno))
        {
            if (action.BaseEvent is BoxerPunchActionEvent or BoxerJabActionEvent)
                _action.SetCooldown(actionId, xeno.Comp.Cooldown);
        }
    }
}
