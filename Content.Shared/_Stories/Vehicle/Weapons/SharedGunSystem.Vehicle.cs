using Content.Shared._Stories.Vehicle;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Content.Shared._Stories.Vehicle;

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

        if (TryComp<BallisticVehicleAmmoProviderComponent>(gun, out var projVehicle) && projVehicle.Shots <= 0)
        {
            args.Cancel();
            return;
        }

        if (args.Used.Comp.Target == args.User)
            args.Cancel();
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

        if (_netManager.IsServer)
            Dirty(provider, provider.Comp);
    }

    private (EntityUid? Entity, IShootable) GetShootable(BallisticVehicleAmmoProviderComponent component, EntityCoordinates coordinates)
    {
        var ent = Spawn(component.Prototype, coordinates);
        return (ent, EnsureShootable(ent));
    }
}
