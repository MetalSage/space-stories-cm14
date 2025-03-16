using System.Linq;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mind;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCEntitySystem
{
    private void InitializeController()
    {
        SubscribeLocalEvent<APCControllerComponent, InteractHandEvent>(AfterInteract);
        SubscribeLocalEvent<APCControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<APCControllerComponent, ReturnToBodyAPCEvent>(OnReturn);

        SubscribeNetworkEvent<RequestControlAPCEvent>(OnRequestControlAPC);
    }

    public void OnReturn(EntityUid uid, APCControllerComponent component, ReturnToBodyAPCEvent args)
    {
        component.CurrentUser = null;
        component.CurrentAPC = null;
    }

    private void AfterInteract(EntityUid uid, APCControllerComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        ControlAPC(uid, component, args.User);
        args.Handled = true;
    }

    public void ControlAPC(EntityUid uid, APCControllerComponent component, EntityUid user)
    {
        if (TryComp<GhostComponent>(user, out var _))
            return;

        if (!TryComp<APCPilotComponent>(user, out var pilot))
            return;

        var target = pilot.APC;
        if (target == null)
            return;

        component.CurrentUser = user;
        component.CurrentAPC = target;
        RaiseLocalEvent(target.Value, new GettingAPCControlledEvent(user, uid));
        _mindSystem.ControlMob(user, target.Value);
    }

    public void OnShutdown(EntityUid uid, APCControllerComponent component, ComponentShutdown args)
    {
        if (component.CurrentUser != null && component.CurrentAPC != null)
        {
            if (!TryComp<APCEntityComponent>(component.CurrentAPC, out var apcComp))
                return;

            if (apcComp.User != null)
                _mindSystem.ControlMob((EntityUid)component.CurrentAPC, (EntityUid)component.CurrentAPC);
        }
    }

    #region Client UI Control

    public void RequestControlAPC(EntityUid uid, EntityUid user)
    {
        if (!uid.IsValid() || !user.IsValid())
            return;

        RaiseNetworkEvent(new RequestControlAPCEvent(GetNetEntity(uid), GetNetEntity(user)));
    }

    private void OnRequestControlAPC(RequestControlAPCEvent ev, EntitySessionEventArgs args)
    {
        EntityUid apcController = GetEntity(ev.APCController);
        EntityUid user = GetEntity(ev.User);

        if (!TryComp<APCControllerComponent>(apcController, out var controller))
            return;

        ControlAPC(apcController, controller, user);
    }

    #endregion
}
