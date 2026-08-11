using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Charge;

namespace Content.Shared._Stories.Xenonids.Crusher;

public sealed class STCrusherChargeSystem : EntitySystem
{
    [Dependency] private readonly CMArmorSystem _armor = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCrusherChargeWindupArmorComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<STCrusherChargeTuningComponent, XenoChargeActionEvent>(OnChargeAction, after: new[] { typeof(XenoChargeSystem) });
        SubscribeLocalEvent<STCrusherChargeTuningComponent, XenoChargeDoAfterEvent>(OnChargeDoAfter, after: new[] { typeof(XenoChargeSystem) });
    }

    private void OnGetArmor(Entity<STCrusherChargeWindupArmorComponent> ent, ref CMGetArmorEvent args)
    {
        args.FrontalArmor += ent.Comp.FrontalArmor;
    }

    private void OnChargeAction(Entity<STCrusherChargeTuningComponent> ent, ref XenoChargeActionEvent args)
    {
        if (HasComp<STCrusherChargeWindupArmorComponent>(ent.Owner))
            _armor.UpdateArmorValue(ent.Owner);
    }

    private void OnChargeDoAfter(Entity<STCrusherChargeTuningComponent> ent, ref XenoChargeDoAfterEvent args)
    {
        _armor.UpdateArmorValue(ent.Owner);
    }
}
