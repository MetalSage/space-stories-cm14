using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Station.Events;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Climbing.Components;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.StepTrigger.Systems;
using Content.Shared._RMC14.Marines.Announce;

namespace Content.Server._RMC14.Vehicle;

public sealed class VehicleSupplySystem : EntitySystem
{
    private readonly record struct HardpointItemInfo(string ProtoId, HashSet<ProtoId<TagPrototype>> Tags);
    private const int VendedHardpointAmmoCount = 3;

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IntelSystem _intel = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCMAutomatedVendorSystem _vendor = default!;
    [Dependency] private readonly VehicleSystem _rmcVehicles = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _announce = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;

    private readonly Dictionary<string, List<HardpointItemInfo>> _hardpointItemsByType = new();
    private readonly Dictionary<string, string> _hardpointTypeByProto = new();
    private readonly Dictionary<string, List<string>> _hardpointsByVehicleCache = new();

    private readonly record struct PreviewOffset(
        Vector2 Base,
        bool UseDirectional,
        Vector2 North,
        Vector2 East,
        Vector2 South,
        Vector2 West);

    private readonly record struct VendorHardpointEntry(
        string Id,
        string SharedKey,
        int SortOrder,
        string DisplayName,
        string SectionName,
        int SectionOrder);

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<VehicleSupplyConsoleComponent, BeforeActivatableUIOpenEvent>(OnConsoleBeforeUiOpen);
        SubscribeLocalEvent<VehicleHardpointVendorComponent, MapInitEvent>(OnVendorMapInit);
        SubscribeLocalEvent<VehicleHardpointVendorComponent, BeforeActivatableUIOpenEvent>(OnVendorBeforeUiOpen);
        SubscribeLocalEvent<VehicleSupplyLiftComponent, MapInitEvent>(OnLiftMapInit);
        SubscribeLocalEvent<ActorComponent, RMCAutomatedVendedUserEvent>(OnAutomatedVendorVended);
        SubscribeLocalEvent<VehicleSupplyLiftComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);

        Subs.BuiEvents<VehicleSupplyConsoleComponent>(VehicleSupplyUIKey.Key, subs =>
        {
            subs.Event<VehicleSupplySelectMsg>(OnVehicleSelected);
            subs.Event<VehicleSupplyLiftMsg>(OnLiftToggleRequested);
            subs.Event<VehicleSupplyPurchaseMsg>(OnVehiclePurchaseRequested);
        });

        SubscribeLocalEvent<TechUnlockVehicleEvent>(OnTechUnlockVehicle);

        ReloadHardpointItems();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private void OnStepTriggerAttempt(Entity<VehicleSupplyLiftComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (ent.Comp.Mode == VehicleSupplyLiftMode.Raised || ent.Comp.Mode == VehicleSupplyLiftMode.Preparing)
            args.Cancelled = true;
    }

    // Stories-Start
    private bool IsHeavyArmor(string key)
    {
        return key.Contains("apc") || key.Contains("tank");
    }
    // Stories-End

    private void RemoveOtherArmorsFromStored(VehicleSupplyLiftComponent comp, string keptKey)
    {
        var keysToRemove = new List<string>();
        foreach (var k in comp.Stored.Keys)
        {
            if (IsHeavyArmor(k) && k != keptKey)
                keysToRemove.Add(k);
        }
        foreach (var k in keysToRemove)
            comp.Stored.Remove(k);
    }

    private static int GetStoredCount(VehicleSupplyLiftComponent lift, string key)
    {
        return lift.Stored.TryGetValue(key, out var count) ? count : 0;
    }

    // Stories-Vehicle-Start
    private int GetVendorAvailableVehicleCount(Entity<VehicleSupplyLiftComponent> lift, string key)
    {
        var count = GetStoredCount(lift.Comp, key);
        var isDeployed = lift.Comp.Deployed.Contains(key);
        var isActive = !string.IsNullOrWhiteSpace(lift.Comp.ActiveVehicleId) && Normalize(lift.Comp.ActiveVehicleId) == key;
        var isPending = !string.IsNullOrWhiteSpace(lift.Comp.PendingVehicle) && Normalize(lift.Comp.PendingVehicle) == key;

        if (isDeployed)
            count++;
        else if (isActive)
            count++;
        else if (isPending)
            count++;

        return count;
    }
    // Stories-Vehicle-End

    private static void AddStored(VehicleSupplyLiftComponent lift, string key, int amount = 1)
    {
        if (amount <= 0)
            return;

        lift.Stored[key] = GetStoredCount(lift, key) + amount;
    }

    private static bool TryRemoveStored(VehicleSupplyLiftComponent lift, string key, int amount = 1)
    {
        if (amount <= 0)
            return true;

        if (!lift.Stored.TryGetValue(key, out var count) || count < amount)
            return false;

        var next = count - amount;
        if (next <= 0)
            lift.Stored.Remove(key);
        else
            lift.Stored[key] = next;

        return true;
    }

    private static void AddStoredEntity(VehicleSupplyLiftComponent lift, string key, EntityUid vehicle)
    {
        if (!lift.StoredEntities.TryGetValue(key, out var list))
        {
            list = new List<EntityUid>();
            lift.StoredEntities[key] = list;
        }

        list.Add(vehicle);
    }

    private bool TryPopStoredEntity(VehicleSupplyLiftComponent lift, string key, out EntityUid vehicle)
    {
        vehicle = default;
        if (!lift.StoredEntities.TryGetValue(key, out var list))
            return false;

        for (var i = list.Count - 1; i >= 0; i--)
        {
            var candidate = list[i];
            list.RemoveAt(i);
            if (Deleted(candidate))
                continue;

            if (list.Count == 0)
                lift.StoredEntities.Remove(key);

            vehicle = candidate;
            return true;
        }

        if (list.Count == 0)
            lift.StoredEntities.Remove(key);

        return false;
    }

    private bool TryTakeStoredEntity(VehicleSupplyLiftComponent lift, string key, int index, out EntityUid vehicle)
    {
        vehicle = default;
        if (!lift.StoredEntities.TryGetValue(key, out var list) || list.Count == 0)
            return false;

        if (index < 0 || index >= list.Count)
            index = list.Count - 1;

        for (var attempts = 0; attempts < list.Count; attempts++)
        {
            var takeIndex = index;
            var candidate = list[takeIndex];
            list.RemoveAt(takeIndex);

            if (Deleted(candidate))
            {
                if (list.Count == 0)
                    break;

                index = Math.Min(index, list.Count - 1);
                continue;
            }

            if (list.Count == 0)
                lift.StoredEntities.Remove(key);

            vehicle = candidate;
            return true;
        }

        if (list.Count == 0)
            lift.StoredEntities.Remove(key);

        return false;
    }

    private bool TryGetStoredEntity(VehicleSupplyLiftComponent lift, string key, int index, out EntityUid vehicle)
    {
        vehicle = default;
        if (!lift.StoredEntities.TryGetValue(key, out var list) || list.Count == 0)
            return false;

        if (index < 0 || index >= list.Count)
            return false;

        var candidate = list[index];
        if (!Deleted(candidate))
        {
            vehicle = candidate;
            return true;
        }

        list.RemoveAt(index);

        if (list.Count == 0)
            lift.StoredEntities.Remove(key);

        return false;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        ReloadHardpointItems();
        _hardpointsByVehicleCache.Clear();
    }

    private void ReloadHardpointItems()
    {
        _hardpointItemsByType.Clear();
        _hardpointTypeByProto.Clear();

        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!proto.TryGetComponent(out HardpointItemComponent? hardpointItem, _compFactory))
                continue;

            _hardpointTypeByProto[Normalize(proto.ID)] = hardpointItem.HardpointType.Id;

            var key = Normalize(hardpointItem.HardpointType.Id);
            if (!_hardpointItemsByType.TryGetValue(key, out var list))
            {
                list = new List<HardpointItemInfo>();
                _hardpointItemsByType[key] = list;
            }

            var tags = new HashSet<ProtoId<TagPrototype>>();
            if (proto.TryGetComponent(out TagComponent? tagComp, _compFactory))
                tags = new HashSet<ProtoId<TagPrototype>>(tagComp.Tags);

            list.Add(new HardpointItemInfo(proto.ID, tags));
        }
    }

    private void OnTechUnlockVehicle(TechUnlockVehicleEvent ev)
    {
        if (string.IsNullOrWhiteSpace(ev.Unlock))
            return;

        var tech = EnsureSupplyTech();
        var unlock = Normalize(ev.Unlock);
        if (!tech.Comp.Unlocked.Contains(unlock))
        {
            tech.Comp.Unlocked.Add(unlock);
            Dirty(tech);
        }

        // Stories-Vehicle-Start
        if (IsHeavyArmor(unlock))
            return;
        // Stories-Vehicle-End

        var liftQuery = EntityQueryEnumerator<VehicleSupplyLiftComponent>();
        while (liftQuery.MoveNext(out var uid, out var lift))
        {
            if (GetStoredCount(lift, unlock) > 0 || lift.Deployed.Contains(unlock))
                continue;

            AddStored(lift, unlock);
            Dirty(uid, lift);
        }

        SendConsoleStateAll();
        UpdateVendorSectionsAll();
    }

    private void OnConsoleBeforeUiOpen(Entity<VehicleSupplyConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        SendConsoleState(ent.Owner, ent.Comp);
    }

    private void OnLiftMapInit(Entity<VehicleSupplyLiftComponent> ent, ref MapInitEvent args)
    {
        SeedStoredFromConsoles(ent);
        Dirty(ent);
    }

    private void SeedStoredFromConsoles(Entity<VehicleSupplyLiftComponent> lift)
    {
        var unlocked = BuildUnlockedSet();
        var mapId = _transform.GetMapId(lift.Owner);

        var query = EntityQueryEnumerator<VehicleSupplyConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var console, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            foreach (var entry in console.Vehicles)
            {
                if (!IsEntryUnlocked(entry, unlocked))
                    continue;

                var key = Normalize(entry.Vehicle.Id);

                // Stories-Start
                if (IsHeavyArmor(key))
                    continue;
                // Stories-End

                if (lift.Comp.Deployed.Contains(key))
                    continue;

                if (GetStoredCount(lift.Comp, key) > 0)
                    continue;

                AddStored(lift.Comp, key);
            }
        }
    }

    private void OnVendorMapInit(Entity<VehicleHardpointVendorComponent> ent, ref MapInitEvent args)
    {
        UpdateVendorSections(ent.Owner, ent.Comp);
    }

    private void OnVendorBeforeUiOpen(Entity<VehicleHardpointVendorComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateVendorSections(ent.Owner, ent.Comp);
    }

    private void OnAutomatedVendorVended(Entity<ActorComponent> ent, ref RMCAutomatedVendedUserEvent args)
    {
        if (!HasComp<HardpointItemComponent>(args.Item))
            return;

        TrySpawnVendedHardpointAmmo(ent.Owner, args.Item);
        UpdateVendorSectionsAll();
    }

    private void TrySpawnVendedHardpointAmmo(EntityUid user, EntityUid hardpointItem)
    {
        if (!TryComp(hardpointItem, out VehicleHardpointAmmoComponent? _) ||
            !TryComp(hardpointItem, out RefillableByBulletBoxComponent? refillable) ||
            refillable.BulletType is not { } bulletType)
        {
            return;
        }

        if (!TryResolveVendedAmmoPrototype(bulletType, out var ammoProto))
            return;

        for (var i = 0; i < VendedHardpointAmmoCount; i++)
        {
            SpawnNextToOrDrop(ammoProto, user);
        }
    }

    private bool TryResolveVendedAmmoPrototype(EntProtoId bulletType, out EntProtoId ammoProto)
    {
        ammoProto = bulletType;

        if (_prototypes.TryIndex<EntityPrototype>(bulletType, out var exact) && !exact.Abstract)
            return true;

        string? fallback = null;
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!proto.TryGetComponent(out BulletBoxComponent? box, _compFactory))
                continue;

            if (box.BulletType != bulletType)
                continue;

            if (fallback == null || string.CompareOrdinal(proto.ID, fallback) < 0)
                fallback = proto.ID;
        }

        if (fallback == null)
            return false;

        ammoProto = fallback;
        return true;
    }

    private void OnVehicleSelected(Entity<VehicleSupplyConsoleComponent> ent, ref VehicleSupplySelectMsg args)
    {
        if (string.IsNullOrWhiteSpace(args.VehicleId))
            return;

        if (!TryGetLift(ent.Owner, ent.Comp, out var lift))
            return;

        var key = Normalize(args.VehicleId);
        var isHeavy = IsHeavyArmor(key);

        if (!isHeavy)
        {
            if (!TryGetEntry(ent.Comp, args.VehicleId, out var entry))
                return;

            var unlocked = BuildUnlockedSet();
            if (!IsEntryUnlocked(entry, unlocked))
                return;
        }

        if (Normalize(lift.Comp.PendingVehicle) == key)
            return;

        ent.Comp.SelectedVehicle = args.VehicleId;
        ent.Comp.SelectedVehicleCopyIndex = Math.Max(0, args.CopyIndex);
        SendConsoleStateAll();
    }

    private void OnVehiclePurchaseRequested(Entity<VehicleSupplyConsoleComponent> ent, ref VehicleSupplyPurchaseMsg args)
    {
        if (string.IsNullOrWhiteSpace(args.VehicleId))
            return;

        if (!TryGetLift(ent.Owner, ent.Comp, out var lift))
            return;

        if (lift.Comp.HasDispensedArmor)
            return;

        var key = Normalize(args.VehicleId);

        // Stories-Start
        if (!IsHeavyArmor(key))
            return;

        var sessionCount = _player.PlayerCount;
        var lowPop = _cfg.GetCVar(SCCVars.RMCLowPopVehicle);
        var highPop = _cfg.GetCVar(SCCVars.RMCHighPopVehicle);

        if (key.Contains("apc") && sessionCount < lowPop)
            return;
        if (key.Contains("tank") && sessionCount < highPop)
            return;

        if (!_prototypes.HasIndex<EntityPrototype>(args.VehicleId))
            return;
        // Stories-End

        lift.Comp.HasDispensedArmor = true;
        AddStored(lift.Comp, key);

        var vehicleName = GetPrototypeName(args.VehicleId);
        _announce.AnnounceARES(null, Loc.GetString("rmc-vehicle-announcement-armor-deployed", ("vehicle", vehicleName)));

        Dirty(lift.Owner, lift.Comp);
        SendConsoleStateAll();
        UpdateVendorSectionsAll();
    }

    private void OnLiftToggleRequested(Entity<VehicleSupplyConsoleComponent> ent, ref VehicleSupplyLiftMsg args)
    {
        if (!TryGetLift(ent.Owner, ent.Comp, out var lift))
            return;

        TryToggleLift(ent, lift, args.Raise);
    }

    private void TryToggleLift(Entity<VehicleSupplyConsoleComponent> console, Entity<VehicleSupplyLiftComponent> lift, bool raise)
    {
        var comp = lift.Comp;
        if (comp.NextMode != null || comp.Busy)
            return;

        if (comp.Mode == VehicleSupplyLiftMode.Lowering || comp.Mode == VehicleSupplyLiftMode.Raising)
            return;

        if (raise)
        {
            if (comp.Mode == VehicleSupplyLiftMode.Raised)
                return;
            var selected = console.Comp.SelectedVehicle;
            var canQueueVehicle = false;
            string? nextVehicle = null;

            if (!string.IsNullOrWhiteSpace(selected))
            {
                var key = Normalize(selected);
                var isHeavy = IsHeavyArmor(key);
                var valid = false;

                // Stories-Vehicle-Start
                if (GetStoredCount(comp, key) > 0)
                {
                    valid = true;
                }
                else if (isHeavy)
                {
                    valid = true;
                }
                else if (TryGetEntry(console.Comp, selected, out var entry))
                {
                    var unlocked = BuildUnlockedSet();
                    if (IsEntryUnlocked(entry, unlocked))
                        valid = true;
                }
                // Stories-Vehicle-End

                if (valid)
                {
                    var count = GetStoredCount(comp, key);

                    if (count > 0 && _prototypes.TryIndex<EntityPrototype>(selected, out _))
                    {
                        if (TryRemoveStored(comp, key))
                        {
                            canQueueVehicle = true;
                            nextVehicle = selected;
                            comp.PendingVehicleEntity = null;
                            if (TryTakeStoredEntity(comp, key, console.Comp.SelectedVehicleCopyIndex, out var pendingEntity))
                                comp.PendingVehicleEntity = pendingEntity;

                            console.Comp.SelectedVehicle = string.Empty;
                            console.Comp.SelectedVehicleCopyIndex = 0;
                        }
                    }
                }
            }

            if (canQueueVehicle && nextVehicle != null)
            {
                comp.PendingVehicle = nextVehicle;
            }
            else
            {
                comp.PendingVehicle = string.Empty;
                comp.PendingVehicleEntity = null;
            }

            UpdateVendorSectionsAll();
        }
        else
        {
            if (comp.Mode == VehicleSupplyLiftMode.Lowered)
                return;

            if (IsLoweringBlocked(lift))
                return;
        }

        comp.ToggledAt = _timing.CurTime;
        comp.Busy = true;
        SetMode(lift, VehicleSupplyLiftMode.Preparing, raise ? VehicleSupplyLiftMode.Raising : VehicleSupplyLiftMode.Lowering);
    }

    private bool IsLoweringBlocked(Entity<VehicleSupplyLiftComponent> lift)
    {
        if (lift.Comp.ActiveVehicle is { } active &&
            IsOnLift(lift, active) &&
            _rmcVehicles.TryGetInteriorMapId(active, out var interiorMap))
        {
            var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (actorQuery.MoveNext(out _, out _, out var xform))
            {
                if (xform.MapID == interiorMap)
                    return true;
            }
        }

        var mask = (int)(CollisionGroup.MobLayer | CollisionGroup.MobMask);
        foreach (var entity in _physics.GetEntitiesIntersectingBody(lift, mask, false))
        {
            if (HasComp<MobStateComponent>(entity))
                return true;
        }

        return false;
    }

    private void SetMode(Entity<VehicleSupplyLiftComponent> lift, VehicleSupplyLiftMode mode, VehicleSupplyLiftMode? nextMode)
    {
        lift.Comp.Mode = mode;
        lift.Comp.NextMode = nextMode;
        Dirty(lift);

        RequisitionsGearMode? gearMode = mode switch
        {
            VehicleSupplyLiftMode.Lowered or VehicleSupplyLiftMode.Raised or VehicleSupplyLiftMode.Preparing => RequisitionsGearMode.Static,
            VehicleSupplyLiftMode.Lowering or VehicleSupplyLiftMode.Raising => RequisitionsGearMode.Moving,
            _ => null
        };

        if (gearMode != null)
            UpdateGears(lift, gearMode.Value);

        RequisitionsRailingMode? railingMode = (mode, nextMode) switch
        {
            (VehicleSupplyLiftMode.Lowered, _) => RequisitionsRailingMode.Raised,
            (VehicleSupplyLiftMode.Raised, _) => RequisitionsRailingMode.Lowering,
            (_, VehicleSupplyLiftMode.Lowering) => RequisitionsRailingMode.Raising,
            _ => null
        };

        if (railingMode != null)
            UpdateRailings(lift, railingMode.Value);

        SendConsoleStateAll();
    }

    private void UpdateRailings(Entity<VehicleSupplyLiftComponent> elevator, RequisitionsRailingMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator.Owner);
        var railings = _lookup.GetEntitiesInRange<RequisitionsRailingComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            SetRailingMode(railing, mode);
        }
    }

    private void UpdateGears(Entity<VehicleSupplyLiftComponent> elevator, RequisitionsGearMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator.Owner);
        var gears = _lookup.GetEntitiesInRange<RequisitionsGearComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var gear in gears)
        {
            if (gear.Comp.Mode == mode)
                continue;

            gear.Comp.Mode = mode;
            Dirty(gear);
        }
    }

    private void SetRailingMode(Entity<RequisitionsRailingComponent> railing, RequisitionsRailingMode mode)
    {
        if (railing.Comp.Mode == mode)
            return;

        railing.Comp.Mode = mode;
        Dirty(railing);

        if (!TryComp(railing, out Robust.Shared.Physics.FixturesComponent? fixtures) ||
            _fixtures.GetFixtureOrNull(railing, railing.Comp.Fixture, fixtures) is not { } fixture)
        {
            return;
        }

        var hard = mode is RequisitionsRailingMode.Raising or RequisitionsRailingMode.Raised;
        _physics.SetHard(railing, fixture, hard);

        if (hard)
            EnsureComp<ClimbableComponent>(railing);
        else
            RemCompDeferred<ClimbableComponent>(railing);
    }

    private void TryPlayAudio(Entity<VehicleSupplyLiftComponent> lift)
    {
        var comp = lift.Comp;
        if (comp.Audio != null || comp.ToggledAt == null)
            return;

        var time = _timing.CurTime;
        if (comp.NextMode == VehicleSupplyLiftMode.Lowering || comp.Mode == VehicleSupplyLiftMode.Lowering)
        {
            if (time < comp.ToggledAt + comp.LowerSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.LoweringSound, lift)?.Entity;
            return;
        }

        if (comp.NextMode == VehicleSupplyLiftMode.Raising || comp.Mode == VehicleSupplyLiftMode.Raising)
        {
            if (time < comp.ToggledAt + comp.RaiseSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.RaisingSound, lift)?.Entity;
        }
    }

    // Stories-Vehicle-Start
    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        var threshold = _cfg.GetCVar(SCCVars.RMCLowPopVehicle);
        var totalPlayers = _player.PlayerCount;
        var crewmanSlots = totalPlayers >= threshold ? 2 : 0;

        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var stationId, out var jobs))
        {
            if (!_stationJobs.TryGetJobSlot(stationId, "CMVehicleCrewman", out _, jobs))
                continue;

            _stationJobs.TrySetJobSlot(stationId, "CMVehicleCrewman", crewmanSlots, stationJobs: jobs);
        }
    }
    // Stories-Vehicle-End

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var updateUi = false;
        var liftQuery = EntityQueryEnumerator<VehicleSupplyLiftComponent>();
        while (liftQuery.MoveNext(out var uid, out var lift))
        {
            if (CleanupDestroyedActive((uid, lift)))
                updateUi = true;

            // Stories-Vehicle-Start
            if (CleanupDrivenOffActive((uid, lift)))
                updateUi = true;
            // Stories-Vehicle-End

            if (ProcessLift((uid, lift)))
                updateUi = true;
        }

        if (updateUi)
            SendConsoleStateAll();
    }

    private bool CleanupDestroyedActive(Entity<VehicleSupplyLiftComponent> lift)
    {
        var comp = lift.Comp;
        if (comp.ActiveVehicle == null)
            return false;

        var active = comp.ActiveVehicle.Value;
        if (Deleted(active))
        {
            if (!string.IsNullOrWhiteSpace(comp.ActiveVehicleId))
                comp.Deployed.Remove(Normalize(comp.ActiveVehicleId));

            comp.ActiveVehicle = null;
            comp.ActiveVehicleId = string.Empty;
            return true;
        }

        return false;
    }

    // Stories-Vehicle-Start
    private bool CleanupDrivenOffActive(Entity<VehicleSupplyLiftComponent> lift)
    {
        var comp = lift.Comp;
        if (comp.ActiveVehicle == null)
            return false;

        var active = comp.ActiveVehicle.Value;
        if (Exists(active) && !IsOnLift(lift, active))
        {
            comp.ActiveVehicle = null;
            comp.ActiveVehicleId = string.Empty;
            return true;
        }

        return false;
    }
    // Stories-Vehicle-End

    private bool ProcessLift(Entity<VehicleSupplyLiftComponent> lift)
    {
        var comp = lift.Comp;
        if (comp.ToggledAt == null)
            return false;

        var time = _timing.CurTime;
        if (time > comp.ToggledAt + comp.ToggleDelay)
        {
            comp.ToggledAt = null;
            comp.Busy = false;
            Dirty(lift);
            return true;
        }

        TryPlayAudio(lift);

        var delay = comp.NextMode == VehicleSupplyLiftMode.Raising ? comp.RaiseDelay : comp.LowerDelay;
        if (comp.Mode == VehicleSupplyLiftMode.Preparing &&
            comp.NextMode != null &&
            time > comp.ToggledAt + delay)
        {
            SetMode(lift, comp.NextMode.Value, null);
            return true;
        }

        if (comp.Mode != VehicleSupplyLiftMode.Lowering && comp.Mode != VehicleSupplyLiftMode.Raising)
            return false;

        var moveDelay = delay + (comp.Mode == VehicleSupplyLiftMode.Raising ? comp.RaiseDelay : comp.LowerDelay);
        if (time > comp.ToggledAt + moveDelay)
        {
            comp.Audio = null;

            var mode = comp.Mode == VehicleSupplyLiftMode.Raising
                ? VehicleSupplyLiftMode.Raised
                : VehicleSupplyLiftMode.Lowered;

            SetMode(lift, mode, comp.NextMode);
            if (mode == VehicleSupplyLiftMode.Raised)
                SpawnVehicle(lift);
            else
                StoreVehicle(lift);

            comp.ToggledAt = null;
            comp.Busy = false;
            Dirty(lift);
            return true;
        }

        return false;
    }

    private void SpawnVehicle(Entity<VehicleSupplyLiftComponent> lift)
    {
        var comp = lift.Comp;
        var pending = comp.PendingVehicle;
        if (string.IsNullOrWhiteSpace(pending))
            return;

        var key = Normalize(pending);
        if (comp.PendingVehicleEntity is { } pendingEntity && Exists(pendingEntity))
        {
            var moverCoords = _transform.GetMoverCoordinates(lift);
            var mapCoords = _transform.ToMapCoordinates(moverCoords);
            _transform.SetMapCoordinates(pendingEntity, mapCoords);

            comp.ActiveVehicle = pendingEntity;
            comp.ActiveVehicleId = pending;
            comp.PendingVehicle = string.Empty;
            comp.PendingVehicleEntity = null;
            comp.Deployed.Add(key);
            return;
        }

        comp.PendingVehicleEntity = null;
        if (TryPopStoredEntity(comp, key, out var stored))
        {
            var moverCoords = _transform.GetMoverCoordinates(lift);
            var mapCoords = _transform.ToMapCoordinates(moverCoords);
            _transform.SetMapCoordinates(stored, mapCoords);

            comp.ActiveVehicle = stored;
            comp.ActiveVehicleId = pending;
            comp.PendingVehicle = string.Empty;
            comp.Deployed.Add(key);
            return;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(pending, out _))
        {
            AddStored(comp, key);
            comp.PendingVehicle = string.Empty;
            UpdateVendorSectionsAll();
            return;
        }

        var spawnCoords = _transform.GetMoverCoordinates(lift);
        var vehicle = SpawnAtPosition(pending, spawnCoords);

        comp.ActiveVehicle = vehicle;
        comp.ActiveVehicleId = pending;
        comp.PendingVehicle = string.Empty;
        comp.Deployed.Add(key);
    }

    private void StoreVehicle(Entity<VehicleSupplyLiftComponent> lift)
    {
        var comp = lift.Comp;
        if (comp.ActiveVehicle == null)
            return;

        var active = comp.ActiveVehicle.Value;
        if (!IsOnLift(lift, active))
            return;

        if (!string.IsNullOrWhiteSpace(comp.ActiveVehicleId))
        {
            var key = Normalize(comp.ActiveVehicleId);
            comp.Deployed.Remove(key);
            AddStored(comp, key);
            AddStoredEntity(comp, key, active);
        }

        _transform.SetParent(active, EntityUid.Invalid);
        comp.ActiveVehicle = null;
        comp.ActiveVehicleId = string.Empty;
        UpdateVendorSectionsAll();
    }

    private bool IsOnLift(Entity<VehicleSupplyLiftComponent> lift, EntityUid entity)
    {
        if (!TryComp(lift.Owner, out TransformComponent? liftXform) ||
            !TryComp(entity, out TransformComponent? entityXform))
        {
            return false;
        }

        var liftCoords = _transform.GetMapCoordinates(lift.Owner, liftXform);
        var entityCoords = _transform.GetMapCoordinates(entity, entityXform);
        if (liftCoords.MapId != entityCoords.MapId)
            return false;

        var radius = lift.Comp.Radius;
        return (entityCoords.Position - liftCoords.Position).LengthSquared() <= radius * radius;
    }

    private void SendConsoleStateAll()
    {
        var query = EntityQueryEnumerator<VehicleSupplyConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            SendConsoleState(uid, comp);
        }
    }

    private void SendConsoleState(EntityUid uid, VehicleSupplyConsoleComponent? console = null)
    {
        if (!Resolve(uid, ref console, logMissing: false))
            return;

        var unlocked = BuildUnlockedSet();
        var available = new List<VehicleSupplyEntryState>();

        VehicleSupplyLiftMode? mode = null;
        var busy = false;
        string? activeId = null;
        string? selectedId = string.IsNullOrWhiteSpace(console.SelectedVehicle) ? null : console.SelectedVehicle;
        var selectedCopyIndex = console.SelectedVehicleCopyIndex;
        VehicleSupplyPreviewState? preview = null;

        var hasLift = TryGetLift(uid, console, out var lift);
        if (hasLift)
        {
            mode = lift.Comp.Mode;
            busy = lift.Comp.Busy;
            activeId = string.IsNullOrWhiteSpace(lift.Comp.ActiveVehicleId) ? null : lift.Comp.ActiveVehicleId;

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                var key = Normalize(selectedId);
                var layers = new List<VehicleHardpointLayerState>();
                var overlays = new List<VehicleSupplyPreviewOverlay>();

                if (TryGetStoredEntity(lift.Comp, key, selectedCopyIndex, out var stored))
                {
                    layers = BuildPreviewLayers(stored);
                    overlays = BuildPreviewOverlays(stored);
                }

                preview = new VehicleSupplyPreviewState(selectedId, layers, overlays);
            }
        }

        var sessionCount = _player.PlayerCount;
        var lowPop = _cfg.GetCVar(SCCVars.RMCLowPopVehicle);
        var highPop = _cfg.GetCVar(SCCVars.RMCHighPopVehicle);

        // Stories-Vehicle-Start
        if (hasLift)
        {
            var addedKeys = new HashSet<string>();

            if (!lift.Comp.HasDispensedArmor)
            {
                var options = new List<string>();

                if (sessionCount >= lowPop)
                {
                    options.Add("VehicleAPC");
                    options.Add("VehicleAPCMed");
                    options.Add("VehicleAPCCommand");
                }
                if (sessionCount >= highPop)
                {
                    options.Add("VehicleTank");
                }

                if (options.Count > 0)
                {
                    foreach (var opt in options)
                    {
                        string name = opt;
                        if (_prototypes.TryIndex<EntityPrototype>(opt, out var proto))
                            name = proto.Name;

                        available.Add(new VehicleSupplyEntryState(opt, name, 0, false, true));
                        addedKeys.Add(Normalize(opt));
                    }
                }
                else
                {
                    available.Add(new VehicleSupplyEntryState("", Loc.GetString("rmc-vehicle-supply-locked-pop"), 0, true, true));
                }
            }

            foreach (var entry in console.Vehicles)
            {
                var key = Normalize(entry.Vehicle.Id);
                var count = GetStoredCount(lift.Comp, key);
                var isActiveOrPending = Normalize(lift.Comp.ActiveVehicleId) == key || Normalize(lift.Comp.PendingVehicle) == key;

                if (!addedKeys.Contains(key) && (count > 0 || isActiveOrPending))
                {
                    available.Add(new VehicleSupplyEntryState(entry.Vehicle.Id, GetEntryName(entry), count > 0 ? count : 1, false, false));
                    addedKeys.Add(key);
                }
            }

            var allTrackedKeys = new HashSet<string>(lift.Comp.Stored.Keys);
            if (!string.IsNullOrWhiteSpace(lift.Comp.ActiveVehicleId)) allTrackedKeys.Add(Normalize(lift.Comp.ActiveVehicleId));
            if (!string.IsNullOrWhiteSpace(lift.Comp.PendingVehicle)) allTrackedKeys.Add(Normalize(lift.Comp.PendingVehicle));

            foreach (var key in allTrackedKeys)
            {
                if (addedKeys.Contains(key)) continue;

                var count = GetStoredCount(lift.Comp, key);
                var isActiveOrPending = Normalize(lift.Comp.ActiveVehicleId) == key || Normalize(lift.Comp.PendingVehicle) == key;

                if (count > 0 || isActiveOrPending)
                {
                    string originalId = key;
                    string name = key;
                    foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
                    {
                        if (Normalize(proto.ID) == key)
                        {
                            originalId = proto.ID;
                            name = proto.Name;
                            break;
                        }
                    }

                    available.Add(new VehicleSupplyEntryState(originalId, name, count, false, false));
                    addedKeys.Add(key);
                }
            }
        }
        else
        {
            foreach (var entry in console.Vehicles)
            {
                if (IsEntryUnlocked(entry, unlocked))
                    available.Add(new VehicleSupplyEntryState(entry.Vehicle.Id, GetEntryName(entry), 1, false, false));
            }
        }
        // Stories-Vehicle-End

        console.Ui = new VehicleSupplyUiState(mode, busy, activeId, selectedId, selectedCopyIndex, preview, available);
        Dirty(uid, console);
    }

    private void UpdateVendorSectionsAll()
    {
        var query = EntityQueryEnumerator<VehicleHardpointVendorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateVendorSections(uid, comp);
        }
    }

    private int GetHardpointTypeOrder(string type)
    {
        return type switch
        {
            "HardpointTypeTurret" => 0,
            "HardpointTypePrimary" => 1,
            "HardpointTypeCannon" => 1,
            "HardpointTypeSecondary" => 2,
            "HardpointTypeLauncher" => 2,
            "HardpointTypeArmor" => 3,
            "HardpointTypeFrontAttachment" => 4,
            "HardpointTypeRoofAttachment" => 5,
            "HardpointTypeSupport" => 6,
            "HardpointTypeSupportAttachment" => 6,
            "HardpointTypeWheel" => 7,
            _ => 10
        };
    }

    private void UpdateVendorSections(
        EntityUid uid,
        VehicleHardpointVendorComponent? vendor = null,
        CMAutomatedVendorComponent? automated = null)
    {
        if (!Resolve(uid, ref vendor, ref automated, logMissing: false))
            return;

        var hasLift = TryGetLiftForVendor(uid, vendor, out var lift);

        var catalog = BuildVendorCatalog(uid, vendor);
        var unlocked = BuildUnlockedSet();

        var existingAmounts = new Dictionary<EntProtoId, int>();
        foreach (var section in automated.Sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Amount != null)
                    existingAmounts[entry.Id] = entry.Amount.Value;
            }
        }

        var previousCounts = new Dictionary<string, int>(vendor.LastVehicleCounts);
        vendor.LastVehicleCounts.Clear();
        var validGroupStateKeys = new HashSet<string>();

        var sections = new List<CMVendorSection>();
        foreach (var entry in catalog)
        {
            var vehicleKey = Normalize(entry.Vehicle.Id);

            // Stories-Start
            if (!IsHeavyArmor(vehicleKey) && !IsEntryUnlocked(entry, unlocked))
                continue;
            // Stories-End

            var count = hasLift ? GetVendorAvailableVehicleCount(lift, vehicleKey) : 0;
            var lastCount = previousCounts.TryGetValue(vehicleKey, out var prev) ? prev : 0;
            var delta = count - lastCount;
            vendor.LastVehicleCounts[vehicleKey] = count;

            var hardpoints = GetHardpointsForVehicle(entry.Vehicle.Id, catalog);
            if (hardpoints.Count == 0)
                continue;

            var hardpointEntries = new List<VendorHardpointEntry>();
            foreach (var hardpoint in hardpoints)
            {
                if (string.IsNullOrWhiteSpace(hardpoint))
                    continue;

                var displayName = GetPrototypeName(hardpoint);
                var sharedKey = hardpoint;
                var order = int.MaxValue;
                var sectionName = Loc.GetString("rmc-hardpoint-category-unknown");
                var sectionOrder = 99;

                if (TryGetTankSharedCategory(entry.Vehicle.Id, hardpoint, out var catKey, out var catLabel, out var catOrder))
                {
                    sharedKey = catKey;
                    sectionName = catLabel;
                    sectionOrder = catOrder;
                    order = 0;
                }
                else if (_hardpointTypeByProto.TryGetValue(Normalize(hardpoint), out var hType))
                {
                    // Stories-Vehicle-Start
                    sharedKey = hType;
                    sectionName = Loc.GetString($"rmc-hardpoint-category-{hType}");
                    sectionOrder = GetHardpointTypeOrder(hType);
                    order = 0;
                    // Stories-Vehicle-End
                }

                hardpointEntries.Add(new VendorHardpointEntry(
                    hardpoint,
                    sharedKey,
                    order,
                    displayName,
                    sectionName,
                    sectionOrder));
            }

            if (hardpointEntries.Count == 0)
                continue;

            var groupedBySharedKey = new Dictionary<string, List<EntProtoId>>();
            foreach (var hardpoint in hardpointEntries)
            {
                var id = new EntProtoId(hardpoint.Id);
                if (!groupedBySharedKey.TryGetValue(hardpoint.SharedKey, out var list))
                {
                    list = new List<EntProtoId>();
                    groupedBySharedKey[hardpoint.SharedKey] = list;
                }

                list.Add(id);
            }

            if (count <= 0)
            {
                foreach (var sharedKey in groupedBySharedKey.Keys)
                {
                    var groupStateKey = $"{vehicleKey}:{sharedKey}";
                    vendor.RemainingGroupAmounts.Remove(groupStateKey);
                }

                continue;
            }

            var sharedAmounts = new Dictionary<string, int>();
            foreach (var (sharedKey, ids) in groupedBySharedKey)
            {
                var groupStateKey = $"{vehicleKey}:{sharedKey}";
                validGroupStateKeys.Add(groupStateKey);

                var remaining = vendor.RemainingGroupAmounts.TryGetValue(groupStateKey, out var tracked)
                    ? tracked
                    : lastCount;

                var hasExistingForGroup = false;
                var minExisting = int.MaxValue;
                foreach (var id in ids)
                {
                    if (!existingAmounts.TryGetValue(id, out var existing))
                        continue;

                    hasExistingForGroup = true;
                    if (existing < minExisting)
                        minExisting = existing;
                }

                if (hasExistingForGroup)
                    remaining = minExisting;

                if (delta > 0)
                    remaining += delta;

                remaining = Math.Clamp(remaining, 0, count);
                vendor.RemainingGroupAmounts[groupStateKey] = remaining;
                sharedAmounts[sharedKey] = remaining;
            }

            foreach (var sectionGroup in hardpointEntries
                         .GroupBy(h => h.SectionName)
                         .OrderBy(g => g.Min(h => h.SectionOrder))
                         .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var section = new CMVendorSection
                {
                    Name = sectionGroup.Key,
                    Entries = new List<CMVendorEntry>()
                };

                foreach (var hardpoint in sectionGroup
                             .OrderBy(h => h.SortOrder)
                             .ThenBy(h => h.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    if (!sharedAmounts.TryGetValue(hardpoint.SharedKey, out var amount))
                        continue;

                    var id = new EntProtoId(hardpoint.Id);

                    if (section.Entries.Any(e => e.Id == id))
                        continue;

                    section.Entries.Add(new CMVendorEntry
                    {
                        Id = id,
                        Name = hardpoint.DisplayName,
                        Amount = amount,
                        Multiplier = amount,
                        Max = amount
                    });
                }

                if (section.Entries.Count > 0)
                {
                    var existingSection = sections.FirstOrDefault(s => s.Name == section.Name);
                    if (existingSection != null)
                    {
                        foreach (var e in section.Entries)
                        {
                            if (!existingSection.Entries.Any(x => x.Id == e.Id))
                                existingSection.Entries.Add(e);
                        }
                    }
                    else
                    {
                        sections.Add(section);
                    }
                }
            }
        }

        var staleGroupKeys = vendor.RemainingGroupAmounts.Keys
            .Where(key => !validGroupStateKeys.Contains(key))
            .ToArray();
        foreach (var key in staleGroupKeys)
        {
            vendor.RemainingGroupAmounts.Remove(key);
        }

        sections = sections.OrderBy(s => GetHardpointTypeOrder(s.Name.ToLowerInvariant())).ToList(); // fallback sort

        _vendor.SetSections((uid, automated), sections);
    }

    private bool TryGetTankSharedCategory(
        string vehicleId,
        string hardpointId,
        out string categoryKey,
        out string categoryLabel,
        out int categoryOrder)
    {
        categoryKey = string.Empty;
        categoryLabel = string.Empty;
        categoryOrder = int.MaxValue;

        if (!string.Equals(Normalize(vehicleId), "vehicletank", StringComparison.Ordinal))
            return false;

        var hardpointKey = Normalize(hardpointId);
        if (hardpointKey == "vehicletanksnowplow")
        {
            // Stories-Vehicle-Start
            categoryKey = "tank-general";
            categoryLabel = Loc.GetString("rmc-hardpoint-category-general");
            categoryOrder = 5;
            return true;
            // Stories-Vehicle-End
        }

        if (!_hardpointTypeByProto.TryGetValue(Normalize(hardpointId), out var hardpointType))
            return false;

        switch (Normalize(hardpointType))
        {
            case "hardpointtypecannon":
                categoryKey = "tank-primary";
                categoryLabel = Loc.GetString("rmc-hardpoint-category-primary");
                categoryOrder = 0;
                return true;
            case "hardpointtypelauncher":
                categoryKey = "tank-secondary";
                categoryLabel = Loc.GetString("rmc-hardpoint-category-secondary");
                categoryOrder = 1;
                return true;
            case "hardpointtypearmor":
                categoryKey = "tank-armor";
                categoryLabel = Loc.GetString("rmc-hardpoint-category-armor");
                categoryOrder = 2;
                return true;
            // Stories-Vehicle-Start
            case "hardpointtypesupport":
                categoryKey = "tank-support";
                categoryLabel = Loc.GetString("rmc-hardpoint-category-support");
                categoryOrder = 3;
                return true;
            case "hardpointtypewheel":
                categoryKey = "tank-treads";
                categoryLabel = Loc.GetString("rmc-hardpoint-category-wheel");
                categoryOrder = 4;
                return true;
            // Stories-Vehicle-End
            default:
                return false;
        }
    }

    private bool TryGetLiftForVendor(
        EntityUid vendorUid,
        VehicleHardpointVendorComponent vendor,
        out Entity<VehicleSupplyLiftComponent> lift)
    {
        lift = default;
        var found = false;

        var vendorCoords = _transform.GetMapCoordinates(vendorUid);
        var maxDistance = vendor.ConsoleSearchRange * vendor.ConsoleSearchRange;

        if (TryFindLiftForVendor(vendorCoords, maxDistance, true, out var rangedLift))
        {
            lift = rangedLift;
            return true;
        }

        if (TryFindLiftForVendor(vendorCoords, maxDistance, false, out var anyLift))
        {
            lift = anyLift;
            return true;
        }

        return found;
    }

    private bool TryFindLiftForVendor(
        MapCoordinates vendorCoords,
        float maxDistance,
        bool useRange,
        out Entity<VehicleSupplyLiftComponent> lift)
    {
        lift = default;
        var found = false;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<VehicleSupplyLiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            var liftCoords = _transform.GetMapCoordinates(uid, xform);
            if (liftCoords.MapId != vendorCoords.MapId)
                continue;

            var distance = (liftCoords.Position - vendorCoords.Position).LengthSquared();
            if (useRange && distance > maxDistance)
                continue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            lift = (uid, comp);
            found = true;
        }

        return found;
    }

    public bool TryGetAnyLift(out Entity<VehicleSupplyLiftComponent> lift)
    {
        var query = EntityQueryEnumerator<VehicleSupplyLiftComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            lift = (uid, comp);
            return true;
        }

        lift = default;
        return false;
    }

    public bool DebugAddVehicleToStorage(EntityUid liftUid, string vehicleId, bool forceUnlock, out string? reason)
    {
        reason = null;

        if (!TryComp(liftUid, out VehicleSupplyLiftComponent? lift))
        {
            reason = Loc.GetString("rmc-vehicle-supply-cmd-err-no-comp", ("lift", liftUid));
            return false;
        }

        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            reason = Loc.GetString("rmc-vehicle-supply-cmd-err-empty");
            return false;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(vehicleId, out _))
        {
            reason = Loc.GetString("rmc-vehicle-supply-cmd-err-unknown", ("vehicle", vehicleId));
            return false;
        }

        var key = Normalize(vehicleId);

        if (forceUnlock)
        {
            var tech = EnsureSupplyTech();
            if (!tech.Comp.Unlocked.Contains(key))
            {
                tech.Comp.Unlocked.Add(key);
                Dirty(tech);
            }
        }

        AddStored(lift, key);

        Dirty(liftUid, lift);
        SendConsoleStateAll();
        UpdateVendorSectionsAll();
        return true;
    }

    public bool DebugEnsureVehicleOnAnyLift(string vehicleId, bool forceUnlock, out string? reason)
    {
        reason = null;

        if (!TryGetAnyLift(out var lift))
        {
            reason = "No vehicle lift found.";
            return false;
        }

        var result = DebugEnsureVehicleInStorage(lift.Owner, vehicleId, forceUnlock, out reason);
        if (result)
            DebugEnsureVehicleInConsoles(lift.Owner, vehicleId);

        return result;
    }

    public bool DebugEnsureVehicleInStorage(EntityUid liftUid, string vehicleId, bool forceUnlock, out string? reason)
    {
        reason = null;

        if (!TryComp(liftUid, out VehicleSupplyLiftComponent? lift))
        {
            reason = $"Entity {liftUid} does not have VehicleSupplyLiftComponent.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            reason = "Vehicle id is empty.";
            return false;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(vehicleId, out _))
        {
            reason = $"Unknown vehicle prototype '{vehicleId}'.";
            return false;
        }

        var key = Normalize(vehicleId);

        if (forceUnlock)
        {
            var tech = EnsureSupplyTech();
            if (!tech.Comp.Unlocked.Contains(key))
            {
                tech.Comp.Unlocked.Add(key);
                Dirty(tech);
            }
        }

        var alreadyAvailable =
            GetStoredCount(lift, key) > 0 ||
            lift.Deployed.Contains(key) ||
            (!string.IsNullOrWhiteSpace(lift.PendingVehicle) && Normalize(lift.PendingVehicle) == key) ||
            (!string.IsNullOrWhiteSpace(lift.ActiveVehicleId) && Normalize(lift.ActiveVehicleId) == key);

        if (!alreadyAvailable)
            AddStored(lift, key);

        Dirty(liftUid, lift);
        SendConsoleStateAll();
        UpdateVendorSectionsAll();
        return true;
    }

    public void DebugEnsureVehicleInConsoles(EntityUid liftUid, string vehicleId)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(vehicleId, out var proto))
            return;

        var mapId = _transform.GetMapId(liftUid);
        var query = EntityQueryEnumerator<VehicleSupplyConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var console, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            if (TryGetEntry(console, vehicleId, out _))
                continue;

            console.Vehicles.Add(new VehicleSupplyEntry
            {
                Vehicle = vehicleId,
                Unlock = vehicleId,
                Name = proto.Name
            });

            SendConsoleState(uid, console);
        }

        UpdateVendorSectionsAll();
    }

    private bool TryGetLift(EntityUid consoleUid, VehicleSupplyConsoleComponent console, out Entity<VehicleSupplyLiftComponent> lift)
    {
        lift = default;
        var found = false;

        var consoleCoords = _transform.GetMapCoordinates(consoleUid);
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<VehicleSupplyLiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            var liftCoords = _transform.GetMapCoordinates(uid, xform);
            if (liftCoords.MapId != consoleCoords.MapId)
                continue;

            var distance = (liftCoords.Position - consoleCoords.Position).LengthSquared();
            if (distance > console.LiftSearchRange * console.LiftSearchRange)
                continue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            lift = (uid, comp);
            found = true;
        }

        return found;
    }


    private List<VehicleSupplyEntry> BuildVendorCatalog(EntityUid vendorUid, VehicleHardpointVendorComponent vendor)
    {
        var vendorCoords = _transform.GetMapCoordinates(vendorUid);
        var maxDistance = vendor.ConsoleSearchRange * vendor.ConsoleSearchRange;
        var list = new List<VehicleSupplyEntry>();
        var seen = new HashSet<string>();

        void Collect(bool useRange)
        {
            var query = EntityQueryEnumerator<VehicleSupplyConsoleComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var console, out var xform))
            {
                var consoleCoords = _transform.GetMapCoordinates(uid, xform);
                if (consoleCoords.MapId != vendorCoords.MapId)
                    continue;

                if (useRange)
                {
                    var distance = (consoleCoords.Position - vendorCoords.Position).LengthSquared();
                    if (distance > maxDistance)
                        continue;
                }

                foreach (var entry in console.Vehicles)
                {
                    var key = Normalize(entry.Vehicle.Id);
                    if (seen.Add(key))
                        list.Add(entry);
                }
            }
        }

        Collect(true);
        if (list.Count == 0)
            Collect(false);

        return list;
    }

    private bool TryGetEntry(VehicleSupplyConsoleComponent console, string vehicleId, out VehicleSupplyEntry entry)
    {
        var key = Normalize(vehicleId);
        foreach (var candidate in console.Vehicles)
        {
            if (Normalize(candidate.Vehicle.Id) == key)
            {
                entry = candidate;
                return true;
            }
        }

        entry = default!;
        return false;
    }

    private string GetEntryName(VehicleSupplyEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
            return entry.Name;

        return GetPrototypeName(entry.Vehicle.Id);
    }

    private string GetPrototypeName(string protoId)
    {
        if (_prototypes.TryIndex<EntityPrototype>(protoId, out var proto))
            return proto.Name;

        return protoId;
    }

    private Entity<VehicleSupplyTechComponent> EnsureSupplyTech()
    {
        var query = EntityQueryEnumerator<VehicleSupplyTechComponent>();
        if (query.MoveNext(out var uid, out var comp))
            return (uid, comp);

        var tree = _intel.EnsureTechTree();
        var tech = EnsureComp<VehicleSupplyTechComponent>(tree.Owner);
        return (tree.Owner, tech);
    }

    private List<VehicleHardpointLayerState> BuildPreviewLayers(
        EntityUid vehicle,
        HardpointSlotsComponent? hardpoints = null,
        ItemSlotsComponent? itemSlots = null)
    {
        if (!Resolve(vehicle, ref hardpoints, ref itemSlots, logMissing: false))
            return new List<VehicleHardpointLayerState>();

        var layers = new List<VehicleHardpointLayerState>(hardpoints.Slots.Count);
        var indexByLayer = new Dictionary<string, int>();

        foreach (var slot in hardpoints.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            var layer = slot.VisualLayer;
            if (string.IsNullOrWhiteSpace(layer))
                continue;

            var state = string.Empty;
            var usesOverlay = false;
            if (_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) && itemSlot.HasItem)
            {
                var item = itemSlot.Item!.Value;
                state = ResolveVisualState(item, out usesOverlay);
            }

            var key = layer.ToLowerInvariant();
            if (indexByLayer.TryGetValue(key, out var existingIndex))
            {
                if (!string.IsNullOrWhiteSpace(state))
                    layers[existingIndex] = new VehicleHardpointLayerState(layer, state);
                continue;
            }

            indexByLayer[key] = layers.Count;
            if (usesOverlay)
                state = string.Empty;
            layers.Add(new VehicleHardpointLayerState(layer, state));
        }

        return layers;
    }

    private List<VehicleSupplyPreviewOverlay> BuildPreviewOverlays(
        EntityUid vehicle,
        HardpointSlotsComponent? hardpoints = null,
        ItemSlotsComponent? itemSlots = null)
    {
        if (!Resolve(vehicle, ref hardpoints, ref itemSlots, logMissing: false))
            return new List<VehicleSupplyPreviewOverlay>();

        var overlays = new List<VehicleSupplyPreviewOverlay>();
        var turretOffsets = new Dictionary<string, PreviewOffset>();

        foreach (var slot in hardpoints.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) || !itemSlot.HasItem)
                continue;

            var item = itemSlot.Item!.Value;
            if (TryGetTurretOverlay(item, 0, out var overlay, out var offset))
            {
                overlays.Add(overlay);
                turretOffsets[slot.Id] = offset;
            }

            if (!TryComp(item, out HardpointSlotsComponent? attachedSlots) ||
                !TryComp(item, out ItemSlotsComponent? attachedItemSlots))
            {
                continue;
            }

            foreach (var turretSlot in attachedSlots.Slots)
            {
                if (string.IsNullOrWhiteSpace(turretSlot.Id))
                    continue;

                if (!_itemSlots.TryGetSlot(item, turretSlot.Id, out var turretItemSlot, attachedItemSlots) ||
                    !turretItemSlot.HasItem)
                {
                    continue;
                }

                var child = turretItemSlot.Item!.Value;
                if (!TryGetTurretOverlay(child, 1, out var childOverlay, out var childOffset))
                    continue;

                if (turretOffsets.TryGetValue(slot.Id, out var parentOffset))
                {
                    var combined = CombineOffsets(parentOffset, childOffset);
                    childOverlay = new VehicleSupplyPreviewOverlay(
                        childOverlay.Rsi,
                        childOverlay.State,
                        childOverlay.Order,
                        combined.Base,
                        combined.UseDirectional,
                        combined.North,
                        combined.East,
                        combined.South,
                        combined.West);
                }

                overlays.Add(childOverlay);
            }
        }

        return overlays;
    }

    private bool TryGetTurretOverlay(
        EntityUid item,
        int order,
        out VehicleSupplyPreviewOverlay overlay,
        out PreviewOffset offset)
    {
        overlay = default!;
        offset = default;

        if (!TryComp(item, out VehicleTurretComponent? turret))
            return false;

        if (!turret.ShowOverlay || string.IsNullOrWhiteSpace(turret.OverlayState) || string.IsNullOrWhiteSpace(turret.OverlayRsi))
            return false;

        offset = new PreviewOffset(
            turret.PixelOffset,
            turret.UseDirectionalOffsets,
            turret.PixelOffsetNorth,
            turret.PixelOffsetEast,
            turret.PixelOffsetSouth,
            turret.PixelOffsetWest);

        overlay = new VehicleSupplyPreviewOverlay(
            turret.OverlayRsi,
            turret.OverlayState,
            order,
            offset.Base,
            offset.UseDirectional,
            offset.North,
            offset.East,
            offset.South,
            offset.West);
        return true;
    }

    private static PreviewOffset CombineOffsets(PreviewOffset a, PreviewOffset b)
    {
        var useDirectional = a.UseDirectional || b.UseDirectional;
        var north = (a.UseDirectional ? a.North : Vector2.Zero) + (b.UseDirectional ? b.North : Vector2.Zero);
        var east = (a.UseDirectional ? a.East : Vector2.Zero) + (b.UseDirectional ? b.East : Vector2.Zero);
        var south = (a.UseDirectional ? a.South : Vector2.Zero) + (b.UseDirectional ? b.South : Vector2.Zero);
        var west = (a.UseDirectional ? a.West : Vector2.Zero) + (b.UseDirectional ? b.West : Vector2.Zero);
        return new PreviewOffset(a.Base + b.Base, useDirectional, north, east, south, west);
    }

    private string ResolveVisualState(EntityUid item, out bool usesOverlay, int depth = 0)
    {
        usesOverlay = false;
        if (depth > 2)
            return string.Empty;

        if (TryComp(item, out VehicleTurretComponent? turretOverlay) && turretOverlay.ShowOverlay)
            usesOverlay = true;

        if (TryComp(item, out HardpointSlotsComponent? attachedSlots) &&
            TryComp(item, out ItemSlotsComponent? attachedItemSlots))
        {
            foreach (var slot in attachedSlots.Slots)
            {
                if (string.IsNullOrWhiteSpace(slot.Id))
                    continue;

                if (!_itemSlots.TryGetSlot(item, slot.Id, out var itemSlot, attachedItemSlots) || !itemSlot.HasItem)
                    continue;

                var child = itemSlot.Item!.Value;
                var childState = ResolveVisualState(child, out var childOverlay, depth + 1);
                usesOverlay |= childOverlay;
                if (!string.IsNullOrWhiteSpace(childState))
                    return childState;
            }
        }

        if (TryComp(item, out HardpointVisualComponent? visual) &&
            !string.IsNullOrWhiteSpace(visual.VehicleState))
        {
            return visual.VehicleState;
        }

        if (TryComp(item, out VehicleTurretComponent? turret) &&
            !string.IsNullOrWhiteSpace(turret.OverlayState))
        {
            return turret.OverlayState;
        }

        return string.Empty;
    }

    private HashSet<string> BuildUnlockedSet()
    {
        var unlocked = new HashSet<string>();
        if (!TryGetSupplyTech(out var tech))
            return unlocked;

        foreach (var id in tech.Comp.Unlocked)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            unlocked.Add(Normalize(id));
        }

        return unlocked;
    }

    private bool TryGetSupplyTech(out Entity<VehicleSupplyTechComponent> tech)
    {
        var query = EntityQueryEnumerator<VehicleSupplyTechComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            tech = (uid, comp);
            return true;
        }

        tech = default;
        return false;
    }

    private static bool IsEntryUnlocked(VehicleSupplyEntry entry, HashSet<string> unlocked)
    {
        var key = Normalize(entry.Vehicle.Id);
        if (key.Contains("humvee"))
        {
            var unlockKey = !string.IsNullOrWhiteSpace(entry.Unlock) ? Normalize(entry.Unlock) : key;
            return unlocked.Contains(unlockKey);
        }

        if (string.IsNullOrWhiteSpace(entry.Unlock))
            return true;

        return unlocked.Contains(Normalize(entry.Unlock));
    }

    private IReadOnlyList<string> GetHardpointsForVehicle(string vehicleId, IReadOnlyList<VehicleSupplyEntry> entries)
    {
        var key = Normalize(vehicleId);
        if (_hardpointsByVehicleCache.TryGetValue(key, out var cached))
            return cached;

        var explicitList = GetExplicitHardpoints(vehicleId, entries);
        if (explicitList != null)
        {
            _hardpointsByVehicleCache[key] = explicitList;
            return explicitList;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(vehicleId, out var vehicleProto))
        {
            _hardpointsByVehicleCache[key] = new List<string>();
            return _hardpointsByVehicleCache[key];
        }

        if (!vehicleProto.TryGetComponent(out HardpointSlotsComponent? slots, _compFactory))
        {
            _hardpointsByVehicleCache[key] = new List<string>();
            return _hardpointsByVehicleCache[key];
        }

        var result = new List<string>();
        var seen = new HashSet<string>();

        foreach (var slot in slots.Slots)
        {
            var typeKey = Normalize(slot.HardpointType.Id);
            if (!_hardpointItemsByType.TryGetValue(typeKey, out var candidates))
                continue;

            var whitelistTags = slot.Whitelist?.Tags;

            foreach (var candidate in candidates)
            {
                if (whitelistTags != null && whitelistTags.Count > 0)
                {
                    var allowed = false;
                    foreach (var tag in whitelistTags)
                    {
                        if (candidate.Tags.Contains(tag))
                        {
                            allowed = true;
                            break;
                        }
                    }

                    if (!allowed)
                        continue;
                }

                if (seen.Add(candidate.ProtoId))
                    result.Add(candidate.ProtoId);
            }
        }

        _hardpointsByVehicleCache[key] = result;
        return result;
    }

    private static List<string>? GetExplicitHardpoints(string vehicleId, IReadOnlyList<VehicleSupplyEntry> entries)
    {
        var key = Normalize(vehicleId);
        foreach (var entry in entries)
        {
            if (Normalize(entry.Vehicle.Id) != key)
                continue;

            if (entry.Hardpoints.Count == 0)
                return null;

            var list = new List<string>(entry.Hardpoints.Count);
            foreach (var hardpoint in entry.Hardpoints)
            {
                if (!string.IsNullOrWhiteSpace(hardpoint.Id))
                    list.Add(hardpoint.Id);
            }

            return list;
        }

        return null;
    }
}
