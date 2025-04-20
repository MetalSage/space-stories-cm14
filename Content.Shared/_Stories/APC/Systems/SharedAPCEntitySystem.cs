using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Destructible;
using Content.Shared.Coordinates;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCEntityComponent, APCControlReturnActionEvent>(OnReturn);
        SubscribeLocalEvent<APCEntityComponent, GettingAPCControlledEvent>(OnGettingControlled);
        SubscribeLocalEvent<APCEntityComponent, BreakageEventArgs>(OnDestruction);

        InitializeController();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var entities = EntityQueryEnumerator<APCEntityComponent>();
        while (entities.MoveNext(out var uid, out var comp))
        {
            if (comp.User != null && comp.Controller != null && (!_mobStateSystem.IsAlive((EntityUid)comp.User) ||
            TryComp<SleepingComponent>((EntityUid)comp.User, out var _) ||
            TryComp<ForcedSleepingComponent>((EntityUid)comp.User, out var _))) Return(uid, comp);
        }
    }

    #region ControlAPC
    public void OnGettingControlled(EntityUid uid, APCEntityComponent component, GettingAPCControlledEvent args)
    {
        component.User = args.User;
        component.Controller = args.Controller;
    }

    public void OnReturn(EntityUid uid, APCEntityComponent component, APCControlReturnActionEvent args)
    {
        Return(uid, component);
    }

    public void Return(EntityUid uid, APCEntityComponent component)
    {
        if (TryComp<MindContainerComponent>(uid, out var mind))
        {
            if (mind.HasMind)
                TryReturnToBody(uid, component);
        }

        component.User = null;

        if (component.Controller != null)
        {
            RaiseLocalEvent((EntityUid)component.Controller, new ReturnToBodyAPCEvent(uid));
            component.Controller = null;
        }
    }

    public bool TryReturnToBody(EntityUid uid, APCEntityComponent component)
    {
        if (component.User != null)
        {
            _mindSystem.ControlMob(uid, (EntityUid)component.User);
            return true;
        }
        else return false;
    }

    #endregion
    #region DestroyAPC

    private void OnDestruction(EntityUid uid, APCEntityComponent component, BreakageEventArgs args)
    {
        DestroyAPC(uid, component);
    }

    public void DestroyAPC(EntityUid uid, APCEntityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        Return(uid, component);

        component.Destroyed = true;
        UpdateAppearance(uid, component);
    }

    public void UpdateAppearance(EntityUid uid, APCEntityComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        _appearance.SetData(uid, APCVisuals.Destroyed, component.Destroyed, appearance);
    }

    public bool TryEjectEntities(EntityUid uid, APCEntityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var apcGrid = _transform.GetGrid(uid);
        var apcMap = _transform.GetMap(uid);

        var gridEnt = apcGrid ?? apcMap;
        if (gridEnt == null)
            return false;

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var queryUid, out var transform))
        {
            if (transform.GridUid != component.GridEnt || transform.Anchored)
                continue;

            if (TerminatingOrDeleted(queryUid))
                continue;

            var coords = new EntityCoordinates(gridEnt.Value, _transform.GetWorldPosition(uid));
            _transform.SetCoordinates(queryUid, coords);
        }
        return true;

    }
    #endregion
}
