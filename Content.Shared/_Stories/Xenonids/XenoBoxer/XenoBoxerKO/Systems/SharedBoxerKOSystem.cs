using Content.Shared._RMC14.Aura;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

public sealed class SharedBoxerKOSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly List<EntityUid> _trackersToRemove = new();

    public void UpdateKOTracker(EntityUid ent, XenoBoxerKOComponent comp, EntityUid target, float koPoint)
    {
        var time = _timing.CurTime;
        var recently = EnsureComp<XenoBoxerKORecentlyComponent>(ent);
        var tracker = recently.Trackers.GetValueOrDefault(target);

        if (tracker.Count >= comp.MaxKO)
        {
            _popup.PopupPredicted($"Вы готовы нанести сокрушительный удар!", ent, null, PopupType.LargeCaution);
            return;
        }

        tracker.Count = Math.Min(tracker.Count + koPoint, comp.MaxKO);
        tracker.Last = time;
        recently.Trackers[target] = tracker;
        Dirty(ent, recently);

        if (_net.IsClient)
            return;

        comp.AuraColor = GetAuraColor(tracker.Count, comp.MaxKO);
        if (comp.AuraColor.HasValue)
            _aura.GiveAura(ent, comp.AuraColor.Value, comp.AuraDuration);

        //_popup.PopupPredicted($"{tracker.Count}", ent, null, PopupType.MediumCaution);
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
                {
                    _trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (var id in _trackersToRemove)
            {
                recently.Trackers.Remove(id);
            }

            if (recently.Trackers.Count == 0)
            {
                RemCompDeferred<XenoBoxerKORecentlyComponent>(uid);
                RemCompDeferred<AuraComponent>(uid);
                _popup.PopupPredicted($"Ваше тело слабеет и сбрасывает комбо!", uid, null, PopupType.MediumCaution);
            }
        }
    }

    private Color? GetAuraColor(float count, float maxKO)
    {
        if (count >= maxKO)
            return Color.Red;
        if (count >= maxKO / 2f)
            return Color.Yellow;

        return null;
    }
}
