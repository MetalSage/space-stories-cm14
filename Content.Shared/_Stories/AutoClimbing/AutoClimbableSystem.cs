using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.AutoClimbing;

public sealed class AutoClimbableSystem : EntitySystem
{
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly INetConfigurationManager _netConfig = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _isColliding;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClimbableComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ClimbableComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnStartCollide(Entity<ClimbableComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState || _isColliding)
            return;

        if (!TryComp<AutoClimbableComponent>(ent, out var autoClimb) || 
            !TryComp<ClimbingComponent>(args.OtherEntity, out var climbing) || 
            climbing.IsClimbing || !climbing.CanClimb)
            return;

        if (HasComp<AutoClimbBlockedComponent>(args.OtherEntity) || _mobState.IsIncapacitated(args.OtherEntity))
            return;

        if (TryComp<ActorComponent>(args.OtherEntity, out var actor) &&
            !_netConfig.GetClientCVar(actor.PlayerSession.Channel, SCCVars.SCCVars.AutoClimb))
            return;

        var curTime = _timing.CurTime;
        if (curTime < autoClimb.LastCollideTime + TimeSpan.FromSeconds(autoClimb.CollideCooldown))
            return;

        _isColliding = true;
        autoClimb.LastCollideTime = curTime;
        _climb.TryClimb(args.OtherEntity, args.OtherEntity, ent, out _, ent.Comp);
    }

    private void OnEndCollide(Entity<ClimbableComponent> _, ref EndCollideEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState)
            return;

        _isColliding = false;
    }
}
