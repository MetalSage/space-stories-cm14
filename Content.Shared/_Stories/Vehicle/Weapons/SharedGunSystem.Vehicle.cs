using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Stun;
using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Vehicle.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly RMCCameraShakeSystem _rmcCamera = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedVehicleWeaponLoaderSystem _weaponLoader = default!;
    [Dependency] private readonly SharedVehicleSystem _vehicle = default!;

    protected virtual void InitializeVehicleGun()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleGunComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<VehicleComponent, GunMuzzleFlashAttemptEvent>(OnMuzzleFlashAttempt);
        SubscribeLocalEvent<VehicleGunComponent, GunMuzzleFlashAttemptEvent>(OnMuzzleFlashAttempt);
        SubscribeLocalEvent<VehicleGunComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<VehicleGunComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<VehicleGunComponent, ComponentInit>(OnGunInit);

        SubscribeLocalEvent<VehiclePilotComponent, VehicleReloadSpecialGunEvent>(OnManualReload);

        SubscribeLocalEvent<ShakeOnHitComponent, ProjectileHitEvent>(OnShakeProjectileHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_netManager.IsClient)
            return;

        var query = EntityQueryEnumerator<VehicleAutoReloadGunComponent, VehicleGunComponent>();
        while (query.MoveNext(out var uid, out var autoReload, out var gun))
        {
            if (!autoReload.IsReloading || autoReload.ReloadEndTime == null)
                continue;

            if (Timing.CurTime >= autoReload.ReloadEndTime.Value)
            {
                CompleteReload((uid, autoReload, gun));
            }
        }
    }

    private void OnGunInit(Entity<VehicleGunComponent> gun, ref ComponentInit args)
    {
        gun.Comp.ActiveMagazineContainer = Containers.EnsureContainer<ContainerSlot>(
            gun, gun.Comp.ActiveMagazineContainerId);

        gun.Comp.SpareMagazinesContainer = Containers.EnsureContainer<Container>(
            gun, gun.Comp.SpareMagazinesContainerId);

        gun.Comp.ActiveMagazineContainer.OccludesLight = false;
        gun.Comp.SpareMagazinesContainer.OccludesLight = false;

        if (_netManager.IsClient)
            return;

        if (!string.IsNullOrEmpty(gun.Comp.StartingMagazinePrototype))
        {
            var magazine = Spawn(gun.Comp.StartingMagazinePrototype, Transform(gun).Coordinates);
            Containers.Insert(magazine, gun.Comp.ActiveMagazineContainer);
        }
    }

    private void OnMuzzleFlashAttempt<T>(Entity<T> _, ref GunMuzzleFlashAttemptEvent args) where T : Component
    {
        args.Cancelled = true;
    }

    private void OnShotAttempt(Entity<VehicleGunComponent> gun, ref ShotAttemptedEvent args)
    {
        EntityUid? actualPilot = null;
        EntityUid? actualVehicle = null;

        if (TryComp<VehicleAttachableComponent>(gun, out var attachable) && attachable.Destroyed)
        {
            args.Cancel();
            return;
        }

        if (TryComp<VehicleComponent>(args.User, out var vehicleFromUser))
        {
            actualVehicle = args.User;
            var query = EntityQueryEnumerator<VehiclePilotComponent>();
            while (query.MoveNext(out var pilotUid, out var pilotComp))
            {
                if (pilotComp.Vehicle == actualVehicle)
                {
                    actualPilot = pilotUid;
                    break;
                }
            }
        }
        else if (TryComp<VehiclePilotComponent>(args.User, out var pilot) && pilot.Vehicle != null)
        {
            actualPilot = args.User;
            actualVehicle = pilot.Vehicle;
        }

        if (actualVehicle == null || !TryComp<VehicleComponent>(actualVehicle.Value, out var vehicle))
        {
            args.Cancel();
            return;
        }

        var userForPopup = actualPilot ?? gun.Comp.User ?? args.User;

        if (gun.Comp.User is null)
        {
            args.Cancel();
            return;
        }

        if (TryComp<VehicleAutoReloadGunComponent>(gun, out var autoReload))
        {
            if (autoReload.IsReloading)
            {
                if (Timing.IsFirstTimePredicted)
                    PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-reloading"), userForPopup, PopupType.Medium);
                args.Cancel();
                return;
            }
        }

        if (gun.Comp.ActiveMagazineContainer.ContainedEntity == null)
        {
            if (Timing.IsFirstTimePredicted)
                PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-no-magazine"), userForPopup, PopupType.Medium);
            args.Cancel();
            return;
        }

        if (!TryComp<VehicleGunMagazineComponent>(gun.Comp.ActiveMagazineContainer.ContainedEntity.Value, out var magazine))
        {
            args.Cancel();
            return;
        }

        if (magazine.Shots <= 0)
        {
            if (autoReload != null && _netManager.IsServer)
            {
                StartReload((gun, autoReload, gun.Comp), actualPilot);
            }
            else if (autoReload == null && Timing.IsFirstTimePredicted)
            {
                PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-magazine-empty"), userForPopup, PopupType.Medium);
            }
            args.Cancel();
            return;
        }

        if (args.Used.Comp.Target != null)
        {
            var target = args.Used.Comp.Target.Value;
            if (target == actualVehicle || target == actualPilot)
            {
                args.Cancel();
                return;
            }
        }

        if (gun.Comp.NeedHands && actualPilot != null && Hands.CountFreeHands(actualPilot.Value) < 2)
        {
            if (Timing.IsFirstTimePredicted)
                PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-need-hands"), userForPopup, PopupType.Medium);
            args.Cancel();
            return;
        }

        if (gun.Comp.DisableAtHullDamage > 0f && actualVehicle != null)
        {
            if (TryComp<DamageableComponent>(actualVehicle.Value, out var damageable) && vehicle.MaxHealth > 0)
            {
                var currentHealth = vehicle.MaxHealth - damageable.TotalDamage;
                var hullIntegrityPercent = (float)currentHealth / vehicle.MaxHealth;

                if (hullIntegrityPercent < gun.Comp.DisableAtHullDamage)
                {
                    if (Timing.IsFirstTimePredicted)
                        PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-hull-low"), userForPopup, PopupType.Large);
                    args.Cancel();
                    return;
                }
            }
        }
    }

    private void OnGetAmmoCount(Entity<VehicleGunComponent> gun, ref GetAmmoCountEvent args)
    {
        if (gun.Comp.ActiveMagazineContainer.ContainedEntity == null)
        {
            args.Count = 0;
            args.Capacity = 0;
            return;
        }

        if (!TryComp<VehicleGunMagazineComponent>(gun.Comp.ActiveMagazineContainer.ContainedEntity.Value, out var magazine))
        {
            args.Count = 0;
            args.Capacity = 0;
            return;
        }

        args.Count = magazine.Shots;
        args.Capacity = magazine.Capacity;
    }

    private void OnTakeAmmo(Entity<VehicleGunComponent> gun, ref TakeAmmoEvent args)
    {
        if (gun.Comp.ActiveMagazineContainer.ContainedEntity == null)
            return;

        if (!TryComp<VehicleGunMagazineComponent>(gun.Comp.ActiveMagazineContainer.ContainedEntity.Value, out var magazine))
            return;

        var shots = Math.Min(args.Shots, magazine.Shots);
        if (shots == 0)
            return;

        magazine.Shots -= shots;
        Dirty(gun.Comp.ActiveMagazineContainer.ContainedEntity.Value, magazine);
        UpdateVehicleStatusUIForGun(gun.Owner);

        for (var i = 0; i < shots; i++)
        {
            var projectile = Spawn(magazine.ProjectilePrototype, args.Coordinates);
            args.Ammo.Add((projectile, EnsureShootable(projectile)));
        }

        if (magazine.Shots <= 0 && TryComp<VehicleAutoReloadGunComponent>(gun, out var autoReload))
        {
            StartReload((gun, autoReload, gun.Comp), gun.Comp.User);
        }
    }

    private void StartReload(Entity<VehicleAutoReloadGunComponent, VehicleGunComponent> gun, EntityUid? pilot)
    {
        if (_netManager.IsClient)
            return;

        if (gun.Comp1.IsReloading)
            return;

        gun.Comp1.IsReloading = true;
        gun.Comp1.ReloadEndTime = Timing.CurTime + TimeSpan.FromSeconds(gun.Comp1.ReloadTime);

        Dirty(gun, gun.Comp1);

        if (pilot != null)
            PopupSystem.PopupCursor(Loc.GetString("st-vehicle-gun-reloading-time",
                ("time", gun.Comp1.ReloadTime.ToString("F1"))), pilot.Value, PopupType.Medium);
    }

    private void CompleteReload(Entity<VehicleAutoReloadGunComponent, VehicleGunComponent> gun)
    {
        if (_netManager.IsClient)
            return;

        gun.Comp1.IsReloading = false;
        gun.Comp1.ReloadEndTime = null;

        if (gun.Comp2.ActiveMagazineContainer.ContainedEntity != null &&
            TryComp<VehicleGunMagazineComponent>(gun.Comp2.ActiveMagazineContainer.ContainedEntity.Value, out var magazine))
        {
            magazine.Shots = magazine.Capacity;

            Dirty(gun.Comp2.ActiveMagazineContainer.ContainedEntity.Value, magazine);
            Dirty(gun, gun.Comp1);

            if (gun.Comp2.User != null)
                PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-reload-complete"), gun.Comp2.User.Value, PopupType.Medium);

            UpdateVehicleStatusUIForGun(gun.Owner);
        }
    }

    private void OnManualReload(Entity<VehiclePilotComponent> ent, ref VehicleReloadSpecialGunEvent args)
    {
        if (ent.Comp.Gun == null)
            return;

        var gun = ent.Comp.Gun.Value;

        if (!TryComp<VehicleGunComponent>(gun, out var gunComp) ||
            !TryComp<VehicleAutoReloadGunComponent>(gun, out var autoReloadComp))
            return;

        if (autoReloadComp.IsReloading)
        {
            PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-already-reloading"), ent, PopupType.Medium);
            return;
        }

        if (gunComp.ActiveMagazineContainer.ContainedEntity == null)
            return;

        if (!TryComp<VehicleGunMagazineComponent>(gunComp.ActiveMagazineContainer.ContainedEntity.Value, out var magazine))
            return;

        if (magazine.Shots >= magazine.Capacity)
        {
            PopupSystem.PopupPredictedCursor(Loc.GetString("st-vehicle-gun-magazine-full"), ent, PopupType.Medium);
            return;
        }

        StartReload((gun, autoReloadComp, gunComp), ent.Owner);
        args.Handled = true;
    }

    private void OnShakeProjectileHit(Entity<ShakeOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        var coords = Transform(args.Target).Coordinates;
        var entities = new HashSet<EntityUid>();
        _entityLookup.GetEntitiesInRange(coords, ent.Comp.ShakeRange, entities);

        foreach (var entity in entities)
        {
            if (!HasComp<RMCSizeComponent>(entity))
                continue;

            if (_mobState.IsDead(entity))
                continue;

            if (!TryComp<RMCSizeComponent>(entity, out var sizeComp))
                continue;

            if (sizeComp.Size > ent.Comp.Size)
                continue;

            if (!Examine.InRangeUnOccluded(args.Target, entity))
                continue;

            _rmcCamera.ShakeCamera(entity, ent.Comp.Shakes, ent.Comp.Strength);
        }
    }

    private void UpdateVehicleStatusUIForGun(EntityUid gunUid)
    {
        if (!TryComp<TransformComponent>(gunUid, out var xform) ||
            !xform.ParentUid.Valid)
            return;

        if (!TryComp<VehicleComponent>(xform.ParentUid, out var vehicle))
            return;

        if (vehicle.Hardpoints.Contains(gunUid))
        {
            _vehicle.UpdateVehicleStatusUI((xform.ParentUid, vehicle));

            UpdateWeaponLoadersOnVehicle((xform.ParentUid, vehicle));
        }
    }

    private void UpdateWeaponLoadersOnVehicle(Entity<VehicleComponent> vehicle)
    {
        if (vehicle.Comp.GridEnt == null)
            return;
        
        var loaderQuery = EntityQueryEnumerator<VehicleWeaponLoaderComponent, TransformComponent>();
        while (loaderQuery.MoveNext(out var loaderUid, out var loader, out var loaderXform))
        {
            if (loaderXform.GridUid != vehicle.Comp.GridEnt.Value)
                continue;
                
            _weaponLoader.UpdateLoaderUI((loaderUid, loader));
        }
    }
}
