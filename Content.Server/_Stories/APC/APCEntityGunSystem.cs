using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared._Stories.APC;
using Content.Shared._Stories.Attachables;

namespace Content.Server._Stories.APC;

public sealed class APCEntityGunSystem : EntitySystem
{
    [Dependency] private readonly APCAttachableHolderSystem _attachable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BallisticAPCAmmoProviderComponent, APCGunReloadEvent>(OnReload);
    }

    private void OnReload(Entity<BallisticAPCAmmoProviderComponent> provider, ref APCGunReloadEvent args)
    {
        if (!TryComp<APCAttachableComponent>(provider, out var attachable) || 
            !_attachable.TryGetHolder(provider.Owner, out var holder) ||
            holder is not {} apc)
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

    private EntityUid? TryMagazine(EntityUid apc, BallisticAPCAmmoProviderComponent comp)
    {
        _container.TryGetContainer(apc, comp.AmmoContainerId, out var apcContainer);

        if (apcContainer == null)
            return null;

        foreach (var magazine in apcContainer.ContainedEntities)
        {
            if (!TryComp<APCGunMagazineComponent>(magazine, out var magazineComp))
                continue;

            if (comp.AmmoType != magazineComp.MagazineType)
                continue;

            QueueDel(magazine);
            return magazine;
        }
        return null;
    }
}

// todo сделать норм магазины, а не "затычки"