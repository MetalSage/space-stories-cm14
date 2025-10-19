using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared._Stories.Attachables;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

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

        SubscribeLocalEvent<VehicleControllerComponent, StrapAttemptEvent>(OnControllerStrapAttempt);
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
        TryInitController(controller);
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
        if (seat.Comp.IsGunner && HasComp<SynthComponent>(args.Buckle))
        {
            _popup.PopupEntity(Loc.GetString("st-vehicle-synth-no-heavy-weapons"), args.Buckle);
            args.Cancelled = true;
            return;
        }

        if (!TryValidateStrap(args.Buckle, seat.Comp.Vehicle, seat.Comp.Skills))
            args.Cancelled = true;
    }

    private void OnControllerStrapAttempt(Entity<VehicleControllerComponent> controller, ref StrapAttemptEvent args)
    {
        if (!TryValidateStrap(args.Buckle, controller.Comp.Vehicle, controller.Comp.Skills))
            args.Cancelled = true;
    }

    private bool TryValidateStrap(EntityUid buckle, EntityUid? vehicle, Dictionary<EntProtoId<SkillDefinitionComponent>, int> skills)
    {
        if (vehicle == null)
            return true;

        if (!IsConscious(buckle, skills, out _))
        {
            _popup.PopupEntity(Loc.GetString("rmc-skills-cant-operate", ("target", vehicle.Value)), buckle);
            return false;
        }

        return true;
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

        pilot.StoredZoom = eye.Zoom;

        _contentEye.SetZoom(args.Buckle, pilot.StoredZoom * seat.Comp.Zoom, true);

        if (seat.Comp.IsGunner)
            SetupGunnerSeat(seat, (args.Buckle, pilot), eye);
        else
            SetupPilotSeat(seat, (args.Buckle, pilot), eye);

        foreach (var (ent, key) in _ui.GetActorUis(args.Buckle.Owner))
        {
            if (key is not (VehicleWeaponLoaderUI or VehicleSelectHardpointUI or VehicleStatusUI))
                continue;

            _ui.CloseUi(args.Buckle.Owner, key);
        }
    }

    private void SetupGunnerSeat(Entity<VehiclePilotSeatComponent> seat,
        Entity<VehiclePilotComponent> pilot, EyeComponent eye)
    {
        _eye.SetTarget(pilot, seat.Comp.Vehicle, eye);

        AddActions(pilot, seat.Comp.ActionIds);

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
                pilot.Comp.Gun = hardpoint;
                Dirty(hardpoint, gun);
                Dirty(pilot);
            }
        }
        else
        {
            _popup.PopupCursor(Loc.GetString("st-vehicle-select-hardpoint-prompt"), pilot);
        }
    }

    private void SetupPilotSeat(Entity<VehiclePilotSeatComponent> seat,
        Entity<VehiclePilotComponent> pilot, EyeComponent eye)
    {
        _eye.SetTarget(pilot, seat.Comp.Vehicle, eye);
        _mover.SetRelay(pilot, seat.Comp.Vehicle!.Value);

        AddActions(pilot, seat.Comp.ActionIds);
    }

    private void OnVehicleControllerStrapped(Entity<VehicleControllerComponent> seat, ref StrappedEvent args)
    {
        if (seat.Comp.Vehicle == null)
            return;

        if (seat.Comp.ControllableEntity is null && !TryInitController(seat))
            return;

        var controllable = seat.Comp.ControllableEntity!.Value;

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

        var relay = EnsureComp<InteractionRelayComponent>(args.Buckle);

        _eye.SetTarget(args.Buckle, seat.Comp.Vehicle, eye);
        _interaction.SetRelay(args.Buckle, controllable, relay);
        _mover.SetRelay(args.Buckle, controllable);

        AddActions((args.Buckle, pilot), seat.Comp.ActionIds);

        if (TryComp<VehicleGunComponent>(controllable, out var gun))
        {
            gun.User = pilot.Owner;
            pilot.Gun = controllable;
            Dirty(controllable, gun);
            Dirty(args.Buckle, pilot);
        }
    }

    private void AddActions(Entity<VehiclePilotComponent> pilot, List<EntProtoId> actionIds)
    {
        foreach (var actionId in actionIds)
        {
            var actionEntity = _actions.AddAction(pilot, actionId);
            if (actionEntity != null)
            {
                pilot.Comp.Actions[actionId] = actionEntity.Value;

                if (_actions.GetEvent(actionEntity.Value) is VehicleLockDoorsEvent &&
                    TryComp<VehicleComponent>(pilot.Comp.Vehicle, out var vehicle))
                {
                    var icon = vehicle.Locked
                        ? new SpriteSpecifier.Rsi(new ResPath("/Textures/_Stories/Actions/vehicle_actions.rsi"), "door_locked")
                        : new SpriteSpecifier.Rsi(new ResPath("/Textures/_Stories/Actions/vehicle_actions.rsi"), "door_unlocked");

                    _actions.SetIcon(actionEntity.Value, icon);

                }
            }
        }
    }

    public bool IsConscious(EntityUid pilot, Dictionary<EntProtoId<SkillDefinitionComponent>, int> skills,
        [NotNullWhen(true)] out EyeComponent? eye)
    {
        eye = null;

        if (!TryComp(pilot, out EyeComponent? e))
            return false;

        if (skills.Count > 0 && !HasComp<SkillsComponent>(pilot))
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

        if (TryComp<VehiclePilotComponent>(target, out var pilot))
        {
            _contentEye.SetZoom(target, pilot.StoredZoom, true);

            foreach (var (_, action) in pilot.Actions)
            {
                _actions.RemoveAction(target, action);
            }
        }

        foreach (var (ent, key) in _ui.GetActorUis(target))
        {
            if (key is not (VehicleWeaponLoaderUI or VehicleSelectHardpointUI or VehicleStatusUI))
                continue;

            _ui.CloseUi(target, key);
        }

        RemCompDeferred<VehiclePilotComponent>(target);
        RemCompDeferred<RelayInputMoverComponent>(target);
        RemCompDeferred<InteractionRelayComponent>(target);
    }

    private bool TryInitController(Entity<VehicleControllerComponent> controller)
    {
        if (!TryGetVehicle(controller, out var vehicle))
            return false;

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

        Dirty(controller);
        return true;
    }
}
