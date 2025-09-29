using Content.Shared._Stories.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected virtual void InitializeVehicleGun()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleGunComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<VehicleComponent, GunMuzzleFlashAttemptEvent>(OnVehicleMuzzleFlashAttempt);
        SubscribeLocalEvent<VehicleGunComponent, GunMuzzleFlashAttemptEvent>(OnVehicleGunMuzzleFlashAttempt);
        SubscribeLocalEvent<VehicleGunComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<VehicleGunComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<VehicleGunComponent, ComponentInit>(OnGunInit);
    }

    private void OnGunInit(Entity<VehicleGunComponent> gun, ref ComponentInit args)
    {
        gun.Comp.ActiveMagazineContainer = Containers.EnsureContainer<ContainerSlot>(
            gun, gun.Comp.ActiveMagazineContainerId);
        gun.Comp.ActiveMagazineContainer.OccludesLight = false;

        gun.Comp.SpareMagazinesContainer = Containers.EnsureContainer<Container>(
            gun, gun.Comp.SpareMagazinesContainerId);
        gun.Comp.SpareMagazinesContainer.OccludesLight = false;
    }

    private void OnVehicleMuzzleFlashAttempt(Entity<VehicleComponent> vehicle, ref GunMuzzleFlashAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnVehicleGunMuzzleFlashAttempt(Entity<VehicleGunComponent> gun, ref GunMuzzleFlashAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnShotAttempt(Entity<VehicleGunComponent> gun, ref ShotAttemptedEvent args)
    {
        if (!TryComp<VehicleComponent>(args.User, out var vehicle))
        {
            args.Cancel();
            return;
        }

        if (gun.Comp.User is null)
        {
            args.Cancel();
            return;
        }

        if (gun.Comp.ActiveMagazineContainer.ContainedEntity == null)
        {
            PopupSystem.PopupCursor("No magazine loaded!", gun.Comp.User.Value, PopupType.Small);
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
            PopupSystem.PopupCursor("Magazine empty!", gun.Comp.User.Value, PopupType.Small);
            args.Cancel();
            return;
        }

        if (args.Used.Comp.Target == args.User)
            args.Cancel();

        if (gun.Comp.NeedHands && Hands.CountFreeHands(gun.Comp.User.Value) < 2)
        {
            PopupSystem.PopupCursor("need-free-hands", gun.Comp.User.Value, PopupType.Small);
            args.Cancel();
        }

        if (gun.Comp.DisableAtHullDamage != -1f &&
            TryComp<DamageableComponent>(args.User, out var damageable))
        {
            var hullIntegrityPercent = (float)(vehicle.MaxHealth - damageable.TotalDamage) / vehicle.MaxHealth;
            if (hullIntegrityPercent < gun.Comp.DisableAtHullDamage)
            {
                PopupSystem.PopupCursor("Hull too damaged!", gun.Comp.User.Value, PopupType.Small);
                args.Cancel();
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

        for (var i = 0; i < shots; i++)
        {
            var projectile = Spawn(magazine.ProjectilePrototype, args.Coordinates);
            args.Ammo.Add((projectile, EnsureShootable(projectile)));
            magazine.Shots--;
        }

        // If magazine is empty, remove it
        if (magazine.Shots <= 0 && _netManager.IsServer)
        {
            var magEntity = gun.Comp.ActiveMagazineContainer.ContainedEntity.Value;
            Containers.Remove(magEntity, gun.Comp.ActiveMagazineContainer);
            QueueDel(magEntity);
        }

        if (_netManager.IsServer)
            Dirty(gun.Comp.ActiveMagazineContainer.ContainedEntity.Value, magazine);
    }
}
