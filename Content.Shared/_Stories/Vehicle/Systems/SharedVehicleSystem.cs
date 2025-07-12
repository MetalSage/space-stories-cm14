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

public sealed partial class VehicleSystem : EntitySystem
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
        SubscribeLocalEvent<VehicleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<VehicleComponent, VehicleEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleComponent, BreakageEventArgs>(OnDestruction);
        SubscribeLocalEvent<VehicleComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<VehiclePilotComponent, VehicleHardpointsMenuActionEvent>(OnVehicleHardpointsMenuAction);

        Subs.BuiEvents<VehicleComponent>(VehicleSelectHardpointUI.Key,
            subs =>
            {
                subs.Event<VehicleSelectHardpointBuiMsg>(OnSelectHardpoint);
            });

        InitializeController();
    }

    private void OnMapInit(Entity<VehicleComponent> vehicle, ref MapInitEvent args)
    {
        vehicle.Comp.AmmoStorage = _container.EnsureContainer<ContainerSlot>(vehicle, vehicle.Comp.AmmoStorageID);
        vehicle.Comp.AmmoStorage.OccludesLight = false;

        _movement.RefreshMovementSpeedModifiers(vehicle);
        LoadMap(vehicle);
    }

    private void OnShutdown(Entity<VehicleComponent> vehicle, ref ComponentShutdown args)
    {
        if (vehicle.Comp.GridEnt != null)
            QueueDel(vehicle.Comp.GridEnt.Value);
    }

    private void OnInteractHand(Entity<VehicleComponent> entity, ref InteractHandEvent args)
    {
        if (args.Handled || !CanEnter(args.User, args.Target))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User,
            entity.Comp.EntryDelay, new VehicleEnterDoAfterEvent(),
            entity, target: args.Target, used: entity)
        {
            BreakOnMove = true
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnEnterDoAfter(Entity<VehicleComponent> entity, ref VehicleEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (entity.Comp.Passangers >= entity.Comp.MaxPassangers)
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

    private void LoadMap(Entity<VehicleComponent> vehicle)
    {
        var map = _map.CreateMap(out var mapId);
        if (!_mapLoader.TryLoadGrid(mapId, vehicle.Comp.GridPath, out var grid, null))
        {
            ISawmill.Error($"Failed to load Vehicle Grid from entity {vehicle}");
            _map.DeleteMap(mapId);
            return;
        }

        _meta.SetEntityName(grid.Value, $"VehicleGrid: {vehicle}");
        _meta.SetEntityName(map, $"VehicleMap: {vehicle}");

        vehicle.Comp.MapEnt = map;
        vehicle.Comp.GridEnt = grid.Value;
        Dirty(vehicle, vehicle.Comp);

        EnsureComp<VehicleMapComponent>(map);
        var gridComp = EnsureComp<VehicleGridComponent>(grid.Value);
        gridComp.Vehicle = GetNetEntity(vehicle);

        Dirty(grid.Value, component);
    }


    private bool CanEnter(EntityUid user, Entity<VehicleComponent> target)
    {
        var userPos = _transform.GetMapCoordinates(user).Position;
        var targetPos = _transform.GetMapCoordinates(target).Position;

        var directionToUser = (userPos - targetPos).ToWorldAngle().Degrees;

        var facing = Transform(target).LocalRotation.GetCardinalDir().ToAngle().Degrees;

        var range = target.Comp.EntryInteractionRange;
        var allowed = target.Comp.EntryDirections;

        bool Check(double offset, EntryDirection dir)
        {
            if (!allowed.HasFlag(dir))
                return false;

            var angle = (facing + offset + 360) % 360;
            return IsWithinRange(directionToUser, angle, range);
        }

        return
            Check(0, EntryDirection.Front) ||
            Check(180, EntryDirection.Back) ||
            Check(-90, EntryDirection.Left) ||
            Check(90, EntryDirection.Right);
    }

    private static bool IsWithinRange(double a, double b, float range)
    {
        var delta = ((a - b + 180 + 360) % 360) - 180;
        return Math.Abs(delta) <= range;
    }

    private Vector2? GetEnterPoint(EntityUid gridId)
    {
        var query = EntityQueryEnumerator<VehicleEnterPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == gridId)
                return xform.WorldPosition;
        }

        return null;
    }

    private void HandleEnterPulling(Entity<VehicleComponent> vehicle, EntityUid user, EntityCoordinates coords)
    {
        _rmcPulling.TryStopAllPullsFromAndOn(user);

        if (vehicle.Comp.Passangers >= vehicle.Comp.MaxPassangers)
        {
            _popup.PopupEntity("stories-transport-is-full", user);
            return;
        }

        if (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulledUid)
        {
            _transform.SetCoordinates(user, coords);
            vehicle.Comp.Passangers += 1;
            return;
        }

        _transform.SetCoordinates(pulledUid, coords);
        vehicle.Comp.Passangers += 1;
    }

    private void OnRefreshMovementSpeedModifiers(Entity<VehicleComponent> vehicle, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<VehicleAttachableHolderComponent>(vehicle, out var holderComp) ||
            !holderComp.Slots.ContainsKey(vehicle.Comp.MovementSlot))
        {
            args.ModifySpeed(0f, 0f);
            return;
        }

        var holder = (vehicle.Owner, holderComp);

        if (_attachableHolder.TryGetAttachable(holder, vehicle.Comp.MovementSlot, out var attachable) &&
            TryComp<VehicleMovementAttachableComponent>(attachable, out var attachableMovement))
        {
            args.ModifySpeed(attachableMovement.WalkSpeed, attachableMovement.SprintSpeed);
            return;
        }

        args.ModifySpeed(0f, 0f);
    }

    private void OnVehicleHardpointsMenuAction(Entity<BaseVehicleSeatComponent> gunner, ref VehicleHardpointsMenuActionEvent args)
    {
        if (gunner.Comp.Vehicle is not { } vehicle)
            return;

        _ui.OpenUi(vehicle, VehicleSelectHardpointUI.Key, gunner);
    }

    private void OnSelectHardpoint(Entity<VehicleComponent> vehicle, ref VehicleSelectHardpointBuiMsg args)
    {
        vehicle.Comp.ActiveHardpoint = GetEntity(args.Choice);
        Dirty(vehicle, vehicle.Comp);
    }
    
    private void OnDestruction(Entity<VehicleComponent> vehicle, ref BreakageEventArgs args)
    {
        DestroyVehicle(vehicle, vehicle.Comp);
    }

    public void DestroyVehicle(EntityUid uid, VehicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Destroyed = true;
        UpdateAppearance(uid, component);
    }

    public void UpdateAppearance(EntityUid uid, VehicleComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        _appearance.SetData(uid, VehicleVisuals.Destroyed, component.Destroyed, appearance);
    }

    public bool TryGetVehicle(Entity<TransformComponent> target, out Entity<VehicleComponent> vehicle)
    {
        vehicle = default;

        if (!TryComp<VehicleGridComponent>(target.Comp.GridUid, out var grid) || 
            !TryGetEntity(grid.Vehicle, out var vehicle))
        {
            return false;
        }

        if (vehicle is not { } uid || !TryComp<VehicleComponent>(uid, out var comp))
            return false;

        vehicle = (uid, comp);
        return true;
    }
}
