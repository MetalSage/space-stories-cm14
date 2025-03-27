using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;

public sealed class BoxerJabSystem : EntitySystem
{
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedBoxerKOSystem _koSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerJabComponent, BoxerJabActionEvent>(OnJabAction);
    }

    private void OnJabAction(Entity<BoxerJabComponent> xeno, ref BoxerJabActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        args.Handled = true;

        if (!TryComp<XenoBoxerKOComponent>(xeno, out var koComp))
            return;

        var comp = xeno.Comp;

        _rmcMelee.DoLunge(xeno, args.Target);
        _slow.TryRoot(args.Target, comp.RootTime);

        var messageSelf = Loc.GetString("stories-xeno-boxer-jab-self-message", ("target", Identity.Entity(args.Target, EntityManager)));
        var messageOther = Loc.GetString("stories-xeno-boxer-jab-other-message", ("target", Identity.Entity(args.Target, EntityManager)), ("boxer", Identity.Entity(xeno, EntityManager)));
        _popup.PopupPredicted(messageSelf, messageOther, xeno, xeno);

        if (_net.IsServer)
        {
            SpawnAttachedTo(comp.RootEffect, args.Target.ToCoordinates());
            _audio.PlayPvs(comp.Sound, xeno);
        }

        _koSystem.UpdateKOTracker(xeno, koComp, args.Target, comp.KOIncrease);

        if (!TryComp<XenoBoxerKORecentlyComponent>(xeno, out var recently))
            return;
        var tracker = recently.Trackers.GetValueOrDefault(args.Target);

        foreach (var (actionId, action) in _action.GetActions(xeno))
        {
            if (action.BaseEvent is BoxerPunchActionEvent && tracker.Count != koComp.MaxKO)
                _action.SetCooldown(actionId, comp.Cooldown);
        }
    }
}
