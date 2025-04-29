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
using Robust.Server.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Content.Shared.Weapons.Ranged.Systems;

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
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAPCEntitySystem _sharedAPCEntitySystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCEntityComponent, InteractHandEvent>(AfterInteract);
        SubscribeLocalEvent<APCEntityComponent, EnterAPCDoAfterEvent>(OnDoAfterEnded);
        SubscribeLocalEvent<APCEntityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<APCEntityComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<APCEntityComponent, RefreshMovementSpeedModifiersEvent>(RefreshMovementSpeedModifiers);
        SubscribeLocalEvent<APCModuleComponent, AttemptShootEvent>(OnTurretAttemptShoot);

        InitializeDoors();
        InitializeModules();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var apcQuery = EntityQueryEnumerator<APCEntityComponent>();
        while (apcQuery.MoveNext(out var uid, out var comp))
        {}
    }

    private void RefreshMovementSpeedModifiers(Entity<APCEntityComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // if (!ent.Comp.Modules.Any(e => Comp<MetaDataComponent>(e).EntityF?.ID == ent.Comp.WheelsProto) || ent.Comp.Destroyed)
        //     args.ModifySpeed(0f, 0f);
    }

    private void OnMapInit(Entity<APCEntityComponent> apc, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(apc, ref apc.Comp.APCControlReturnActEntity, apc.Comp.APCControlReturnAction);
        _sharedAPCEntitySystem.UpdateAppearance(apc, apc.Comp);

        if (apc.Comp.SpawnModules.Count != 0)
            SetupModules(apc, apc.Comp.SpawnModules);
        LoadMap(apc, apc.Comp);
    }

    public void LoadMap(EntityUid uid, APCEntityComponent component)
    {
        if (!_resourceManager.TryContentFileRead(component.GridPath, out var _))
            return;

        MapId mapId;
        bool isNewMap = true;
        var mapEntity = _mapManager.GetAllMapIds()
            .Select(id => _mapManager.GetMapEntityId(id))
            .FirstOrDefault(e => _metaDataSystem.GetEntityData(e).EntityName?.StartsWith("APCMap") ?? false);

        if (mapEntity != null && _mapManager.TryGetMapId(mapEntity, out var existingMapId))
        {
            mapId = existingMapId;
            isNewMap = false;
        }
        else
        {
            mapId = _mapManager.CreateMap();
            _metaDataSystem.SetEntityName(_mapManager.GetMapEntityId(mapId), $"APCMap");
        }

        var offset = isNewMap ? Vector2.Zero : FindValidPosition(mapId);
        var grids = _mapLoader.LoadMap(mapId, component.GridPath, new MapLoadOptions
        {
            Offset = offset,
            Rotation = Angle.FromDegrees(0),
            DoMapInit = true
        });

        if (grids.Count > 0)
        {
            component.GridEnt = grids[0];
            component.MapEnt = _mapManager.GetMapEntityId(mapId);
            _metaDataSystem.SetEntityName(grids[0], $"APCEntity Grid: {uid}");
        }
    }

    private Vector2 FindValidPosition(MapId mapId)
    {
        var existingGrids = _mapManager.GetAllMapGrids(mapId).ToList();
        if (existingGrids.Count == 0) return Vector2.Zero;

        var random = new Random();
        for (int i = 0; i < 100; i++)
        {
            var pos = new Vector2(random.Next(-5000, 5000), random.Next(-5000, 5000));
            if (existingGrids.All(g => g.WorldPosition.XY().Distance(pos) >= 200))
                return pos;
        }
        
        Log.Warning("Не удалось найти подходящую позицию для нового грида");
        return Vector2.Zero;
    }

    private void OnComponentShutdown(EntityUid uid, APCEntityComponent component, ComponentShutdown args)
    {
        _sharedAPCEntitySystem.Return(uid, component);
        _sharedAPCEntitySystem.TryEjectEntities(uid, component);
        _actionsSystem.RemoveAction(component.APCControlReturnActEntity);

        if (component.GridEnt != null)
            QueueDel(component.GridEnt);
    }

    private void AfterInteract(Entity<APCEntityComponent> entity, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!CanInteractOnDoor(args.User, args.Target))
        {
            Log.Debug("false");
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, entity.Comp.EntryDelay, new EnterAPCDoAfterEvent(), entity, target: args.Target, used: entity)
        {
            BreakOnDamage = true,
            BreakOnMove = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDoAfterEnded(Entity<APCEntityComponent> entity, ref EnterAPCDoAfterEvent args)
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
            args.Handled = true;
            return;
        }

        if (entity.Comp.GridEnt == null)
            return;


        var pilot = EnsureComp<APCPilotComponent>(args.User);
        pilot.APC = entity.Owner;

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
        var targetFacingDirection = Transform(target).LocalRotation.GetCardinalDir();
        var rightAngle = (targetFacingDirection.ToAngle().Degrees + 90) % 360;
        var leftAngle = (targetFacingDirection.ToAngle().Degrees - 90 + 360) % 360;

        var userMapPos = _transform.GetMapCoordinates(user);
        var targetMapPos = _transform.GetMapCoordinates(target);
        var currentAngle = (userMapPos.Position - targetMapPos.Position).ToWorldAngle();

        var differenceFromLeftAngle = (leftAngle - currentAngle.Degrees + 180 + 360) % 360 - 180;
        var differenceFromRightAngle = (rightAngle - currentAngle.Degrees + 180 + 360) % 360 - 180;

        if (differenceFromLeftAngle > -25 && differenceFromLeftAngle < 25 ||
            differenceFromRightAngle > -25 && differenceFromRightAngle < 25)
        {
            return true;
        }
        return false;
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
                    _popup.PopupEntity("Внутрь БТР-а не помещаетесь вы или тот, кого вы удерживаете. Отпустите, и попробуйте вновь", user);
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

    private void OnTurretAttemptShoot(Entity<APCModuleComponent> ent, ref AttemptShootEvent args)
    {
        if (!TryComp<APCEntityComponent>(ent.Comp.APC, out var apc))
            return;

        if (!TryComp<TransformComponent>(ent.Comp.APC, out var transform))
            return;

        if (!_transform.IsParentOf(transform, ent.Owner))
            args.Cancelled = true;
    }
}
