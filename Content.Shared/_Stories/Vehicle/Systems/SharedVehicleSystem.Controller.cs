using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._Stories.Attachables;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Content.Shared.Coordinates;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem
{
    private void InitializeController()
    {
        SubscribeLocalEvent<VehiclePilotSeatComponent, MapInitEvent>(OnSeatInit);
        SubscribeLocalEvent<VehiclePilotSeatComponent, ComponentShutdown>(OnSeatShutdown);
        SubscribeLocalEvent<VehiclePilotSeatComponent, StrappedEvent>(OnPilotSeatStrapped);
        SubscribeLocalEvent<VehiclePilotSeatComponent, UnstrappedEvent>(OnSeatUnstrapped);
        SubscribeLocalEvent<VehiclePilotSeatComponent, StrapAttemptEvent>(OnStrapAttempt);

        SubscribeLocalEvent<VehicleControllerComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<VehicleControllerComponent, MapInitEvent>(OnControllerInit);
        SubscribeLocalEvent<VehicleControllerComponent, ComponentShutdown>(OnControllerShutdown);
        SubscribeLocalEvent<VehicleControllerComponent, UnstrappedEvent>(OnControllerUnstrapped);

        SubscribeLocalEvent<VehicleControllerComponent, StrappedEvent>(OnVehicleControllerStrapped);

        SubscribeLocalEvent<VehiclePilotComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnSeatInit(Entity<VehiclePilotSeatComponent> seat, ref MapInitEvent args)
    {
        if (TryGetVehicle(seat, out var vehicle))
            seat.Comp.Vehicle = vehicle.Owner;
    }

    private void OnControllerInit(Entity<VehicleControllerComponent> controller, ref MapInitEvent args)
    {
        if (!TryGetVehicle(controller, out var vehicle))
            return;

        controller.Comp.Vehicle = vehicle.Owner;

        foreach (var hardpoint in vehicle.Comp.Hardpoints)
        {
            if (!TryComp<VehicleControllableComponent>(hardpoint, out var controllable))
                continue;

            if (controllable.Id == controller.Comp.Id)
            {
                controller.Comp.ControllableEntity = hardpoint;
                break;
            }
        }
    }

    private void OnSeatShutdown(Entity<VehiclePilotSeatComponent> seat, ref ComponentShutdown args)
    {
        if (seat.Comp.Pilot is { } pilot)
            Return(pilot);
    }

    private void OnSeatUnstrapped(Entity<VehiclePilotSeatComponent> seat, ref UnstrappedEvent args)
    {
        seat.Comp.Pilot = null;
        Return(args.Buckle);
    }

    private void OnControllerShutdown(Entity<VehicleControllerComponent> seat, ref ComponentShutdown args)
    {
        if (seat.Comp.Pilot is { } pilot)
            Return(pilot);
    }

    private void OnControllerUnstrapped(Entity<VehicleControllerComponent> seat, ref UnstrappedEvent args)
    {
        seat.Comp.Pilot = null;
        Return(args.Buckle);
    }

    private void OnStrapAttempt(Entity<VehiclePilotSeatComponent> seat, ref StrapAttemptEvent args)
    {
        if (seat.Comp.Vehicle == null)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out _))
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-skills-cant-operate", ("target", seat.Comp.Vehicle.Value)), 
                args.Buckle
            );
            args.Cancelled = true;
        }
    }

    private void OnStrapAttempt(Entity<VehicleControllerComponent> seat, ref StrapAttemptEvent args)
    {
        if (seat.Comp.Vehicle == null)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out _))
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-skills-cant-operate", ("target", seat.Comp.Vehicle.Value)), 
                args.Buckle
            );
            args.Cancelled = true;
        }
    }

    private void OnPilotSeatStrapped(Entity<VehiclePilotSeatComponent> seat, ref StrappedEvent args)
    {
        if (seat.Comp.Vehicle == null)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out var eye))
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-skills-cant-operate", ("target", seat.Comp.Vehicle)), 
                args.Buckle
            );
            return;
        }

        var pilot = EnsureComp<VehiclePilotComponent>(args.Buckle);
        pilot.Vehicle = seat.Comp.Vehicle;
        seat.Comp.Pilot = args.Buckle;

        if (seat.Comp.IsGunner)
            SetupGunnerSeat(seat, (args.Buckle, pilot), eye);
        else
            SetupPilotSeat(seat, args.Buckle, eye);
    }

    private void SetupGunnerSeat(Entity<VehiclePilotSeatComponent> seat, 
        Entity<VehiclePilotComponent> pilot, EyeComponent eye)
    {
        _eye.SetTarget(pilot, seat.Comp.Vehicle, eye);

        if (seat.Comp.Action is { } gunnerAction)
            pilot.Comp.ActionEntity = _actions.AddAction(pilot.Owner, gunnerAction);

        if (TryComp<VehicleComponent>(seat.Comp.Vehicle, out var vehicle) &&
            vehicle.ActiveHardpoint is { } hardpoint &&
            HasComp<VehicleAttachableComponent>(hardpoint))
        {
            var relay = EnsureComp<InteractionRelayComponent>(pilot);
            _interaction.SetRelay(pilot, hardpoint, relay);
            _mover.SetRelay(pilot, hardpoint);

            if (TryComp<VehicleGunComponent>(hardpoint, out var gun))
            {
                gun.User = pilot.Owner;
                Dirty(hardpoint, gun);
            }
        }
        else
        {
            _popup.PopupCursor("Для начала выберите точку крепления", pilot);
        }
    }

    private void SetupPilotSeat(Entity<VehiclePilotSeatComponent> seat, EntityUid pilotUid, EyeComponent eye)
    {
        _eye.SetTarget(pilotUid, seat.Comp.Vehicle, eye);
        _mover.SetRelay(pilotUid, seat.Comp.Vehicle!.Value);
    }

    private void OnVehicleControllerStrapped(Entity<VehicleControllerComponent> seat, ref StrappedEvent args)
    {
        if (seat.Comp.Vehicle == null)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out var eye))
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-skills-cant-operate", ("target", seat.Comp.Vehicle)), 
                args.Buckle
            );
            return;
        }

        var pilot = EnsureComp<VehiclePilotComponent>(args.Buckle);
        pilot.Vehicle = seat.Comp.Vehicle;
        seat.Comp.Pilot = args.Buckle;

        if (seat.Comp.ControllableEntity is not { } controllable)
            return;

        var relay = EnsureComp<InteractionRelayComponent>(args.Buckle);

        _eye.SetTarget(args.Buckle, seat.Comp.Vehicle, eye);
        _interaction.SetRelay(args.Buckle, controllable, relay);
        _mover.SetRelay(args.Buckle, controllable);

        if (seat.Comp.Action is { } reloadAction &&
            HasComp<VehiclePilotComponent>(args.Buckle))
        {
            pilot.ActionEntity = _actions.AddAction(args.Buckle, reloadAction);
        }

        if (TryComp<VehicleGunComponent>(controllable, out var gun))
        {
            gun.User = pilot.Owner;
            Dirty(controllable, gun);
            Logger.Error("Dirty called")
        }

    }

    public bool IsConscious(EntityUid pilot, Dictionary<EntProtoId<SkillDefinitionComponent>, int> skills, 
        [NotNullWhen(true)] out EyeComponent? eye)
    {
        eye = null;

        if (!TryComp(pilot, out EyeComponent? e))
            return false;

        if (!HasComp<SkillsComponent>(pilot))
            return false;

        if (HasComp<SleepingComponent>(pilot) || 
            HasComp<ForcedSleepingStatusEffectComponent>(pilot) || 
            HasComp<StunnedComponent>(pilot))
        {
            return false;
        }

        if (!_mobState.IsAlive(pilot))
            return false;

        eye = e;

        return skills.Count == 0 || _skills.HasAllSkills(pilot, skills);
    }

    private void OnMindRemoved(Entity<VehiclePilotComponent> pilot, ref MindRemovedMessage args)
    {
        Return(pilot);
    }

    public void Return(EntityUid target)
    {
        _eye.SetTarget(target, null);

        if (TryComp<VehiclePilotComponent>(target, out var pilot) && pilot.ActionEntity is { } action)
            _actions.RemoveAction(target, action);

        RemCompDeferred<VehiclePilotComponent>(target);
        RemCompDeferred<RelayInputMoverComponent>(target);
        RemCompDeferred<InteractionRelayComponent>(target);
    }
}
