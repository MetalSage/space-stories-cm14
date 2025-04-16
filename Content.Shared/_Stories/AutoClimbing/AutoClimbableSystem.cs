using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Shared._Stories.AutoClimbing;

public sealed class AutoClimbableSystem : EntitySystem
{
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly INetConfigurationManager _netConfig = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    
    private bool _collide = false;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClimbableComponent, StartCollideEvent>(StartCollide);
        SubscribeLocalEvent<ClimbableComponent, EndCollideEvent>(EndCollide);
    }

    private void StartCollide(Entity<ClimbableComponent> ent, ref StartCollideEvent args)
    {
        if (_collide)
            return;

        if (TryComp(args.OtherEntity, out ActorComponent? actor) &&
            !_netConfig.GetClientCVar(actor.PlayerSession.Channel, SCCVars.SCCVars.AutoClimb))
        {
            return;
        }

        if (HasComp<AutoClimbBlockedComponent>(args.OtherEntity) || _mobState.IsIncapacitated(args.OtherEntity))
            return;

        if (!HasComp<AutoClimbableComponent>(ent) || !TryComp<ClimbableComponent>(ent, out var climb))
            return;

        if (!TryComp(args.OtherEntity, out ClimbingComponent? climbingComponent) || climbingComponent.IsClimbing || !climbingComponent.CanClimb)
            return;

        _collide = true;
        _climb.TryClimb(args.OtherEntity, args.OtherEntity, args.OurEntity, out _, climb);

    }

    private void EndCollide(Entity<ClimbableComponent> ent, ref EndCollideEvent args)
    {
        _collide = false;
    }
}
