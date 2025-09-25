using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Vehicle.Systems;
using Content.Shared._Stories.Attachables;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Server.Light.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Light.Components;

namespace Content.Server._Stories.Vehicle;

public sealed class VehicleSystem : EntitySystem
{
    [Dependency] private readonly VehicleAttachableHolderSystem _attachable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExpendableLightSystem _expendableLight = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BallisticVehicleAmmoProviderComponent, VehicleGunReloadEvent>(OnReload);
        SubscribeLocalEvent<ActivateExpendableLightOnShootComponent, AmmoShotEvent>(ActivateExpendableLightOnShot);
    }

    private void OnReload(Entity<BallisticVehicleAmmoProviderComponent> provider, ref VehicleGunReloadEvent args)
    {
        if (!_attachable.TryGetHolder(provider.Owner, out var holder) ||
            holder is not { } apc)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        if (provider.Comp.Shots >= provider.Comp.Capacity)
            return;

        var magazine = TryMagazine(apc, provider.Comp);
        if (magazine == null)
            return;

        provider.Comp.Shots = provider.Comp.Capacity;

        QueueDel(magazine);

        Dirty(provider, provider.Comp);
    }

    private EntityUid? TryMagazine(EntityUid apc, BallisticVehicleAmmoProviderComponent comp)
    {
        _container.TryGetContainer(apc, comp.AmmoContainerId, out var apcContainer);

        if (apcContainer == null)
            return null;

        foreach (var magazine in apcContainer.ContainedEntities)
        {
            if (!TryComp<VehicleGunMagazineComponent>(magazine, out var magazineComp))
                continue;

            if (comp.Prototype != magazineComp.Prototype)
                continue;

            QueueDel(magazine);
            return magazine;
        }
        return null;
    }

    private void ActivateExpendableLightOnShot(Entity<ActivateExpendableLightOnShootComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<ExpendableLightComponent>(projectile, out var light))
            _expendableLight.TryActivate((projectile, light));
        }
    }
}
