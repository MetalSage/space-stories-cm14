using Content.Shared._RMC14.Marines.Orders;
using Content.Shared.Clothing;
using Content.Shared.Hands;

namespace Content.Shared._Stories.LeadershipWhistle;
public sealed partial class LeadershipWhistleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeadershipWhistleComponent, ClothingGotEquippedEvent>(OnWhistleEquip);
        SubscribeLocalEvent<LeadershipWhistleComponent, ClothingGotUnequippedEvent>(OnWhistleUnequip);

        SubscribeLocalEvent<LeadershipWhistleComponent, GotEquippedHandEvent>(OnWhistleEquip);
        SubscribeLocalEvent<LeadershipWhistleComponent, GotUnequippedHandEvent>(OnWhistleUnequip);
    }

    private void OnWhistleEquip(Entity<LeadershipWhistleComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!TryComp<MarineOrdersComponent>(args.User, out var marineOrders) || marineOrders.OrderRange >= ent.Comp.MaxOrderAreaBuff)
            return;

        marineOrders.Cooldown += ent.Comp.ActionOrderCooldownDebuff;
        marineOrders.OrderRange += ent.Comp.OrderAreaBuff;
    }
    private void OnWhistleUnequip(Entity<LeadershipWhistleComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!TryComp<MarineOrdersComponent>(args.User, out var marineOrders))
            return;

        marineOrders.Cooldown -= ent.Comp.ActionOrderCooldownDebuff;
        marineOrders.OrderRange -= ent.Comp.OrderAreaBuff;
    }

    private void OnWhistleEquip(Entity<LeadershipWhistleComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!TryComp<MarineOrdersComponent>(args.Wearer, out var marineOrders) || marineOrders.OrderRange >= ent.Comp.MaxOrderAreaBuff)
            return;

        marineOrders.Cooldown += ent.Comp.ActionOrderCooldownDebuff;
        marineOrders.OrderRange += ent.Comp.OrderAreaBuff;
    }

    private void OnWhistleUnequip(Entity<LeadershipWhistleComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!TryComp<MarineOrdersComponent>(args.Wearer, out var marineOrders))
            return;

        marineOrders.Cooldown -= ent.Comp.ActionOrderCooldownDebuff;
        marineOrders.OrderRange -= ent.Comp.OrderAreaBuff;
    }
}
