using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Shared._RMC14.Spawning;

public abstract class SharedGridSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private MapId? _map;
    private int _index;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<GridSpawnerComponent, MapInitEvent>(OnGridSpawnerMapInit);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _map = null;
        _index = 0;
    }

    private void OnGridSpawnerMapInit(Entity<GridSpawnerComponent> ent, ref MapInitEvent args)
    {
        Log.Info($"GridSpawner MapInit triggered for entity: {ToPrettyString(ent)}");

        if (ent.Comp.Spawn is not { } spawn)
            return;

        if (_net.IsClient)
            return;

        if (!_config.GetCVar(CCVars.GridFill))
            return;

        if (_map == null)
        {
            _mapSystem.CreateMap(out var mapId);
            _map = mapId;
        }

        var offset = new Vector2(_index * 50, _index * 50);
        _index++;

        if (!_mapSystem.MapExists(_map) ||
            !_mapLoader.TryLoadGrid(_map.Value, spawn, out var result, offset: offset))
        {
            return;
        }

        var grid = result.Value;

        var xform = Transform(ent);
        var coordinates = _transform.GetMapCoordinates(ent, xform);
        coordinates = coordinates.Offset(ent.Comp.Offset);
        _transform.SetMapCoordinates(grid, coordinates);

        if (TryComp(grid, out PhysicsComponent? physics) &&
            TryComp(grid, out FixturesComponent? fixtures))
        {
            _physics.SetBodyType(grid, BodyType.Static, manager: fixtures, body: physics);
            _physics.SetBodyStatus(grid, physics, BodyStatus.OnGround);
            _physics.SetFixedRotation(grid, true, manager: fixtures, body: physics);
        }

        if (TryComp(ent, out DropshipDestinationComponent? destination))
        {
            destination.Ship = grid;
            Dirty(ent, destination);

            if (TryComp(grid, out DropshipComponent? dropship))
            {
                dropship.Destination = ent.Owner;
                Dirty(grid, dropship);
            }
        }
    }
}
