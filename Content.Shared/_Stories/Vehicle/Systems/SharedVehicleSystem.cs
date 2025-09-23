using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Attachables;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem : EntitySystem
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
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly VehicleAttachableHolderSystem _attachableHolder = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<VehicleComponent, VehicleEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<VehiclePilotComponent, VehicleHardpointsMenuActionEvent>(OnVehicleHardpointsMenuAction);
        SubscribeLocalEvent<VehicleComponent, BeforeDamageChangedEvent>(BeforeDamageTaken);

        Subs.BuiEvents<VehicleComponent>(VehicleSelectHardpointUI.Key,
            subs =>
            {
                subs.Event<VehicleSelectHardpointBuiMsg>(OnSelectHardpoint);
            });

        InitializeController();
        InitializeMovement();
    }

    private void OnMapInit(Entity<VehicleComponent> vehicle, ref MapInitEvent args)
    {
        vehicle.Comp.AmmoStorage = _container.EnsureContainer<ContainerSlot>(vehicle, vehicle.Comp.AmmoStorageID);
        vehicle.Comp.AmmoStorage.OccludesLight = false;

        _movement.RefreshMovementSpeedModifiers(vehicle);
        if (!_net.IsClient)
            LoadMap(vehicle); // lmao.
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
/*
        if (entity.Comp.Passangers >= entity.Comp.MaxPassangers)
        {
            _popup.PopupEntity(Loc.GetString("stories-transport-is-full"), args.User);
            args.Handled = true;
            return;
        }
*/
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
            Log.Error($"Failed to load Vehicle Grid from entity {vehicle}");
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

        Dirty(grid.Value, gridComp);
    }


    private bool CanEnter(EntityUid user, EntityUid target)
    {
        if (!TryComp<VehicleComponent>(target, out var vehicle))
            return false;

        var userPos = _transform.GetMapCoordinates(user).Position;
        var targetPos = _transform.GetMapCoordinates(target).Position;

        var directionToUser = (userPos - targetPos).ToWorldAngle().Degrees;

        var facing = Transform(target).LocalRotation.GetCardinalDir().ToAngle().Degrees;

        var range = vehicle.EntryInteractionRange;
        var allowed = vehicle.EntryDirections;

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
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridId)
                return _transform.GetWorldPosition(uid);
        }

        return null;
    }

    private void HandleEnterPulling(Entity<VehicleComponent> vehicle, EntityUid user, EntityCoordinates coords)
    {
        EntityUid targetEntity;

        var comp = vehicle.Comp;

        if (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulledUid)
            targetEntity = user;
        else
            targetEntity = pulledUid;

        var hasSlot = false;

        if (HasComp<XenoComponent>(targetEntity) && comp.XenoSlots.Current < comp.XenoSlots.Max)
        {
            comp.XenoSlots.Current += 1;
            hasSlot = true;
        }
        else if ((_mobState.IsDead(targetEntity) &&
                 !HasComp<UnrevivableComponent>(targetEntity)) && comp.RevivableDeadSlots.Current < comp.RevivableDeadSlots.Max)
        {
            comp.RevivableDeadSlots.Current += 1;
            hasSlot = true;
        }
        else if (_job.MindTryGetJob(targetEntity, out var job) &&
                 comp.RoleReservedSlots.TryGetValue(job.ID, out var roleSlots) &&
                 roleSlots.Current < roleSlots.Max)
        {
            roleSlots.Current += 1;
            comp.RoleReservedSlots[job.ID] = roleSlots;
            hasSlot = true;
        }
        else if (comp.PassengerSlots.Current < comp.PassengerSlots.Max)
        {
            comp.PassengerSlots.Current += 1;
            hasSlot = true;
        }

        if (!hasSlot)
        {
            _popup.PopupEntity("stories-transport-is-full", user);
            return;
        }

        _rmcPulling.TryStopAllPullsFromAndOn(user);
        _transform.SetCoordinates(targetEntity, coords);
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

    private void OnVehicleHardpointsMenuAction(Entity<VehiclePilotComponent> pilot, ref VehicleHardpointsMenuActionEvent args)
    {
        if (pilot.Comp.Vehicle is not { } vehicle)
            return;

        _ui.OpenUi(vehicle, VehicleSelectHardpointUI.Key, pilot);
    }

    private void OnSelectHardpoint(Entity<VehicleComponent> vehicle, ref VehicleSelectHardpointBuiMsg args)
    {
        vehicle.Comp.ActiveHardpoint = GetEntity(args.Choice);
        Dirty(vehicle, vehicle.Comp);
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

    public bool TryGetVehicle(EntityUid target, out Entity<VehicleComponent> vehicle, TransformComponent? xform = null)
    {
        vehicle = default;

        if (!Resolve(target, ref xform))
            return false;

        if (!TryComp<VehicleGridComponent>(xform.GridUid, out var grid) ||
            !TryGetEntity(grid.Vehicle, out var vehicleUid))
        {
            return false;
        }

        if (vehicleUid is null || !TryComp<VehicleComponent>(vehicleUid, out var comp))
            return false;

        vehicle = (vehicleUid.Value, comp);
        return true;
    }


    private void BeforeDamageTaken(Entity<VehicleComponent> vehicle, ref BeforeDamageChangedEvent args)
    {
        var comp = vehicle.Comp;

        if (args.Cancelled)
            return;

        if (TryComp<RMCSizeComponent>(args.Origin, out var rmcSize) && rmcSize.Size == RMCSizes.Small)
        {
            args.Cancelled = true;
            return;
        }

        var modified = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            var mult = 1f;
            if (comp.DamageMults != null && comp.DamageMults.TryGetValue(type, out var m))
                mult = m;

            modified.DamageDict[type] = value * mult;
        }

        var activeHardpoints = new List<(EntityUid ent, VehicleAttachableComponent comp)>();

        foreach (var h in comp.Hardpoints)
        {
            if (TryComp<VehicleAttachableComponent>(h, out var hard) && hard.Health > FixedPoint2.Zero)
                activeHardpoints.Add((h, hard));
        }

        if (activeHardpoints.Count > 0)
        {
            var hullDamage = modified * 0.1f;
            ApplyToHull(vehicle, hullDamage);

            foreach (var hardpoint in activeHardpoints)
                ApplyToHardpoint(hardpoint, modified);

            args.Cancelled = true;
        }
        else
        {
            ApplyToHull(vehicle, modified);
            args.Cancelled = true;
        }
    }

    private void ApplyToHardpoint(Entity<VehicleAttachableComponent> attachable, DamageSpecifier damage)
    {
        attachable.Comp.Health -= damage.GetTotal();
    }

    private void ApplyToHull(Entity<VehicleComponent> vehicle, DamageSpecifier damage)
    {
        var comp = vehicle.Comp;

        var total = damage.GetTotal();
        comp.Health -= total;

        if (comp.Health == FixedPoint2.Zero)
            DestroyVehicle(vehicle.Owner);

    }
}
