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

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCEntitySystem
{
    private void InitializeController()
    {
        SubscribeLocalEvent<VehiclePilotSeatComponent, MapInitEvent>(OnSeatInit);

        SubscribeLocalEvent<VehiclePilotSeatComponent, ComponentShutdown>(OnSeatShutdown);

        SubscribeLocalEvent<VehiclePilotSeatComponent, StrappedEvent>(OnPilotSeatStrapped);
        SubscribeLocalEvent<VehiclePilotSeatComponent, UnstrappedEvent>(OnSeatUnstrapped);

        SubscribeLocalEvent<MarineComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnSeatInit(Entity<VehiclePilotSeatComponent> seat, ref MapInitEvent args)
    {
        if (!TryGetVehicle(seat, out var vehicle))
            return;

        seat.Vehicle = GetEntity(vehicle);
    }

    private void OnSeatShutdown(Entity<VehiclePilotSeatComponent> seat, ref ComponentShutdown args)
    {
        Return(seat.Pilot);
    }

    private void OnPilotSeatStrapped(Entity<VehiclePilotSeatComponent> seat, ref StrappedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out var eye))
        {
            _popup.PopupEntity(Loc.GetString("rmc-skills-cant-operate", ("target", seat.Comp.Vehicle)), args.Buckle);
            return;
        }

        var pilot = EnsureComp<VehiclePilotComponent>(args.Buckle);

        pilot.Vehicle = seat.Comp.Vehicle;
        seat.Comp.Pilot = args.Buckle;

        if (seat.Comp.Vehicle is null)
            return;

        if (seat.Comp.IsGunner)
        {
            _eye.SetTarget(args.Buckle, seat.Comp.Vehicle, eye);

            if (seat.Comp.Action is { } gunnerAction)
                seat.Comp.ActionEntity = _actions.AddAction(args.Buckle, gunnerAction);

            if (TryComp<VehicleComponent>(seat.Comp.Vehicle, out var vehicle) &&
                vehicle.ActiveHardpoint is { } hardpoint &&
                HasComp<VehicleAttachableComponent>(hardpoint))
            {
                var irelay = EnsureComp<InteractionRelayComponent>(args.Buckle);
                _interaction.SetRelay(args.Buckle, hardpoint, irelay);
                _mover.SetRelay(args.Buckle, hardpoint);
            }
            else
            {
                _popup.PopupCoordinates("Для начала выберите hardpoint", vehicle.ToCoordinates(), args.Buckle);
            }
        }
        else
        {
            _eye.SetTarget(args.Buckle, seat.Comp.Vehicle, eye);
            _mover.SetRelay(args.Buckle, seat.Comp.Vehicle.Value);
        }
    }


    private void OnSeatUnstrapped(Entity<VehiclePilotSeatComponent> seat, ref UnstrappedEvent args)
    {
        Return(args.Buckle);
    }

    public bool IsConscious(EntityUid pilot, Dictionary<EntProtoId<SkillDefinitionComponent>, int> skills, [NotNullWhen(true)] out EyeComponent? eye)
    {
        if (!TryComp<EyeComponent>(pilot, out eye))
            return false;

        if (!HasComp<SkillsComponent>(pilot))
            return false;

        if (HasComp<SleepingComponent>(pilot)
            || HasComp<ForcedSleepingComponent>(pilot)
            || HasComp<StunnedComponent>(pilot))
        {
            return false;
        }

        if (!_mobState.IsAlive(pilot))
            return false;

        if (skills.Count == 0)
            return true;

        if (!_skills.HasAllSkills(pilot, skills))
            return false;

        return true;
    }

    private void OnMindRemoved(Entity<MarineComponent> marine, ref MindRemovedMessage args)
    {
        Return(marine);
    }

    public void Return(EntityUid target)
    {
        _eye.SetTarget(target, null);

        if (TryComp<VehiclePilotComponent>(target, out var gunner) && gunner.ActionEntity is not null)
            _actions.RemoveAction(target, gunner.ActionEntity.Value);

        RemCompDeferred<VehiclePilotComponent>(target);
        RemCompDeferred<RelayInputMoverComponent>(target);
        RemCompDeferred<InteractionRelayComponent>(target);
    }
}
