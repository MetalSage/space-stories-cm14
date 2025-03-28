using Content.Shared._RMC14.Xenonids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using static Content.Shared.Physics.CollisionGroup;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Placeable;

public sealed class CMPlaceFlagSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMPlaceFlagComponent, AfterInteractEvent>(OnFlagAfterInteract);
        SubscribeLocalEvent<CMPlaceFlagComponent, PlaceFlagDoAfterEvent>(OnFlagBuildDoAfter);

        SubscribeLocalEvent<CMPickupFlagComponent, ActivateInWorldEvent>(OnPickupActivateInWorld);
        SubscribeLocalEvent<CMPickupFlagComponent, AfterInteractEvent>(OnPickupAfterInteract);
        SubscribeLocalEvent<CMPickupFlagComponent, PickFlagDoAfterEvent>(OnPickupTakeDoAfter);
    }
    private void OnFlagAfterInteract(Entity<CMPlaceFlagComponent> flag, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !TryComp(args.User, out TransformComponent? transform))
            return;

        if (Build(flag, args.User, args.ClickLocation))
            args.Handled = true;
    }

    private void OnFlagBuildDoAfter(Entity<CMPlaceFlagComponent> flag, ref PlaceFlagDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Cancelled || args.Handled)
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        if (!_mapManager.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp) ||
            !_interaction.InRangeUnobstructed(flag, coordinates, popup: false) ||
            !coordinates.TryGetTileRef(out var turf, EntityManager) ||
            !CanBuild(flag, (gridId, gridComp), args.User, turf.Value))
        {
            return;
        }

        var built = SpawnAtPosition(flag.Comp.Builds, coordinates);
        _transform.SetLocalRotation(built, 0);
        EntityManager.DeleteEntity(flag);

        args.Handled = true;
    }

    private bool Build(Entity<CMPlaceFlagComponent> flag, EntityUid user, EntityCoordinates coordinates)
    {
        if (!_mapManager.TryFindGridAt(_transform.ToMapCoordinates(coordinates), out var gridId, out var gridComp) ||
            !coordinates.TryGetTileRef(out var tile) ||
            !CanBuild(flag, (gridId, gridComp), user, tile.Value))
        {
            return false;
        }

        var ev = new PlaceFlagDoAfterEvent(GetNetCoordinates(coordinates));
        var doAfter = new DoAfterArgs(EntityManager, user, flag.Comp.BuildDelay, ev, flag, flag)
        {
            BreakOnMove = true
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private bool TileSolidAndNotBlocked(TileRef tile)
    {
        return !tile.IsSpace() &&
               tile.GetContentTileDefinition().Sturdy &&
               !_turf.IsTileBlocked(tile, Impassable);
    }

    private bool CanBuild(
        Entity<CMPlaceFlagComponent> flag,
        Entity<MapGridComponent> grid,
        EntityUid user,
        TileRef tile)
    {

        var coordinates = new EntityCoordinates(tile.GridUid, tile.X, tile.Y).Offset(grid.Comp.TileSizeHalfVector);
        var mask = Impassable | InteractImpassable | TableLayer;
        var popup = _net.IsClient;
        if (!_interaction.InRangeUnobstructed(user, coordinates, collisionMask: mask, popup: popup))
            return false;

        if (!TileSolidAndNotBlocked(tile))
            return false;

        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(grid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CMPickupFlagComponent>(uid) &&
                TryComp(uid, out TransformComponent? transform) &&
                transform.LocalRotation.GetCardinalDir() == 0)
            {
                return false;
            }
        }

        return true;
    }



    private void OnPickupActivateInWorld(Entity <CMPickupFlagComponent> component, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp(args.User, out TransformComponent? transform))
        {
            return;
        }

        var coordinates = _transform.GetMoverCoordinates(args.User, transform);
        if (Take(component, args.User, coordinates))
            args.Handled = true;
    }

    private bool Take(Entity<CMPickupFlagComponent> component, EntityUid user, EntityCoordinates coordinates)
    {
        var ev = new PickFlagDoAfterEvent(GetNetCoordinates(coordinates));
        var doAfter = new DoAfterArgs(EntityManager, user, component.Comp.TakeDelay, ev, component, component)
        {
            BreakOnMove = true
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private void OnPickupAfterInteract(Entity<CMPickupFlagComponent> flag, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !TryComp(args.User, out TransformComponent? transform))
            return;

        if (Take(flag, args.User, args.ClickLocation))
            args.Handled = true;
    }

    private void OnPickupTakeDoAfter(Entity<CMPickupFlagComponent> component, ref PickFlagDoAfterEvent args)
    {
        if (_net.IsClient)
            return;
        if (args.Cancelled || args.Handled)
            return;
        var user = args.User;
        if (HasComp<XenoComponent>(user))
            return;

        var coords = Transform(args.User).Coordinates;

        EntityUid? entityToPlaceInHands = Spawn(component.Comp.Item, coords);
        
        args.Handled = true;
        EntityManager.DeleteEntity(args.Target);
        Dirty(component);

        if (entityToPlaceInHands != null)
        {
            _hands.PickupOrDrop(args.User, entityToPlaceInHands.Value);
        }
    }
}
