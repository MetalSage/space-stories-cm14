using Content.Shared._Stories.Vehicle;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Content.Shared._Stories.Vehicle;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Actions;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Popups;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected virtual void InitializeVehicleGun()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleGunComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<VehicleComponent, GunMuzzleFlashAttemptEvent>(OnVehicleMuzzleFlashAttempt);
        SubscribeLocalEvent<VehicleGunComponent, GunMuzzleFlashAttemptEvent>(OnVehicleGunMuzzleFlashAttempt);

        SubscribeLocalEvent<BallisticVehicleAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<BallisticVehicleAmmoProviderComponent, GetAmmoCountEvent>(OnVehicleAmmoCount);
        SubscribeLocalEvent<BallisticVehicleAmmoProviderComponent, ComponentInit>(OnAmmoProviderInit);
    }

    private void OnAmmoProviderInit(Entity<BallisticVehicleAmmoProviderComponent> provider, ref ComponentInit args)
    {
        if (provider.Comp.AutoReload && provider.Comp.InitialShots == 0)
        {
            provider.Comp.InitialShots = provider.Comp.Shots;
        }
    }

    private void OnVehicleReloadAction(VehicleGunReloadActionEvent args)
    {
        if (!TryComp<BallisticVehicleAmmoProviderComponent>(args.Target, out var ammoProvider))
            return;

        StartReload((args.Target!.Value, ammoProvider));
        args.Handled = true;
    }

    private void OnVehicleMuzzleFlashAttempt(Entity<VehicleComponent> apc, ref GunMuzzleFlashAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnVehicleGunMuzzleFlashAttempt(Entity<VehicleGunComponent> apc, ref GunMuzzleFlashAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnShotAttempt(Entity<VehicleGunComponent> gun, ref ShotAttemptedEvent args)
    {
        if (!TryComp<VehicleComponent>(args.User, out var apc))
        {
            args.Cancel();
            return;
        }

        if (gun.Comp.User is null)
        {
            args.Cancel();
            return;
        }

        if (TryComp<BallisticVehicleAmmoProviderComponent>(gun, out var projVehicle))
        {
            if (projVehicle.IsReloading)
            {
                PopupSystem.PopupClient("Перезарядка...", gun.Comp.User.Value, gun.Owner, PopupType.Small);
                args.Cancel();
                return;
            }

            if (projVehicle.Shots <= 0)
            {
                if (projVehicle.AutoReload)
                {
                    StartReload((gun, projVehicle));
                    PopupSystem.PopupClient("Начинается автоматическая перезарядка...", gun.Comp.User.Value, gun.Owner, PopupType.Small);
                }
                args.Cancel();
                return;
            }
        }

        if (args.Used.Comp.Target == args.User)
            args.Cancel();

        if (gun.Comp.NeedHands && Hands.CountFreeHands(gun.Comp.User.Value) < 2)
        {
            PopupSystem.PopupClient("need-free-hands", gun.Comp.User.Value, gun.Owner, PopupType.Small);
            args.Cancel();
        }

        if (gun.Comp.DisableAtHullDamage != -1f &&
            TryComp<DamageableComponent>(args.User, out var damageable) &&
            TryComp<VehicleComponent>(args.User, out var vehicle))
        {
            var hullIntegrityPercent = (float)(vehicle.MaxHealth - damageable.TotalDamage) / vehicle.MaxHealth;
            if (hullIntegrityPercent < gun.Comp.DisableAtHullDamage)
            {
                PopupSystem.PopupClient("Корпус слишком поврежден", gun.Comp.User.Value, gun.Owner, PopupType.Small);
                args.Cancel();
            }
        }
    }

    private void OnVehicleAmmoCount(Entity<BallisticVehicleAmmoProviderComponent> provider, ref GetAmmoCountEvent args)
    {
        args.Count = provider.Comp.Shots;
        args.Capacity = provider.Comp.Capacity;
    }

    private void OnTakeAmmo(Entity<BallisticVehicleAmmoProviderComponent> provider, ref TakeAmmoEvent args)
    {
        var shots = Math.Min(args.Shots, provider.Comp.Shots);

        // Dont dirty if it an empty fire
        if (shots == 0)
            return;

        for (var i = 0; i < shots; i++)
        {
            args.Ammo.Add(GetShootable(provider.Comp, args.Coordinates));
            provider.Comp.Shots--;
        }

        if (provider.Comp.AutoReload && provider.Comp.Shots <= 0 && !provider.Comp.IsReloading)
            StartReload(provider);

        if (_netManager.IsServer)
            Dirty(provider, provider.Comp);
    }

    private void StartReload(Entity<BallisticVehicleAmmoProviderComponent> ent)
    {
        var comp = ent.Comp;

        if (comp.IsReloading || !comp.AutoReload)
            return;

        comp.IsReloading = true;
        comp.ReloadEndTime = Timing.CurTime + comp.ReloadCooldown;

        if (_netManager.IsServer)
        {
            Dirty(ent);
        }
    }

    private (EntityUid? Entity, IShootable) GetShootable(BallisticVehicleAmmoProviderComponent component, EntityCoordinates coordinates)
    {
        var ent = Spawn(component.Prototype, coordinates);
        return (ent, EnsureShootable(ent));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        if (_netManager.IsClient)
            return;

        var currentTime = Timing.CurTime;

        var query = EntityQueryEnumerator<BallisticVehicleAmmoProviderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.AutoReload || !comp.IsReloading || comp.ReloadEndTime == null)
                continue;

            if (currentTime >= comp.ReloadEndTime)
            {
                comp.IsReloading = false;
                comp.Shots = comp.InitialShots;
                comp.ReloadEndTime = null;

                Dirty(uid, comp);

                if (TryComp<VehicleGunComponent>(uid, out var gun) && gun.User != null)
                    PopupSystem.PopupClient("Перезарядка завершена!", gun.User.Value, uid, PopupType.Small);
            }
        }
    }
}
