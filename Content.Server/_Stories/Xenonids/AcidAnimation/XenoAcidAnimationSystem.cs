using Content.Shared._Stories.Xenonids.AcidAnimation;
using Content.Shared.Actions.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Xenonids.AcidAnimation;

public sealed class XenoAcidAnimationSystem : SharedXenoAcidAnimationSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoAcidAnimationComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<XenoAcidAnimationComponent, MindRemovedMessage>(OnMindRemoved);

        SubscribeNetworkEvent<XenoAcidAnimationToggleEvent>(OnToggle);
    }

    private void OnDetached(Entity<XenoAcidAnimationComponent> ent, ref PlayerDetachedEvent args)
    {
        SetActive(ent, false);
    }

    private void OnMindRemoved(Entity<XenoAcidAnimationComponent> ent, ref MindRemovedMessage args)
    {
        SetActive(ent, false);
    }

    private void OnToggle(XenoAcidAnimationToggleEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var xeno = GetEntity(ev.Xeno);
        if (xeno != user || !TryComp<XenoAcidAnimationComponent>(xeno, out var comp))
            return;

        if (comp.Active == ev.Active)
            return;

        if (!CanToggle((xeno, comp), ev.Action, ev.Active))
            return;

        SetActive((xeno, comp), ev.Active);
    }

    private bool CanToggle(Entity<XenoAcidAnimationComponent> xeno, NetEntity netAction, bool active)
    {
        if (!active)
            return true;

        if (_timing.CurTime < xeno.Comp.NextToggleAt)
            return false;

        var action = GetEntity(netAction);
        if (!TryComp<ActionComponent>(action, out var actionComp) ||
            actionComp.AttachedEntity != xeno.Owner ||
            !IsAcidAnimationAction(action, xeno.Comp))
        {
            return false;
        }

        xeno.Comp.NextToggleAt = _timing.CurTime + TimeSpan.FromSeconds(xeno.Comp.ToggleRateLimit);
        return true;
    }

    private void SetActive(Entity<XenoAcidAnimationComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent, ent.Comp);
    }
}
