using Content.Shared._RMC14.StatusEffect;
using Content.Shared._Stories.Hunter.Marking;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Hunter;

public abstract class SharedHunterSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCStatusEffectSystem _rmcStatusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterComponent, ComponentStartup>(OnHunterStartup);
        SubscribeLocalEvent<HunterComponent, MobStateChangedEvent>(OnHunterMobStateChanged);
        SubscribeLocalEvent<HunterComponent, UpdateHunterMarkEvent>(OnUpdateHunterMark);
    }

    private void OnHunterStartup(Entity<HunterComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream))
            _bloodstream.ChangeBloodReagent((ent.Owner, bloodstream), "STHunterBlood");
        _rmcStatusEffects.GiveStunResistance(ent.Owner, ent.Comp.StunResistance);

        _actions.AddAction(ent.Owner, ref ent.Comp.OpenMarkPanelAction, ent.Comp.OpenMarkPanelActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.MarkForHuntAction, ent.Comp.MarkForHuntActionId);

        if (ent.Comp.AddComponents != null)
            _entityManager.AddComponents(ent.Owner, ent.Comp.AddComponents);

        if (ent.Comp.RemoveComponents != null)
            _entityManager.RemoveComponents(ent.Owner, ent.Comp.RemoveComponents);
    }

    private void OnHunterMobStateChanged(Entity<HunterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var ev = new HunterDiedEvent(ent.Comp.Prey, ent.Comp.Thrall);
        RaiseLocalEvent(ent.Owner, ev);

        ent.Comp.Prey = null;
        ent.Comp.Thrall = null;
        Dirty(ent);
    }

    private void OnUpdateHunterMark(Entity<HunterComponent> hunter, ref UpdateHunterMarkEvent args)
    {
        switch (args.Type)
        {
            case UpdateMarkType.Prey:
                hunter.Comp.Prey = args.Target;
                break;
            case UpdateMarkType.Thrall:
                hunter.Comp.Thrall = args.Target;
                break;
        }

        Dirty(hunter);
    }
}
