using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;

namespace Content.Shared._RMC14.Whistle;
public sealed partial class STWhistleSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private readonly List<string> _whistleSlots = new List<string> { "mask", "neck" };
    public override void Initialize()
    {
        base.Initialize();
    }

    public bool TryGetWhistleInSlotOrHands(EntityUid user, out Entity<RMCWhistleComponent>? whistle)
    {
        whistle = null;

        foreach (var slot in _whistleSlots)
        {
            if ((_inventory.TryGetSlotEntity(user, slot, out var item) && TryComp<RMCWhistleComponent>(item, out var whistleComp)))
            {
                whistle = (item.Value, whistleComp);
                return true;
            }
        }


        foreach (var item in _hands.EnumerateHeld(user))
        {
            if (TryComp<RMCWhistleComponent>(item, out var whistleComp))
            {
                whistle = (item, whistleComp);
                return true;
            }
        }

        return false;
    }
}
