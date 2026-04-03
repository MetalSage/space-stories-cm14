using Content.Shared._Stories.Xenonids.AcidAnimation;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Server._Stories.Xenonids.AcidAnimation;

public sealed class XenoAcidAnimationSystem : EntitySystem
{
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
        if (xeno != user)
            return;

        if (!TryComp<XenoAcidAnimationComponent>(xeno, out var comp))
            return;

        SetActive((xeno, comp), ev.Active);
    }

    private void SetActive(Entity<XenoAcidAnimationComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent, ent.Comp);
    }
}
