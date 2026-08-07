using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Xenonids.DeployTrap;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.AcidMine;

public sealed class RMCXenoDeployAcidMineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _demageable = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private EntityQuery<RMCXenoAcidMineImmuneComponent> _immuneQuery;
    private EntityQuery<BarricadeComponent> _barricadeQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        _immuneQuery = GetEntityQuery<RMCXenoAcidMineImmuneComponent>();
        _barricadeQuery = GetEntityQuery<BarricadeComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<RMCXenoDeployAcidMineComponent, RMCXenoDeployAcidMineEvent>(OnDeploy);
        SubscribeLocalEvent<RMCXenoAcidMineComponent, ComponentInit>(OnMineInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RMCXenoAcidMineComponent>();
        while (query.MoveNext(out var uid, out var mineComponent))
        {
            if (_timing.CurTime < mineComponent.Activation || mineComponent.Activation == TimeSpan.Zero)
                continue;

            OnMineActivate((uid, mineComponent));
        }
    }

    private void OnMineInit(Entity<RMCXenoAcidMineComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Activation = _timing.CurTime + ent.Comp.Delay;
        DirtyField(ent.Owner, ent.Comp, nameof(RMCXenoAcidMineComponent.Activation));
    }

    private void OnMineActivate(Entity<RMCXenoAcidMineComponent> ent)
    {
        if (ent.Comp.Activated)
            return;

        var hits = 0;
        foreach (var targetUid in _lookup.GetEntitiesIntersecting(ent))
        {
            if (_immuneQuery.HasComp(targetUid))
                continue;

            var barricade = _barricadeQuery.HasComp(targetUid);
            var mob = _mobStateQuery.HasComp(targetUid);

            if (!barricade && !mob)
                continue;

            if (barricade)
            {
                _demageable.TryChangeDamage(targetUid, ent.Comp.BarricadeDamage);
            }

            if (mob && !_hive.FromSameHive(ent.Owner, targetUid) && !_mobState.IsDead(targetUid))
            {
                _demageable.TryChangeDamage(targetUid, ent.Comp.Damage);
                if (ent.Comp.Add is { } add)
                    EntityManager.AddComponents(targetUid, add);

                hits++;
            }

            EnsureComp<RMCXenoAcidMineImmuneComponent>(targetUid);
            Timer.Spawn(TimeSpan.FromSeconds(0.2f),
                () =>
                {
                    RemCompDeferred<RMCXenoAcidMineImmuneComponent>(targetUid);
                }
            );
        }

        if (hits != 0 && ent.Comp.Attached is not null)
        {
            foreach (var (actionId, action) in _actions.GetActions(ent.Comp.Attached.Value))
            {
                var actionEvent = _actions.GetEvent(ent.Comp.Attached.Value);

                if (actionEvent is not RMCXenoDeployTrapsActionEvent)
                    continue;

                if (action.Cooldown is null)
                    continue;

                if (action.Cooldown.Value.Start >= action.Cooldown.Value.End - ent.Comp.ReduceDelayPerHit * hits)
                {
                    _actions.ClearCooldown(actionId);
                    break;
                }

                _actions.SetCooldown(actionId, action.Cooldown.Value.Start, action.Cooldown.Value.End - ent.Comp.ReduceDelayPerHit * hits);
                break;
            }
        }

        ent.Comp.Activated = true;
        DirtyField(ent, ent.Comp, nameof(RMCXenoAcidMineComponent.Activated));

        if (_net.IsClient && !IsClientSide(ent))
            return;

        QueueDel(ent);
    }

    private void OnDeploy(Entity<RMCXenoDeployAcidMineComponent> ent, ref RMCXenoDeployAcidMineEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        var prototypeId = TryUseEmpower((ent, ent)) ? ent.Comp.EmpoweredPrototypeId : ent.Comp.PrototypeId;

        if (_net.IsClient)
            return;

        var center = args.Target.Position.Floored() + Vector2.One / 2;
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var mineUid = Spawn(prototypeId, new EntityCoordinates(args.Target.EntityId, center + new Vector2(x, y)));
                var mineComponent = EnsureComp<RMCXenoAcidMineComponent>(mineUid);

                mineComponent.Attached = ent;
                DirtyField(mineUid, mineComponent, nameof(RMCXenoAcidMineComponent.Attached));

                _hive.SetSameHive(ent.Owner, mineUid);
            }
        }
    }

    public void Empower(Entity<RMCXenoDeployAcidMineComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.Empowered)
            return;

        ent.Comp.Empowered = true;
        _popup.PopupPredicted(Loc.GetString(ent.Comp.EmpoweredMessage), ent, null, PopupType.SmallCaution);
    }

    private bool TryUseEmpower(Entity<RMCXenoDeployAcidMineComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!ent.Comp.Empowered)
            return false;

        ent.Comp.Empowered = false;
        DirtyField(ent.Owner, ent.Comp, nameof(RMCXenoDeployAcidMineComponent.Empowered));
        return true;
    }
}
