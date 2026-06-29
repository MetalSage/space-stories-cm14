using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vendors;

[ByRefEvent]
public struct BeforeItemsVendedEvent
{
    public readonly EntityUid User;
    public readonly EntProtoId VendedPrototype;
    public List<EntProtoId> Items;

    public BeforeItemsVendedEvent(EntityUid user, EntProtoId vendedPrototype, List<EntProtoId> items)
    {
        User = user;
        VendedPrototype = vendedPrototype;
        Items = items;
    }
}
