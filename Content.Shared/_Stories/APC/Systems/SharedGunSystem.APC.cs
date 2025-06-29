using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Content.Shared._Stories.APC;
using Content.Shared.Weapons.Ranged;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected virtual void InitializeAPCGun()
    {
        base.Initialize();
        SubscribeLocalEvent<APCGunComponent, ShotAttemptedEvent>(OnShotAttempt);

        SubscribeLocalEvent<BallisticAPCAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<BallisticAPCAmmoProviderComponent, GetAmmoCountEvent>(OnAPCAmmoCount);
    }

    private void OnShotAttempt(Entity<APCGunComponent> gun, ref ShotAttemptedEvent args)
    {
        if (!TryComp<APCEntityComponent>(args.User, out var apc))
        {
            args.Cancel();
            return;
        }

        if (TryComp<BallisticAPCAmmoProviderComponent>(gun, out var projAPC) && projAPC.Shots <= 0)
        {
            args.Cancel();
            return;
        }

        if (args.Used.Comp.Target == args.User)
            args.Cancel();
    }

    private void OnAPCAmmoCount(Entity<BallisticAPCAmmoProviderComponent> provider, ref GetAmmoCountEvent args)
    {
        args.Count = provider.Comp.Shots;
        args.Capacity = provider.Comp.Capacity;
    }

    private void OnTakeAmmo(Entity<BallisticAPCAmmoProviderComponent> provider, ref TakeAmmoEvent args)
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

    private (EntityUid? Entity, IShootable) GetShootable(BallisticAPCAmmoProviderComponent component, EntityCoordinates coordinates)
    {
        var ent = Spawn(component.Prototype, coordinates);
        return (ent, EnsureShootable(ent));
    }
}