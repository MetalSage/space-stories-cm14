using Content.Shared._Stories.Xenonids.Boiler;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Server._Stories.Xenonids.Boiler;

public sealed class BoilerAcidAnimationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoilerAcidAnimationComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<BoilerAcidAnimationComponent, MindRemovedMessage>(OnMindRemoved);

        SubscribeNetworkEvent<BoilerAcidAnimationToggleEvent>(OnToggle);
    }

    private void OnDetached(Entity<BoilerAcidAnimationComponent> ent, ref PlayerDetachedEvent args)
    {
        SetActive(ent, false);
    }

    private void OnMindRemoved(Entity<BoilerAcidAnimationComponent> ent, ref MindRemovedMessage args)
    {
        SetActive(ent, false);
    }

    private void OnToggle(BoilerAcidAnimationToggleEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var boiler = GetEntity(ev.Boiler);
        if (boiler != user)
            return;

        if (!TryComp<BoilerAcidAnimationComponent>(boiler, out var comp))
            return;

        SetActive((boiler, comp), ev.Active);
    }

    private void SetActive(Entity<BoilerAcidAnimationComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent, ent.Comp);
    }
}
