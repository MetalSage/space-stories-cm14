using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerUppercut;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Interaction.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

public sealed class SharedBoxerKnockoutSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly List<EntityUid> _trackersToRemove = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoBoxerKnockoutRecentlyComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    public void UpdateKnockoutTracker(EntityUid ent, XenoBoxerKnockoutComponent comp, EntityUid target, float count)
    {
        float trackerCount = 0f;
        if (_net.IsServer)
        {
            var recently = EnsureComp<XenoBoxerKnockoutRecentlyComponent>(ent);
            var tracker = recently.Trackers.GetValueOrDefault(target);
            var time = _timing.CurTime;

            tracker.Count = Math.Min(tracker.Count + count, comp.MaxKnockout);
            tracker.Last = time;
            recently.Trackers[target] = tracker;
            trackerCount = tracker.Count;

            if (GetAuraColor(tracker.Count, out var color) && color is not null)
                _aura.GiveAura(ent, color.Value, comp.AuraDuration, comp.AuraOutline);


            Dirty(ent, recently);
        }

        if (trackerCount >= comp.MaxKnockout)
            _popup.PopupPredicted(Loc.GetString("stories-xeno-boxer-can-use-titanic-uppercut"),
            null, ent, ent, PopupType.LargeCaution);
    }

    public void ResetTracker(EntityUid ent, XenoBoxerKnockoutRecentlyComponent recently)
    {
        RemCompDeferred<AuraComponent>(ent);
        RemCompDeferred<XenoBoxerKnockoutRecentlyComponent>(ent);
        _popup.PopupPredicted(Loc.GetString("stories-xeno-boxer-reset-ko"), ent, null, PopupType.MediumCaution);
        recently.Trackers.Clear();
        Dirty(ent, recently);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoBoxerKnockoutRecentlyComponent>();
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
                ResetTracker(uid, recently);
            }
        }

        if (_net.IsClient)
            return;

        var knockoutQuery = EntityQueryEnumerator<KnockoutLabelComponent>();
        while (knockoutQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.ExpiresAt == null || time < comp.ExpiresAt)
                continue;

            RemCompDeferred<KnockoutLabelComponent>(uid);
        }
    }

    private bool GetAuraColor(float count, [NotNullWhen(true)] out Color? color)
    {
        color = null;

        if (count >= 10)
        {
            color = Color.Red;
            return true;
        }

        if (count >= 5)
        {
            color = Color.Yellow;
            return true;
        }

        return false;
    }

    private void OnAttackAttempt(Entity<XenoBoxerKnockoutRecentlyComponent> recently, ref AttackAttemptEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (recently.Comp.Trackers.TryGetValue(target, out var tracker) && tracker.Count != 0)
            args.Cancel();
    }
}