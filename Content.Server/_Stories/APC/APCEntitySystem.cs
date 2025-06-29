using System.Linq;
using System.Numerics;
using Content.Shared._Stories.APC;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem : EntitySystem
{
    private const float DoorInteractionAngleRange = 25f;

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCEntityComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<APCEntityComponent, EnterAPCDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<APCEntityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<APCEntityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<APCEntityComponent> apc, ref MapInitEvent args)
    {
        apc.Comp.AmmoStorage = _container.EnsureContainer<ContainerSlot>(apc, apc.Comp.AmmoStorageID);
        apc.Comp.AmmoStorage.OccludesLight = false;
        _movement.RefreshMovementSpeedModifiers(apc);
        LoadMap(apc);
    }

    private void OnShutdown(Entity<APCEntityComponent> apc, ref ComponentShutdown args)
    {
        if (apc.Comp.GridEnt != null)
            QueueDel(apc.Comp.GridEnt.Value);
    }

    private void OnInteractHand(Entity<APCEntityComponent> entity, ref InteractHandEvent args)
    {
        if (args.Handled || !CanInteractOnDoor(args.User, args.Target))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User,
            entity.Comp.EntryDelay, new EnterAPCDoAfterEvent(),
            entity, target: args.Target, used: entity)
        {
            BreakOnMove = true
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnEnterDoAfter(Entity<APCEntityComponent> entity, ref EnterAPCDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (entity.Comp.OnAPC >= entity.Comp.MaxOnAPC)
        {
            _popup.PopupEntity(Loc.GetString("stories-transport-is-full"), args.User);
            args.Handled = true;
            return;
        }

        if (entity.Comp.GridEnt is not { } gridEnt)
            return;

        var position = GetEnterPoint(gridEnt);
        if (position is not { } pos)
            return;

        args.Handled = true;
        var coords = new EntityCoordinates(gridEnt, pos);
        HandleEnterPulling(entity, args.User, coords);
    }

    private void LoadMap(Entity<APCEntityComponent> apc)
    {
        var mapEnt = FindOrCreateAPCMap();
        apc.Comp.MapEnt = mapEnt;

        var mapId = _transform.GetMapId(mapEnt);
        var existing = _mapManager.GetAllMapGrids(mapId)
            .Select(grid => _transform.GetWorldPosition(grid.Owner))
            .ToList();

        var offset = new Vector2(500, 500);

        if (_mapLoader.TryLoadGrid(mapId, apc.Comp.GridPath, out var grid, null, offset))
        {
            apc.Comp.GridEnt = grid.Value;
            _meta.SetEntityName(grid.Value, $"APC Grid: {apc}");
            Dirty(apc, apc.Comp);

            var component = EnsureComp<APCEntityGridComponent>(grid.Value);
            component.APC = GetNetEntity(apc);
            Dirty(grid.Value, component);
        }
    }

    private EntityUid FindOrCreateAPCMap()
    {
        var query = EntityQueryEnumerator<APCMapComponent>();
        while (query.MoveNext(out var uid, out _))
            return uid;

        var newMapEnt = _map.CreateMap();
        EnsureComp<APCMapComponent>(newMapEnt);
        _meta.SetEntityName(newMapEnt, "APCMap");
        return newMapEnt;
    }

    private bool CanInteractOnDoor(EntityUid user, EntityUid target)
    {
        var userPos = _transform.GetMapCoordinates(user).Position;
        var targetPos = _transform.GetMapCoordinates(target).Position;

        var directionToUser = (userPos - targetPos).ToWorldAngle().Degrees;
        var facing = Transform(target).LocalRotation.GetCardinalDir().ToAngle().Degrees;

        var left = (facing - 90 + 360) % 360;
        var right = (facing + 90) % 360;

        return IsWithinRange(directionToUser, left, DoorInteractionAngleRange)
            || IsWithinRange(directionToUser, right, DoorInteractionAngleRange);

        static bool IsWithinRange(double a, double b, double range)
        {
            var delta = ((a - b + 180 + 360) % 360) - 180;
            return Math.Abs(delta) <= range;
        }
    }

    private Vector2? GetEnterPoint(EntityUid gridId)
    {
        var query = EntityQueryEnumerator<APCEnterPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == gridId)
                return xform.WorldPosition;
        }

        return null;
    }

    private void HandleEnterPulling(Entity<APCEntityComponent> apc, EntityUid user, EntityCoordinates coords, bool checkCapacity = true)
    {
        if (TryComp(user, out PullableComponent? userAsPullable) && userAsPullable.Puller is { } userPuller)
        {
            _pulling.TryStopPull(user, userAsPullable, userPuller);
        }

        if (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulledUid)
        {
            _transform.SetCoordinates(user, coords);
            apc.Comp.OnAPC += 1;
            Dirty(apc);
            return;
        }

        if (TryComp(pulledUid, out PullerComponent? nestedPuller) &&
            nestedPuller.Pulling is { } nestedPulledUid &&
            TryComp(nestedPulledUid, out PullableComponent? nestedPullable))
        {
            _pulling.TryStopPull(nestedPulledUid, nestedPullable, pulledUid);
        }

        if (checkCapacity)
        {
            if (apc.Comp.OnAPC >= apc.Comp.MaxOnAPC)
            {
                _popup.PopupEntity("stories-transport-is-full", user);
                return;
            }
        }

        if (TryComp(pulledUid, out PullableComponent? pulledComp))
        {
            _pulling.TryStopPull(pulledUid, pulledComp, user);
        }

        _transform.SetCoordinates(pulledUid, coords);
        apc.Comp.OnAPC += 1;
        Dirty(apc);
    }
}
