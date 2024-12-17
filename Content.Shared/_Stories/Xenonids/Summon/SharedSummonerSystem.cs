using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.Summon;

public abstract class SharedSummonerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
   

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SummonerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SummonerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SummonerComponent, SummonerOrderActionEvent>(OnOrderAction);
        SubscribeLocalEvent<SummonerServantComponent, ComponentShutdown>(OnServantShutdown);
    }

    private void OnStartup(EntityUid uid, SummonerComponent component, ComponentStartup args)
    {
        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        _action.AddAction(uid, ref component.ActionRaiseArmyEntity, component.ActionRaiseArmy, component: comp);
        _action.AddAction(uid, ref component.ActionOrderStayEntity, component.ActionOrderStay, component: comp);
        _action.AddAction(uid, ref component.ActionOrderFollowEntity, component.ActionOrderFollow, component: comp);
        _action.AddAction(uid, ref component.ActionOrderAttackEntity, component.ActionOrderAttack, component: comp);
        _action.AddAction(uid, ref component.ActionOrderLooseEntity, component.ActionOrderLoose, component: comp);

        UpdateActions(uid, component);
    }

    private void OnShutdown(EntityUid uid, SummonerComponent component, ComponentShutdown args)
    {
        foreach (var servant in component.Servants)
        {
            if (TryComp(servant, out SummonerServantComponent? servantComp))
                servantComp.King = null;
        }

        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        _action.RemoveAction(uid, component.ActionRaiseArmyEntity, comp);
        _action.RemoveAction(uid, component.ActionOrderStayEntity, comp);
        _action.RemoveAction(uid, component.ActionOrderFollowEntity, comp);
        _action.RemoveAction(uid, component.ActionOrderAttackEntity, comp);
        _action.RemoveAction(uid, component.ActionOrderLooseEntity, comp);
    }

    private void OnOrderAction(EntityUid uid, SummonerComponent component, SummonerOrderActionEvent args)
    {
        if (component.CurrentOrder == args.Type)
            return;
        args.Handled = true;

        component.CurrentOrder = args.Type;
        Dirty(uid, component);

        DoCommandCallout(uid, component);
        UpdateActions(uid, component);
        UpdateAllServants(uid, component);
    }

    private void OnServantShutdown(EntityUid uid, SummonerServantComponent component, ComponentShutdown args)
    {
        if (TryComp(component.King, out SummonerComponent? SummonerComponent))
            SummonerComponent.Servants.Remove(uid);
    }

    private void UpdateActions(EntityUid uid, SummonerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _action.SetToggled(component.ActionOrderStayEntity, component.CurrentOrder == SummonerOrderType.Stay);
        _action.SetToggled(component.ActionOrderFollowEntity, component.CurrentOrder == SummonerOrderType.Follow);
        _action.SetToggled(component.ActionOrderAttackEntity, component.CurrentOrder == SummonerOrderType.Attack);
        _action.SetToggled(component.ActionOrderLooseEntity, component.CurrentOrder == SummonerOrderType.Loose);
        _action.StartUseDelay(component.ActionOrderStayEntity);
        _action.StartUseDelay(component.ActionOrderFollowEntity);
        _action.StartUseDelay(component.ActionOrderAttackEntity);
        _action.StartUseDelay(component.ActionOrderLooseEntity);
    }


    public void UpdateAllServants(EntityUid uid, SummonerComponent component)
    {
        foreach (var servant in component.Servants)
        {
            UpdateServantNpc(servant, component.CurrentOrder);
        }
    }

    public virtual void UpdateServantNpc(EntityUid uid, SummonerOrderType orderType)
    {

    }

    public virtual void DoCommandCallout(EntityUid uid, SummonerComponent component)
    {

    }
}
