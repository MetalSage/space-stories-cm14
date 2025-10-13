using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Camera;
using Content.Shared._Stories.Attachables;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

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
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly VehicleAttachableHolderSystem _attachableHolder = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly GunIFFSystem _gunIFF = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedRMCCameraSystem _rmcCamera = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleComponent, ActivateInWorldEvent>(OnVehicleActivateInWorld);
        SubscribeLocalEvent<VehicleComponent, VehicleEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<VehicleInteriorDoorComponent, ActivateInWorldEvent>(OnInteriorDoorActivateInWorld);
        SubscribeLocalEvent<VehicleComponent, VehicleLeaveDoAfterEvent>(OnLeaveDoAfter);
        SubscribeLocalEvent<VehiclePilotComponent, VehicleLockDoorsEvent>(OnLockActionEvent);

        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<VehiclePilotComponent, VehicleSelectHardpointEvent>(OnVehicleHardpointsMenuAction);
        SubscribeLocalEvent<VehiclePilotComponent, VehicleStatusMenuEvent>(OnVehicleStatusMenuAction);
        SubscribeLocalEvent<VehicleComponent, DamageModifyEvent>(OnVehicleDamageModify);
        SubscribeLocalEvent<VehicleComponent, DamageChangedEvent>(OnVehicleDamageChanged);
        SubscribeLocalEvent<VehicleComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<VehicleComponent, GettingAttackedAttemptEvent>(OnBeingAttacked);
        SubscribeLocalEvent<VehicleViewportComponent, ActivateInWorldEvent>(OnViewportInteract);
        SubscribeLocalEvent<VehicleViewportWatcherComponent, MoveInputEvent>(OnViewportWatcherMove);

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
            PredictedQueueDel(vehicle.Comp.GridEnt.Value);
    }

    private void OnVehicleActivateInWorld(Entity<VehicleComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !CanEnter(args.User, args.Target))
            return;

        args.Handled = true;

        var entryDelay = TimeSpan.FromSeconds(GetEntryDelay(entity, args.User));

        var doAfter = new DoAfterArgs(EntityManager, args.User,
            entryDelay, new VehicleEnterDoAfterEvent(),
            entity, target: args.Target, used: entity)
        {
            BreakOnMove = true
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnInteriorDoorActivateInWorld(Entity<VehicleInteriorDoorComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetVehicle(entity.Owner, out var vehicle))
            return;

        args.Handled = true;

        var entryDelay = TimeSpan.FromSeconds(GetEntryDelay(vehicle, args.User));

        var doAfter = new DoAfterArgs(EntityManager, args.User,
            entryDelay, new VehicleLeaveDoAfterEvent(),
            vehicle, target: args.Target, used: entity)
        {
            BreakOnMove = true
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnEnterDoAfter(Entity<VehicleComponent> ent, ref VehicleEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (ent.Comp.Locked && TryComp<DamageableComponent>(ent, out var damageable) &&
            damageable.TotalDamage < ent.Comp.MaxHealth)
        {
            if (!CheckVehicleAccess(ent, args.User))
                return;
        }

        if (ent.Comp.GridEnt is not { } gridEnt)
            return;

        var position = GetEnterPoint(gridEnt);
        if (position is not { } pos)
            return;

        args.Handled = true;
        var coords = new EntityCoordinates(gridEnt, pos);
        HandleEnterPulling(ent, args.User, coords);
    }

    private void OnLeaveDoAfter(Entity<VehicleComponent> ent, ref VehicleLeaveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;
        
        if (!TryComp<TransformComponent>(ent, out var xform) || xform.GridUid is null)
            return;
        
        if (!TryComp<VehicleInteriorDoorComponent>(args.Args.Target.Value, out var door))
            return;
        
        var vehiclePos = _transform.GetWorldPosition(ent.Owner);
        var vehicleRotation = _transform.GetWorldRotation(ent.Owner);
        
        var exitOffset = GetExitOffset(door.Side, vehicleRotation);
        var exitPos = vehiclePos + exitOffset;
        
        args.Handled = true;
        var coords = new EntityCoordinates(xform.GridUid.Value, exitPos);
        HandleLeavePulling(ent, args.User, coords);
    }

    private Vector2 GetExitOffset(EntryDirection direction, Angle rotation)
    {
        const float exitDistance = 2f;
        
        return direction switch
        {
            EntryDirection.Front => rotation.RotateVec(new Vector2(0, exitDistance)),
            EntryDirection.Back => rotation.RotateVec(new Vector2(0, -exitDistance)),
            EntryDirection.Left => rotation.RotateVec(new Vector2(-exitDistance, 0)),
            EntryDirection.Right => rotation.RotateVec(new Vector2(exitDistance, 0)),
            _ => rotation.RotateVec(new Vector2(0, -exitDistance))
        };
    }

    private void OnLockActionEvent(Entity<VehiclePilotComponent> pilot, ref VehicleLockDoorsEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<VehicleComponent>(pilot.Comp.Vehicle, out var vehicle))
            return;

        args.Handled = true;

        vehicle.Locked = !vehicle.Locked;

        var icon = vehicle.Locked
            ? new SpriteSpecifier.Rsi(new ResPath("/Textures/_Stories/Actions/vehicle_actions.rsi"), "door_locked")
            : new SpriteSpecifier.Rsi(new ResPath("/Textures/_Stories/Actions/vehicle_actions.rsi"), "door_unlocked");

        foreach (var action in _actions.GetActions(pilot))
        {
            if (_actions.GetEvent(action) is VehicleLockDoorsEvent)
                _actions.SetIcon(action.Owner, icon);
        }

        Dirty(pilot.Comp.Vehicle.Value, vehicle);
        UpdateVehicleStatusUI((pilot.Comp.Vehicle.Value, vehicle));
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
            TryComp<VehicleMovementAttachableComponent>(attachable, out var attachableMovement) && !attachable.Comp.Destroyed)
        {
            args.ModifySpeed(attachableMovement.WalkSpeed, attachableMovement.SprintSpeed);
            return;
        }

        args.ModifySpeed(0f, 0f);
    }

    private void OnVehicleStatusMenuAction(Entity<VehiclePilotComponent> pilot, ref VehicleStatusMenuEvent args)
    {
        if (pilot.Comp.Vehicle is not { } vehicle)
            return;

        _ui.OpenUi(vehicle, VehicleStatusUI.Key, pilot);
    }

    private void OnVehicleHardpointsMenuAction(Entity<VehiclePilotComponent> pilot, ref VehicleSelectHardpointEvent args)
    {
        if (pilot.Comp.Vehicle is not { } vehicle)
            return;

        _ui.OpenUi(vehicle, VehicleSelectHardpointUI.Key, pilot);
    }

    private void OnSelectHardpoint(Entity<VehicleComponent> vehicle, ref VehicleSelectHardpointBuiMsg args)
    {
        var hardpoint = GetEntity(args.Choice);
        vehicle.Comp.ActiveHardpoint = hardpoint;
        Dirty(vehicle, vehicle.Comp);

        var gunnerSeats = EntityQueryEnumerator<VehiclePilotSeatComponent>();
        while (gunnerSeats.MoveNext(out var seatUid, out var seat))
        {
            if (seat.Vehicle != vehicle.Owner || !seat.IsGunner)
                continue;

            if (seat.Pilot is not { } pilotUid || !TryComp<VehiclePilotComponent>(pilotUid, out var pilot))
                continue;

            if (hardpoint != null && TryComp<VehicleGunComponent>(hardpoint, out var gun))
            {
                gun.User = pilotUid;
                pilot.Gun = hardpoint;

                var relay = EnsureComp<InteractionRelayComponent>(pilotUid);
                _interaction.SetRelay(pilotUid, hardpoint, relay);
                _mover.SetRelay(pilotUid, hardpoint);

                Dirty(hardpoint, gun);
                Dirty(pilotUid, pilot);
            }

            break;
        }
    }

    private void OnBeingAttacked(Entity<VehicleComponent> ent, ref GettingAttackedAttemptEvent args)
    {
        if (TryComp<RMCSizeComponent>(args.Attacker, out var rmcSize) && rmcSize.Size < ent.Comp.SizeRequiredToHit)
        {
            _popup.PopupClient(Loc.GetString("st-vehicle-wrong-size-to-attack"), args.Attacker);
            args.Cancelled = true;
        }
    }

    private void OnBeforeDamageChanged(Entity<VehicleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled ||
            !TryComp<DamageableComponent>(ent, out var damageable))
        {
            return;
        }

        var maxHealth = ent.Comp.MaxHealth;
        var currentDamage = damageable.TotalDamage;
        var incomingDamage = args.Damage.GetTotal();

        if (incomingDamage < 0)
            return;

        if (currentDamage >= maxHealth)
        {
            args.Cancelled = true;
        }
        else if (currentDamage + incomingDamage > maxHealth)
        {
            var allowedDamage = maxHealth - currentDamage;
            var factor = allowedDamage / incomingDamage;

            var newDamage = new DamageSpecifier();
            FixedPoint2 accumulatedDamage = FixedPoint2.Zero;
            var damageList = args.Damage.DamageDict.ToList();

            for (int i = 0; i < damageList.Count - 1; i++)
            {
                var scaled = damageList[i].Value * factor;
                newDamage.DamageDict[damageList[i].Key] = scaled;
                accumulatedDamage += scaled;
            }

            if (damageList.Count > 0)
            {
                var lastKey = damageList[damageList.Count - 1].Key;
                newDamage.DamageDict[lastKey] = allowedDamage - accumulatedDamage;
            }

            args.Damage = newDamage;
        }
    }

    private void OnVehicleDamageModify(Entity<VehicleComponent> vehicle, ref DamageModifyEvent args)
    {
        var comp = vehicle.Comp;
        var modifiedDamage = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            var mult = 1f;
            if (comp.DamageMults != null && comp.DamageMults.TryGetValue(type, out var m))
                mult = m;
            modifiedDamage.DamageDict[type] = value * mult;
        }

        if (args.Origin != null &&
            TryComp<VehicleDamageMultiplierComponent>(args.Origin.Value, out var vehicleDamageMult))
        {
            modifiedDamage *= vehicleDamageMult.Mult;
        }

        var activeHardpoints = new List<(EntityUid ent, VehicleAttachableComponent comp)>();
        foreach (var h in comp.Hardpoints)
        {
            if (!TryComp<VehicleAttachableComponent>(h, out var hard))
                continue;

            var currentHealth = hard.MaxHealth;
            if (TryComp<DamageableComponent>(h, out var hardDamageable))
                currentHealth = FixedPoint2.Max(hard.MaxHealth - hardDamageable.TotalDamage, 0);

            if (currentHealth <= FixedPoint2.Zero)
                continue;

            if (hard.Ignored)
                continue;

            activeHardpoints.Add((h, hard));
        }

        if (activeHardpoints.Count > 0)
        {
            foreach (var (hardpointEnt, hardpointComp) in activeHardpoints)
            {
                _damageable.TryChangeDamage(hardpointEnt, modifiedDamage, ignoreResistances: false,
                    interruptsDoAfters: false, origin: args.Origin, tool: args.Tool);
            }

            modifiedDamage *= 0.1f;
        }

        if (TryComp<DamageableComponent>(vehicle, out var damageable))
        {
            var maxHealth = comp.MaxHealth;
            var currentDamage = damageable.TotalDamage;
            var incomingDamage = modifiedDamage.GetTotal();

            if (currentDamage >= maxHealth)
            {
                modifiedDamage *= 0f;
            }
            else if (currentDamage + incomingDamage > maxHealth)
            {
                var allowedDamage = maxHealth - currentDamage;
                var factor = allowedDamage / incomingDamage;

                var clampedDamage = new DamageSpecifier();
                FixedPoint2 accumulatedDamage = FixedPoint2.Zero;
                var damageList = modifiedDamage.DamageDict.ToList();

                for (int i = 0; i < damageList.Count - 1; i++)
                {
                    var scaled = damageList[i].Value * factor;
                    clampedDamage.DamageDict[damageList[i].Key] = scaled;
                    accumulatedDamage += scaled;
                }

                if (damageList.Count > 0)
                {
                    var lastKey = damageList[damageList.Count - 1].Key;
                    clampedDamage.DamageDict[lastKey] = allowedDamage - accumulatedDamage;
                }

                modifiedDamage = clampedDamage;
            }
        }

        args.Damage = modifiedDamage;
    }

    private void OnVehicleDamageChanged(Entity<VehicleComponent> vehicle, ref DamageChangedEvent args)
    {
        var currentHealth = FixedPoint2.Max(vehicle.Comp.MaxHealth - args.Damageable.TotalDamage, 0);

        if (currentHealth == FixedPoint2.Zero && vehicle.Comp.MaxHealth > FixedPoint2.Zero)
        {
            vehicle.Comp.Destroyed = true;
        }
        else if (currentHealth > 0)
        {
            vehicle.Comp.Destroyed = false;
        }

        Dirty(vehicle);
        UpdateVehicleStatusUI(vehicle);

        _pointLight.SetEnabled(vehicle.Owner, currentHealth > FixedPoint2.Zero);

        bool anyAlive = false;
        foreach (var hardpoint in vehicle.Comp.Hardpoints)
        {
            if (!TryComp<VehicleAttachableComponent>(hardpoint, out var attachable))
                continue;

            if (attachable.Ignored)
                continue;

            if (!TryComp<DamageableComponent>(hardpoint, out var dmg))
                continue;

            var hp = FixedPoint2.Max(attachable.MaxHealth - dmg.TotalDamage, 0);
            if (hp > FixedPoint2.Zero)
            {
                anyAlive = true;
                break;
            }
        }

        var cameraQuery = EntityQueryEnumerator<RMCCameraComponent, TransformComponent>();
        while (cameraQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (uid == vehicle.Owner || xform.GridUid == vehicle.Comp.GridEnt)
                _rmcCamera.SetCameraActive(vehicle.Owner, anyAlive);
        }
    }

    private void OnViewportInteract(Entity<VehicleViewportComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<XenoComponent>(args.User))
            return;

        if (!TryGetVehicle(ent, out var vehicle))
            return;

        if (ent.Comp.Watcher != null)
        {
            _popup.PopupEntity("taken", args.User);
            return;
        }

        args.Handled = true;

        ent.Comp.Watcher = args.User;
        EnsureComp<VehicleViewportWatcherComponent>(args.User);
        Dirty(ent);

        _eye.SetTarget(args.User, vehicle.Owner);
    }

    private void OnViewportWatcherMove(Entity<VehicleViewportWatcherComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;
        
        var query = EntityQueryEnumerator<VehicleViewportComponent>();
        while (query.MoveNext(out var uid, out var viewport))
        {
            if (viewport.Watcher == ent.Owner)
            {
                viewport.Watcher = null;
                Dirty(uid, viewport);
                break;
            }
        }
        
        RemCompDeferred<VehicleViewportWatcherComponent>(ent);
        _eye.SetTarget(ent, null);
    }

    public void DestroyVehicle(EntityUid uid, VehicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Destroyed = true;
        Dirty(uid, component);

        _movement.RefreshMovementSpeedModifiers(uid);
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

    public bool TryGetDriver(Entity<VehicleComponent> vehicle, [NotNullWhen(true)] out EntityUid? driver)
    {
        driver = default;

        var query = EntityQueryEnumerator<VehiclePilotSeatComponent>();
        while (query.MoveNext(out var uid, out var seat))
        {
            if (seat.Vehicle == vehicle.Owner && seat.Pilot != null && !seat.IsGunner)
            {
                driver = seat.Pilot;
                return true;
            }
        }

        return false;
    }

    private void LoadMap(Entity<VehicleComponent> vehicle)
    {
        var map = _map.CreateMap(out var mapId);
        if (!_mapLoader.TryLoadGrid(mapId, vehicle.Comp.GridPath, out var grid, null))
        {
            Log.Error($"[{nameof(LoadMap)}] Failed to load vehicle grid from {ToPrettyString(vehicle)}");
            _map.DeleteMap(mapId);
            return;
        }

        _meta.SetEntityName(grid.Value, $"VehicleGrid: {ToPrettyString(vehicle)}");
        _meta.SetEntityName(map, $"VehicleMap: {ToPrettyString(vehicle)}");

        vehicle.Comp.MapEnt = map;
        vehicle.Comp.GridEnt = grid.Value;
        Dirty(vehicle, vehicle.Comp);

        EnsureComp<VehicleMapComponent>(map);
        var gridComp = EnsureComp<VehicleGridComponent>(grid.Value);
        gridComp.Vehicle = GetNetEntity(vehicle);

        Dirty(grid.Value, gridComp);

        var seatsQuery = EntityQueryEnumerator<VehiclePilotSeatComponent, TransformComponent>();
        while (seatsQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != grid.Value.Owner)
                continue;

            comp.Vehicle = vehicle.Owner;
        }
    }

    private void HandleEnterPulling(Entity<VehicleComponent> vehicle, EntityUid user, EntityCoordinates coords)
    {
        EntityUid targetEntity = (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulledUid)
            ? user
            : pulledUid;

        var comp = vehicle.Comp;

        if (targetEntity != user)
        {
            if (comp.Locked && TryComp<DamageableComponent>(vehicle, out var damageable) &&
                damageable.TotalDamage < comp.MaxHealth)
            {
                if (!CheckVehicleAccess(vehicle, user))
                    return;
            }
        }

        if (HasComp<XenoComponent>(targetEntity))
        {
            if (comp.XenoSlots.Current < comp.XenoSlots.Max)
                comp.XenoSlots.Current++;
            else
            {
                _popup.PopupEntity(Loc.GetString("stories-transport-is-full"), user);
                return;
            }
        }
        else if (_mobState.IsDead(targetEntity) && !HasComp<UnrevivableComponent>(targetEntity))
        {
            if (comp.RevivableDeadSlots.Current < comp.RevivableDeadSlots.Max)
                comp.RevivableDeadSlots.Current++;
            else
            {
                _popup.PopupEntity(Loc.GetString("stories-transport-is-full"), user);
                return;
            }
        }
        else if (comp.PassengerSlots.Current < comp.PassengerSlots.Max)
        {
            comp.PassengerSlots.Current++;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("stories-transport-is-full"), user);
            return;
        }

        _rmcPulling.TryStopAllPullsFromAndOn(user);
        _transform.SetCoordinates(targetEntity, coords);
        Dirty(vehicle);
        UpdateVehicleStatusUI(vehicle);
    }

    private void HandleLeavePulling(Entity<VehicleComponent> vehicle, EntityUid user, EntityCoordinates coords)
    {
        EntityUid targetEntity = (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulledUid)
            ? user
            : pulledUid;

        var comp = vehicle.Comp;

        if (HasComp<XenoComponent>(targetEntity))
        {
            if (comp.XenoSlots.Current > 0)
                comp.XenoSlots.Current--;
        }
        else if (_mobState.IsDead(targetEntity) && !HasComp<UnrevivableComponent>(targetEntity))
        {
            if (comp.RevivableDeadSlots.Current > 0)
                comp.RevivableDeadSlots.Current--;
        }
        else if (comp.PassengerSlots.Current > 0)
        {
            comp.PassengerSlots.Current--;
        }

        _rmcPulling.TryStopAllPullsFromAndOn(user);
        _transform.SetCoordinates(targetEntity, coords);
        Dirty(vehicle);
        UpdateVehicleStatusUI(vehicle);
    }

    private bool CanEnter(EntityUid user, EntityUid target)
    {
        if (!TryComp<VehicleComponent>(target, out var vehicle))
            return false;

        if (HasComp<XenoComponent>(user) && vehicle.Destroyed)
        {
            _popup.PopupClient(Loc.GetString("st-vehicle-xeno-push-back-hull"), user);
            return true;
        }

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

    private float GetEntryDelay(Entity<VehicleComponent> vehicle, EntityUid user)
    {
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling != null)
            return vehicle.Comp.EntryDelayPulling;

        if (HasComp<XenoComponent>(user))
            return vehicle.Comp.EntryDelayXeno;

        if (HasComp<MarineComponent>(user))
            return vehicle.Comp.EntryDelay;

        return vehicle.Comp.EntryDelay;
    }

    private bool CheckVehicleAccess(Entity<VehicleComponent> vehicle, EntityUid user)
    {
        var comp = vehicle.Comp;

        if (HasComp<XenoComponent>(user))
            return true;

        if (HasComp<MarineComponent>(user))
        {
            bool hasAccess = _access.IsAllowed(user, vehicle.Owner);

            bool correctFaction = CheckFactionAccess(vehicle.Owner, user);

            if (!hasAccess || !correctFaction)
            {
                _popup.PopupClient(Loc.GetString("st-vehicle-locked"), user);
                return false;
            }

            return true;
        }

        _popup.PopupClient(Loc.GetString("st-vehicle-locked"), user);
        return false;
    }

    private bool CheckFactionAccess(EntityUid vehicle, EntityUid user)
    {
        if (!HasComp<UserIFFComponent>(vehicle))
            return true;

        if (!_gunIFF.TryGetUserFaction(vehicle, out var faction))
            return false;

        return _gunIFF.IsInFaction(user, faction);
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

    private static bool IsWithinRange(double a, double b, float range)
    {
        var delta = ((a - b + 180 + 360) % 360) - 180;
        return Math.Abs(delta) <= range;
    }

    private void UpdateVehicleStatusUI(Entity<VehicleComponent> vehicle)
    {
        var state = new VehicleStatusUIState();
        _ui.SetUiState(vehicle.Owner, VehicleStatusUI.Key, state);
    }
}
