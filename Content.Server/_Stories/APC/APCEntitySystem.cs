using System.Linq;
using System.Numerics;
using Content.Shared._Stories.APC;
using Content.Shared._Stories.APC.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Content.Shared.Prying.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Movement.Components;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAPCEntitySystem _apcSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<APCEntityComponent, InteractHandEvent>(AfterInteract);
        SubscribeLocalEvent<APCEntityComponent, EnterAPCDoAfterEvent>(OnEnterAPCEvent);
        SubscribeLocalEvent<APCEntityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<APCEntityComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<APCEntityComponent, AfterInteractUsingEvent>(OnAfterInteractUsingEvent);
        SubscribeLocalEvent<APCEntityComponent, DeattachModuleToAPCDoAfterEvent>(OnDeattachModule);
        SubscribeLocalEvent<APCEntityComponent, AttachModuleToAPCDoAfterEvent>(AttachModuleDoAfter);
        SubscribeLocalEvent<APCEntityComponent, DeattachModuleEvent>(DeattachModuleDoAfter);

        InitializeDoors();
        InitializeModules();
        InitializeMovement();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var apcQuery = EntityQueryEnumerator<APCEntityComponent>();
        while (apcQuery.MoveNext(out var uid, out var comp))
        {
        }
    }

    private void OnMapInit(Entity<APCEntityComponent> apc, ref MapInitEvent args)
    {
        _apcSystem.UpdateAppearance(apc, apc.Comp);

        apc.Comp.ModulesContainer = _container.EnsureContainer<Container>(apc, apc.Comp.ModulesContainerId);

        if (apc.Comp.StartingModules.Count != 0)
            SetupStartingModules(apc, apc.Comp.StartingModules);

        if (TryComp<MovementSpeedModifierComponent>(apc, out var apcMovement))
        {
            float totalWalkSpeed = apcMovement.BaseWalkSpeed;
            float totalSprintSpeed = apcMovement.BaseSprintSpeed;
            float totalAcceleration = apcMovement.Acceleration;

            foreach (var module in apc.Comp.ModulesContainer.ContainedEntities)
            {
                if (TryComp<MovementSpeedModifierComponent>(module, out var moduleMovement))
                {
                    totalWalkSpeed += moduleMovement.BaseWalkSpeed;
                    totalSprintSpeed += moduleMovement.BaseSprintSpeed;
                    totalAcceleration += moduleMovement.Acceleration;
                }
            }

            _movement.ChangeBaseSpeed(apc, totalWalkSpeed, totalSprintSpeed, totalAcceleration, apcMovement);
        }

        LoadMap(apc, apc.Comp);
        Dirty(apc);
    }

    public void LoadMap(EntityUid uid, APCEntityComponent component)
    {
        EntityUid? mapEntity = null;
        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var map, out var _))
        {
            if (HasComp<APCMapComponent>(map))
            {
                mapEntity = map;
                break;
            }
        }

        var mapId = mapEntity != null
            ? _transform.GetMapId(mapEntity.Value)
            : _mapManager.CreateMap();

        var mapEnt = _map.GetMap(mapId);

        if (mapEntity == null)
        {
            EnsureComp<APCMapComponent>(mapEnt);
            _metaDataSystem.SetEntityName(mapEnt, "APCMap");
        }

        var existingGrids = _mapManager.GetAllMapGrids(mapId)
            .Select(grid => _transform.GetWorldPosition(grid.Owner))
            .ToList();

        Vector2i offset = Vector2i.Zero;
        if (existingGrids.Count != 0)
        {
            for (int x = 0; ; x++)
            {
                offset = new Vector2i(x * 200, 0);
                var tooClose = existingGrids.Any(pos =>
                    Vector2.Distance(pos, new Vector2(offset.X, offset.Y)) < 200);

                if (!tooClose)
                    break;
            }
        }

        if (_mapLoader.TryLoadGrid(mapId, new ResPath(component.GridPath), out var grid,
            null, offset, Angle.FromDegrees(0)))
        {
            component.GridEnt = grid.Value;
            component.MapEnt = mapEnt;
            _metaDataSystem.SetEntityName(grid.Value, $"APC Grid: {uid}");
            var apcGridComp = EnsureComp<APCEntityGridComponent>(grid.Value);
            apcGridComp.APC = component.Owner;
        }

    }


    private void OnComponentShutdown(Entity<APCEntityComponent> apc, ref ComponentShutdown args)
    {
        if (apc.Comp.GridEnt != null)
            QueueDel(apc.Comp.GridEnt);
    }

    private void AfterInteract(Entity<APCEntityComponent> entity, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanInteractOnDoor(args.User, args.Target))
            return;

        if (TryComp<AccessReaderComponent>(entity, out var access) &&
            !_accessReader.IsAllowed(args.User, entity, access))
        {
            _popup.PopupEntity(Loc.GetString("gateway-access-denied"), args.User);
            _audio.PlayPvs(entity.Comp.AccessDeniedSound, entity);
            args.Handled = true;
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User,
            entity.Comp.EntryDelay, new EnterAPCDoAfterEvent(),
            entity, target: args.Target, used: entity)
        {
            BreakOnMove = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    private void OnEnterAPCEvent(Entity<APCEntityComponent> entity, ref EnterAPCDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (entity.Comp.OnAPC >= entity.Comp.MaxOnAPC)
        {
            _popup.PopupEntity(Loc.GetString("apc-full"), args.User);
            args.Handled = true;
            return;
        }

        if (entity.Comp.GridEnt == null)
            return;

        var position = GetAPCEnterPoint(entity.Comp.GridEnt.Value);
        var gridEnt = entity.Comp.GridEnt ?? entity.Comp.MapEnt;

        if (!position.HasValue)
            return;

        if (!gridEnt.HasValue)
            return;

        args.Handled = true;
        var coordinates = new EntityCoordinates(gridEnt.Value, position.Value);
        HandleEnterPulling(args.User, coordinates, entity.Comp);
    }


    public bool CanInteractOnDoor(EntityUid user, EntityUid target)
    {
        var targetRotation = Transform(target).LocalRotation;
        var userPos = _transform.GetMapCoordinates(user).Position;
        var targetPos = _transform.GetMapCoordinates(target).Position;

        var directionToUser = (userPos - targetPos).ToWorldAngle().Degrees;
        var facing = targetRotation.GetCardinalDir().ToAngle().Degrees;

        var left = (facing - 90 + 360) % 360;
        var right = (facing + 90) % 360;

        bool IsWithinRange(double a, double b, double range)
        {
            var delta = ((a - b + 180 + 360) % 360) - 180;
            return Math.Abs(delta) <= range;
        }

        return IsWithinRange(directionToUser, left, 25) || IsWithinRange(directionToUser, right, 25);
    }

    public Vector2? GetAPCEnterPoint(EntityUid gridId)
    {
        var query = EntityQueryEnumerator<APCEnterPointComponent, TransformComponent>();
        while (query.MoveNext(out var _, out var _, out var transform))
        {
            if (transform.GridUid != gridId)
                continue;

            return transform.WorldPosition;
        }

        return null;
    }

    private void HandleEnterPulling(EntityUid user, EntityCoordinates coords, APCEntityComponent component, bool checkCapacity = true)
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
                    _popup.PopupEntity("", user);
                    return;
                }
                component.OnAPC += FixedPoint2.New(1);
            }

            _pulling.TryStopPull(pulling, pullable, user);
            _transform.SetCoordinates(user, coords);
            _transform.SetCoordinates(pulling, coords);
            _pulling.TryStartPull(user, pulling);
            component.OnAPC += FixedPoint2.New(1);
        }
        else
        {
            _transform.SetCoordinates(user, coords);
            component.OnAPC += FixedPoint2.New(1);
        }
    }

    private void OnAfterInteractUsingEvent(Entity<APCEntityComponent> apc, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        args.Handled = true;

        if (HasComp<PryingComponent>(args.Used))
        {
            var options = new List<DialogOption>();

            foreach (var containedModule in apc.Comp.ModulesContainer.ContainedEntities)
            {
                if (!TryComp<MetaDataComponent>(containedModule, out var moduleMeta))
                    continue;

                options.Add(new DialogOption(
                    moduleMeta.EntityName,
                    new DeattachModuleEvent(GetNetEntity(args.User), GetNetEntity(containedModule))
                ));
            }

            if (!TryComp<MetaDataComponent>(apc, out var apcMeta))
                return;

            _dialog.OpenOptions(apc.Owner, args.User, apcMeta.EntityName, options, "Доступные модули:");
            return;
        }

        if (TryComp<APCModuleComponent>(args.Used, out var module))
        {
            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, module.AttachTime,
                new AttachModuleToAPCDoAfterEvent(), apc,
                target: apc.Owner, used: args.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true
            };

            _doAfterSystem.TryStartDoAfter(doAfterArgs);
        }
    }

    private void AttachModuleDoAfter(Entity<APCEntityComponent> apc, ref AttachModuleToAPCDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null || args.Used == null)
            return;

        args.Handled = true;
        SetupModule(apc, args.Used.Value);
    }

    private void DeattachModuleDoAfter(Entity<APCEntityComponent> apc, ref DeattachModuleEvent args)
    {
        if (!TryComp<APCModuleComponent>(GetEntity(args.Module), out var moduleComp))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, GetEntity(args.Attacher), moduleComp.DeattachTime,
            new DeattachModuleToAPCDoAfterEvent(), apc,
            target: apc.Owner, used: GetEntity(args.Module))
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    private void OnDeattachModule(Entity<APCEntityComponent> apc, ref DeattachModuleToAPCDoAfterEvent args)
    {
        // SHIT
        if (args.Cancelled || args.Handled || args.Used == null)
            return;

        args.Handled = true;

        var module = args.Used.Value;
        var user = args.User;

        if (!TryComp<APCModuleComponent>(module, out var moduleComp) ||
            !TryComp<TransformComponent>(module, out var xform))
        {
            return;
        }

        if (moduleComp.VirtualModuleEnt != null && 
            apc.Comp.VirtualModules != null)
        {
            apc.Comp.VirtualModules.Remove(moduleComp.VirtualModuleEnt.Value);
            QueueDel(moduleComp.VirtualModuleEnt.Value);
        }

        if (apc.Comp.ModulesContainer != null)
        {
            _container.Remove(module, apc.Comp.ModulesContainer);
        }

        if (TryComp<TransformComponent>(user, out var userXform))
        {
            _transform.SetCoordinates(module, userXform.Coordinates);
        }

        if (TryComp<MovementSpeedModifierComponent>(module, out var moduleMovement) && 
            TryComp<MovementSpeedModifierComponent>(apc, out var apcMovement))
        {
            var totalWalk = apcMovement.BaseWalkSpeed - moduleMovement.BaseWalkSpeed;
            var totalSprint = apcMovement.BaseSprintSpeed - moduleMovement.BaseSprintSpeed;
            var totalAcceleration = apcMovement.Acceleration - moduleMovement.Acceleration;

            _movement.ChangeBaseSpeed(apc, totalWalk, totalSprint, totalAcceleration, apcMovement);
        }

        if (TryComp<GunComponent>(module, out var gun))
        {
            Logger.Info($"found gun comp but gun logic is unavaible");
        }
    }

}
