using System.Numerics;
using Content.Shared._Stories.APC;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Map;
using Content.Shared.Coordinates;
using Content.Shared.FixedPoint;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem
{
    public void InitializeDoors()
    {
        SubscribeLocalEvent<APCDoorComponent, InteractHandEvent>(AfterInteract);
    }

    private void AfterInteract(Entity<APCDoorComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        Log.Debug("ExitDoafter1");
        var comp = ent.Comp;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.LeaveDelay, new LeaveAPCDoAfterEvent(), ent, target: args.Target, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        Log.Debug("ExitDoafter");
        args.Handled = true;
    }

    private void LeaveAPC(Entity<APCDoorComponent> ent, ref LeaveAPCDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        args.Handled = true;

        if (!TryComp<APCPilotComponent>(args.User, out var pilot))
            return;

        if (pilot.APC is not { } apcUid)
        {
            Log.Debug("is not");
            return;
        }

        if (!TryComp<APCEntityComponent>(apcUid, out var apcComp))
            return;
        Log.Debug("exit");
        ExitOnAPC(new(apcUid, apcComp), ent.Comp.Side, args.User);
    }

    public void ExitOnAPC(Entity<APCEntityComponent> target, APCEnterSide side, EntityUid user)
    {
        var targetFacingDirection = Transform(target).LocalRotation.GetCardinalDir();
        var targetPos = target.Owner.ToCoordinates();//_transform.ToCoordinates(targetTransform);
        Vector2 offset = side switch
        {
            APCEnterSide.Left => targetFacingDirection switch
            {
                Direction.North => new Vector2(-2, 0), // Запад от APC
                Direction.South => new Vector2(2, 0),  // Восток от APC
                Direction.East => new Vector2(0, 2),   // Север от APC
                Direction.West => new Vector2(0, -2),  // Юг от APC
                _ => Vector2.Zero
            },
            APCEnterSide.Right => targetFacingDirection switch
            {
                Direction.North => new Vector2(2, 0),  // Восток от APC
                Direction.South => new Vector2(-2, 0), // Запад от APC
                Direction.East => new Vector2(0, -2),  // Юг от APC
                Direction.West => new Vector2(0, 2),   // Север от APC
                _ => Vector2.Zero
            },
            _ => Vector2.Zero
        };

        EntityCoordinates newUserPosition = new EntityCoordinates(target, targetPos.Position + offset);
        HandleExitPulling(user, newUserPosition, target.Comp);
        Log.Debug("exit2");
    }


    private void HandleExitPulling(EntityUid user, EntityCoordinates coords, APCEntityComponent component)
    {
        if (TryComp(user, out PullableComponent? otherPullable) &&
            otherPullable.Puller != null)
        {
            _pulling.TryStopPull(user, otherPullable, otherPullable.Puller.Value);
        }

        if (TryComp(user, out PullerComponent? puller) &&
            TryComp(puller.Pulling, out PullableComponent? pullable))
        {
            if (TryComp(puller.Pulling, out PullerComponent? otherPullingPuller) &&
                TryComp(otherPullingPuller.Pulling, out PullableComponent? otherPullingPullable))
            {
                _pulling.TryStopPull(otherPullingPuller.Pulling.Value, otherPullingPullable, puller.Pulling);
            }

            var pulling = puller.Pulling.Value;

            if (HasComp<HumanoidAppearanceComponent>(pulling))
                component.OnAPC -= FixedPoint2.New(1);

            _pulling.TryStopPull(pulling, pullable, user);
            _transform.SetCoordinates(user, coords);
            _transform.SetCoordinates(pulling, coords);
            _pulling.TryStartPull(user, pulling);
            component.OnAPC -= FixedPoint2.New(1);
            RemCompDeferred<APCPilotComponent>(user);
            Log.Debug("TpHandle");
        }
        else
        {
            _transform.SetCoordinates(user, coords);
            component.OnAPC -= FixedPoint2.New(1);
            RemCompDeferred<APCPilotComponent>(user);
            Log.Debug("TpHandle");
        }
        Log.Debug("exit3");
    }
}
