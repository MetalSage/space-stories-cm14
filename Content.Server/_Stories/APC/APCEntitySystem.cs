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
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;

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
        if (args.Handled || !CanEnter(args.User, args.Target))
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
        var map = _map.CreateMap(out var mapId);
        if (!_mapLoader.TryLoadGrid(mapId, apc.Comp.GridPath, out var grid, null))
        {
            ISawmill.Error($"Failed to load APC Grid from entity {apc}");
            _map.DeleteMap(mapId);
            return;
        }

        _meta.SetEntityName(grid.Value, $"APCGrid: {apc}");
        _meta.SetEntityName(map, $"APCMap: {apc}");

        apc.Comp.MapEnt = map;
        apc.Comp.GridEnt = grid.Value;
        Dirty(apc, apc.Comp);

        EnsureComp<APCMapComponent>(map);
        var gridComp = EnsureComp<APCEntityGridComponent>(grid.Value);
        gridComp.APC = GetNetEntity(apc);

        Dirty(grid.Value, component);
    }


    private bool CanEnter(EntityUid user, Entity<APCEntityComponent> target)
    {
        var userPos = _transform.GetMapCoordinates(user).Position;
        var targetPos = _transform.GetMapCoordinates(target.Owner).Position;

        var directionToUser = (userPos - targetPos).ToWorldAngle().Degrees;
        var facing = Transform(target.Owner).LocalRotation.GetCardinalDir().ToAngle().Degrees;

        var left = (facing - 90 + 360) % 360;
        var right = (facing + 90) % 360;

        return IsWithinRange(directionToUser, left)
            || IsWithinRange(directionToUser, right);

        static bool IsWithinRange(double a, double b)
        {
            var delta = ((a - b + 180 + 360) % 360) - 180;
            return Math.Abs(delta) <= target.Comp.EntryInteractionRange;
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

    private void HandleEnterPulling(Entity<APCEntityComponent> apc, EntityUid user, EntityCoordinates coords)
    {
        _rmcPulling.TryStopAllPullsFromAndOn(user);

        if (apc.Comp.Passangers >= apc.Comp.MaxPassangers)
        {
            _popup.PopupEntity("stories-transport-is-full", user);
            return;
        }

        if (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulledUid)
        {
            _transform.SetCoordinates(user, coords);
            apc.Comp.OnAPC += 1;
            return;
        }

        _transform.SetCoordinates(pulledUid, coords);
        apc.Comp.OnAPC += 1;
    }
}
