using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using System.Numerics;
using Robust.Server.Maps;
using Robust.Shared.Console;
using Content.Shared._Stories.APC;
using Content.Shared._Stories.APC.Systems;
using Robust.Server.GameObjects;
using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Actions;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAPCEntitySystem _sharedAPCEntitySystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCEntityComponent, DragDropTargetEvent>(DragAndDropOnAPC);
        SubscribeLocalEvent<APCEntityComponent, EnterAPCDoAfterEvent>(DragFinishedOnAPCEntity);
        SubscribeLocalEvent<APCEntityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<APCEntityComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<APCEntityComponent, CanDropTargetEvent>(OnCanDragDrop);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var apcQuery = EntityQueryEnumerator<APCEntityComponent>();
        while (apcQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.Destroyed)
                _sharedAPCEntitySystem.DestroyAPC(uid, comp);
        }
    }

    private void OnMapInit(EntityUid uid, APCEntityComponent component, MapInitEvent args)
    {
        string _gridPath = component.GridPath;

        if (!_resourceManager.TryContentFileRead(_gridPath, out var _))
        {
            Logger.Warning($"No map found: {_gridPath}");
            return;
        }

        _actionsSystem.AddAction(uid, ref component.APCControlReturnActEntity, component.APCControlReturnAction);
        _sharedAPCEntitySystem.UpdateAppearance(uid, component);

        if (component.GridEnt != null || component.MapEnt != null)
        {
            Logger.Warning($"APCEntity {uid} already has a grid or map entity.");
            return;
        }

        LoadMap(uid, component, _gridPath);
    }

    public void LoadMap(EntityUid uid, APCEntityComponent component, string _gridPath)
    {
        var mapId = _mapManager.CreateMap();
        _metaDataSystem.SetEntityName(_mapManager.GetMapEntityId(mapId), $"APCEntity Map: {uid}");
        var gridOptions = new MapLoadOptions();
        gridOptions.Offset = new Vector2(0, 0);
        gridOptions.Rotation = Angle.FromDegrees(0);
        var grids = _mapLoader.LoadMap(mapId, _gridPath, gridOptions);

        if (grids.Count != 0)
        {
            _metaDataSystem.SetEntityName(grids[0], $"APCEntity Grid: {uid}");
            component.GridEnt = grids[0];
        }

        component.MapEnt = _mapManager.GetMapEntityIdOrThrow(mapId);

        component.APC = uid;
    }

    private void OnComponentShutdown(EntityUid uid, APCEntityComponent component, ComponentShutdown args)
    {
        _sharedAPCEntitySystem.Return(uid, component);
        _sharedAPCEntitySystem.TryEjectEntities(uid, component);
        _actionsSystem.RemoveAction(component.APCControlReturnActEntity);

        if (component.GridEnt != null)
            QueueDel(component.GridEnt);
        if (component.MapEnt != null)
            QueueDel(component.MapEnt);
    }
    private void OnCanDragDrop(EntityUid uid, APCEntityComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.CanDrop |= !component.Destroyed;
    }

    private void DragAndDropOnAPC(Entity<APCEntityComponent> entity, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, entity.Comp.EntryDelay, new EnterAPCDoAfterEvent(), entity, target: args.Dragged, used: entity)
        {
            BreakOnDamage = true,
            BreakOnMove = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void DragFinishedOnAPCEntity(Entity<APCEntityComponent> entity, ref EnterAPCDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (TryComp<AccessReaderComponent>(entity, out var access) && !_accessReader.IsAllowed(args.User, entity, access))
        {
            _popup.PopupEntity(Loc.GetString("gateway-access-denied"), args.User);
            _audio.PlayPvs(entity.Comp.AccessDeniedSound, entity);
            args.Handled = true;
            return;
        }

        if (entity.Comp.OnAPC >= entity.Comp.MaxOnAPC)
        {
            _popup.PopupEntity(Loc.GetString("apc-full"), args.User);
            return;
        }

        EnterToAPCEntity(entity, entity.Comp, args.User);
        args.Handled = true;
    }

    public void EnterToAPCEntity(EntityUid apcUID, APCEntityComponent component, EntityUid uid)
    {
        if (component.GridEnt == null)
            return;

        var pilot = EnsureComp<APCPilotComponent>(uid);
        pilot.APC = apcUID;
        var position = GetAPCEnterPoint(component.GridEnt.Value);

        if (!position.HasValue)
            return;

        var gridEnt = component.GridEnt ?? component.MapEnt;
        if (gridEnt == null)
            return;

        var newCoords = new EntityCoordinates(gridEnt.Value, position.Value);
        HandlePulling(uid, newCoords, component, true);
    }

    public Vector2? GetAPCEnterPoint(EntityUid gridId)
    {
        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tag, out var transform))
        {
            if (transform.GridUid != gridId || !_tags.HasTag(uid, APCEnterPoint))
                continue;

            return transform.WorldPosition;
        }

        return null;
    }

    public void HandlePulling(EntityUid user, EntityCoordinates coords, APCEntityComponent component, bool checkCapacity = false)
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

            if (checkCapacity && HasComp<HumanoidAppearanceComponent>(pulling))
            {
                if (component.OnAPC + 1 >= component.MaxOnAPC)
                {
                    _popup.PopupEntity("Внутрь БТР-а не помещаетесь вы или тот, кого вы удерживаете. Отпустите, и попробуйте вновь", user);
                    return;
                }
                component.OnAPC++;
            }

            _pulling.TryStopPull(pulling, pullable, user);
            _transform.SetCoordinates(user, coords);
            _transform.SetCoordinates(pulling, coords);
            _pulling.TryStartPull(user, pulling);
            component.OnAPC++;

        }
        else
        {
            _transform.SetCoordinates(user, coords);
            component.OnAPC++;
        }
    }

}
