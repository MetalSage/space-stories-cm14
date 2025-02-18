namespace Content.Shared._Stories.Xenonids.WarriorBoxer;

using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

public sealed class SharedKOCounterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;

        var koQuery = EntityQueryEnumerator<KOComponent>();
        while (koQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.LastHitTarget == null)
                continue;

            if ((time - comp.LastHitTime) >= comp.KOResetTime)
                ResetKO(uid, comp);
        }
    }

    public void ResetKO(EntityUid uid, KOComponent? component = null)
    {
        if (component != null)
        {
            component.KOCounter = 0f;
            component.LastHitTarget = null;
        }
        else
        {
            if (!TryComp<KOComponent>(uid, out var koComp))
                return;

            koComp.KOCounter = 0f;
            koComp.LastHitTarget = null;
        }
    }
}
