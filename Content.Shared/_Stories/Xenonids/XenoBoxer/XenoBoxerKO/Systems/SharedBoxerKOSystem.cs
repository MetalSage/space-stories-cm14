using Content.Shared._RMC14.Aura;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.IdentityManagement;
using Content.Shared._RMC14.Xenonids;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

public sealed class SharedBoxerKOSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private readonly List<EntityUid> _trackersToRemove = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoBoxerKOComponent, MeleeHitEvent>(OnMeleeHit);
    }
    public void UpdateKOTracker(EntityUid ent, XenoBoxerKOComponent comp, EntityUid target, float koPoint)
    {
        if (_net.IsClient)
            return;

        var recently = EnsureComp<XenoBoxerKORecentlyComponent>(ent);
        var tracker = recently.Trackers.GetValueOrDefault(target);
        var time = _timing.CurTime;

        tracker.Count = Math.Min(tracker.Count + koPoint, comp.MaxKO);
        tracker.Last = time;
        recently.Trackers[target] = tracker;
        comp.AuraColor = GetAuraColor(tracker.Count, comp.MaxKO);

        if (comp.AuraColor.HasValue)
            _aura.GiveAura(ent, comp.AuraColor.Value, comp.AuraDuration);

        if (tracker.Count >= comp.MaxKO)
            _popup.PopupPredicted(Loc.GetString("xeno-boxer-can-use-titanic-uppercut"), ent, null, PopupType.LargeCaution);

        Dirty(ent, recently);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoBoxerKORecentlyComponent>();
        while (query.MoveNext(out var uid, out var recently))
        {
            _trackersToRemove.Clear();
            foreach (var tracker in recently.Trackers)
            {
                if (time >= tracker.Value.Last + recently.ExpireAfter)
                    _trackersToRemove.Add(tracker.Key);
            }

            foreach (var id in _trackersToRemove)
            {
                recently.Trackers.Remove(id);
            }

            if (recently.Trackers.Count == 0)
            {
                RemCompDeferred<XenoBoxerKORecentlyComponent>(uid);
                RemCompDeferred<AuraComponent>(uid);
                _popup.PopupPredicted(Loc.GetString("xeno-boxer-reset-ko"), uid, null, PopupType.MediumCaution);
            }
        }
    }

    private Color? GetAuraColor(float count, float maxKO)
    {
        if (count >= 10)
            return Color.Red;
        if (count >= 5)
            return Color.Yellow;

        return null;
    }

    private void OnMeleeHit(Entity<XenoBoxerKOComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno.Owner, hit))
                continue;

            UpdateKOTracker(xeno.Owner, xeno.Comp, hit, xeno.Comp.KOIncreasePerMeleeHit);
            break;
        }
    }
}
