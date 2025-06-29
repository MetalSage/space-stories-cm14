using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Content.Shared.Bed.Sleep;
using Content.Shared.Stunnable;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._Stories.APC;
using Content.Shared.Mind.Components;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;
using Content.Shared._Stories.Attachables;
using Content.Shared.Interaction.Components;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCEntitySystem
{
    private void InitializeController()
    {
        SubscribeLocalEvent<APCPilotSeatComponent, MapInitEvent>(OnSeatInit);
        SubscribeLocalEvent<APCGunnerSeatComponent, MapInitEvent>(OnSeatInit);

        SubscribeLocalEvent<APCPilotSeatComponent, ComponentShutdown>(OnSeatShutdown);
        SubscribeLocalEvent<APCGunnerSeatComponent, ComponentShutdown>(OnSeatShutdown);

        SubscribeLocalEvent<APCPilotSeatComponent, StrappedEvent>(OnPilotSeatStrapped);
        SubscribeLocalEvent<APCGunnerSeatComponent, StrappedEvent>(OnGunnerSeatStrapped);
        SubscribeLocalEvent<APCPilotSeatComponent, UnstrappedEvent>(OnSeatUnstrapped);
        SubscribeLocalEvent<APCGunnerSeatComponent, UnstrappedEvent>(OnSeatUnstrapped);

        SubscribeLocalEvent<MarineComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnSeatInit<T>(Entity<T> seat, ref MapInitEvent args) where T : IComponent
    {
        if (!TryComp<TransformComponent>(seat, out var xform))
            return;

        if (!TryComp<APCEntityGridComponent>(xform.GridUid, out var apcGrid))
            return;

        switch (seat.Comp)
        {
            case APCPilotSeatComponent pilotSeat:
                pilotSeat.APC = GetEntity(apcGrid.APC);
                break;

            case APCGunnerSeatComponent gunnerSeat:
                gunnerSeat.APC = GetEntity(apcGrid.APC);
                break;
        }
    }

    private void OnSeatShutdown<T>(Entity<T> seat, ref ComponentShutdown args) where T : IComponent
    {
        switch (seat.Comp)
        {
            case APCPilotSeatComponent pilotSeat when pilotSeat.Pilot is not null:
                Return(pilotSeat.Pilot.Value);
                break;

            case APCGunnerSeatComponent gunnerSeat when gunnerSeat.Gunner is not null:
                Return(gunnerSeat.Gunner.Value);
                break;
        }
    }

    private void OnPilotSeatStrapped(Entity<APCPilotSeatComponent> seat, ref StrappedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out var eye))
            return;

        var pilot = EnsureComp<APCPilotComponent>(args.Buckle);

        pilot.APC = seat.Comp.APC;
        seat.Comp.Pilot = args.Buckle;

        if (seat.Comp.APC is null)
            return;

        _eye.SetTarget(args.Buckle, seat.Comp.APC, eye);
        _mover.SetRelay(args.Buckle, seat.Comp.APC.Value);   
    }

    private void OnGunnerSeatStrapped(Entity<APCGunnerSeatComponent> seat, ref StrappedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!IsConscious(args.Buckle, seat.Comp.Skills, out var eye))
            return;

        var gunner = EnsureComp<APCGunnerComponent>(args.Buckle);

        gunner.APC = seat.Comp.APC;
        seat.Comp.Gunner = args.Buckle;

        if (seat.Comp.APC is null)
            return;

        _eye.SetTarget(args.Buckle, seat.Comp.APC, eye);

        if (seat.Comp.Action is {} gunnerAction)
            gunner.ActionEntity = _actions.AddAction(args.Buckle, gunnerAction);

        if (TryComp<APCEntityComponent>(seat.Comp.APC, out var apc) && 
            apc.ActiveHardpoint is {} hardpoint &&
            TryComp<APCAttachableComponent>(hardpoint, out var attachable))
        {
            var irelay = EnsureComp<InteractionRelayComponent>(args.Buckle);
            _interaction.SetRelay(args.Buckle, hardpoint, irelay);
            _mover.SetRelay(args.Buckle, hardpoint);   
        }
        else
        {
            _popup.PopupEntity("Для начала выберите hardpoint", args.Buckle);
        }
    }

    private void OnSeatUnstrapped<T>(Entity<T> seat, ref UnstrappedEvent args) where T : IComponent
    {
        Return(args.Buckle);

        if (TryComp<APCGunnerComponent>(args.Buckle, out var gunner) && gunner.ActionEntity is not null)
            _actions.RemoveAction(args.Buckle, gunner.ActionEntity.Value);

        RemCompDeferred<T>(args.Buckle);
    }
    
    private bool IsConscious(EntityUid pilot, Dictionary<EntProtoId<SkillDefinitionComponent>, int> skills, [NotNullWhen(true)] out EyeComponent? eye)
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
        {
            _popup.PopupEntity("Dont require skills", pilot);
            return true;
        }
        
        if (!_skills.HasAllSkills(pilot, skills))
        {
            _popup.PopupEntity("No skills", pilot);
            return false;
        }

        return true;
    }

    private void OnMindRemoved(Entity<MarineComponent> marine, ref MindRemovedMessage args)
    {
        Return(marine);
    }

    private void Return(EntityUid target)
    {
        _eye.SetTarget(target, null);
        RemCompDeferred<RelayInputMoverComponent>(target);
        RemCompDeferred<InteractionRelayComponent>(target);
    }
}
