using System.Linq;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Content.Shared.Bed.Sleep;
using Content.Shared.Stunnable;
using Content.Shared._RMC14.Marines.Skills;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCEntitySystem
{
    private void InitializeController()
    {
        SubscribeLocalEvent<APCPilotSeatComponent, MapInitEvent>(OnPilotSeatInit);
        SubscribeLocalEvent<APCPilotSeatComponent, StrappedEvent>(OnPilotSeatStrapped);
        SubscribeLocalEvent<APCPilotSeatComponent, UnstrappedEvent>(OnPilotSeatUnstrapped);
    }

    private void OnPilotSeatInit(Entity<APCPilotSeatComponent> seat, ref MapInitEvent args)
    {
        if (!TryComp<TransformComponent>(seat, out var xform))
            return;

        if (!TryComp<APCEntityGridComponent>(xform.GridUid, out var apcGrid))
            return;

        seat.Comp.APC = apcGrid.APC;
    }
/*
    private void OnShutdown(Entity<APCPilotSeatComponent> seat, ComponentShutdown args)
    {
    }
*/
    private void OnPilotSeatStrapped(Entity<APCPilotSeatComponent> seat, ref StrappedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(args.Buckle, out EyeComponent? eye))
            return;

        if (!TryComp<SkillsComponent>(args.Buckle, out var skillsComp))
            return;

        if (!_skills.HasAllSkills(args.Buckle.Owner, seat.Comp.Skills))
        {
            _popup.PopupEntity("a", args.Buckle);
            return;
        }

        var pilot = EnsureComp<APCPilotComponent>(args.Buckle);
        if (!IsConscious(args.Buckle))
            return;

        pilot.APC = seat.Comp.APC;

        if (seat.Comp.APC is null)
            return;

        _eye.SetTarget(args.Buckle, seat.Comp.APC, eye);
        _mover.SetRelay(args.Buckle, seat.Comp.APC.Value);   
    }

    private void OnPilotSeatUnstrapped(Entity<APCPilotSeatComponent> seat, ref UnstrappedEvent args)
    {
        _eye.SetTarget(args.Buckle, null);
        RemComp<RelayInputMoverComponent>(args.Buckle);
    }
    
    private bool IsConscious(EntityUid pilot)
    {
        if (HasComp<SleepingComponent>(pilot) 
            && HasComp<ForcedSleepingComponent>(pilot)
            && HasComp<StunnedComponent>(pilot))
        {
            return false;
        }

        if (!_mobState.IsAlive(pilot))
            return false;

        return true;
    }
}
