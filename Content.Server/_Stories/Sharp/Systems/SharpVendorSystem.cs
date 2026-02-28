using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Marines;
using Content.Shared._Stories.Sharp;
using Content.Shared.Access.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._Stories.Sharp;

public sealed class SharpVendorSystem : EntitySystem
{
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpSpecialistVendorComponent, AfterItemVendedEvent>(OnAfterItemVended);
    }

    private void OnAfterItemVended(Entity<SharpSpecialistVendorComponent> ent, ref AfterItemVendedEvent args)
    {
        var prototypeId = MetaData(args.Item).EntityPrototype?.ID;
        if (prototypeId == null || prototypeId != ent.Comp.EquipmentCase)
            return;

        if (!_idCard.TryGetIdCard(args.User, out var idCard))
            return;

        _idCard.TryChangeJobTitle(idCard.Owner, Loc.GetString(ent.Comp.SpecialistRole), idCard.Comp, args.User);

        if (ent.Comp.SpecialistPrefix != null &&
            TryComp<JobPrefixComponent>(args.User, out var prefix) &&
            prefix.AdditionalPrefix == ent.Comp.SpecialistPrefix)
        {
            prefix.AdditionalPrefix = null;
            Dirty(args.User, prefix);
        }
    }
}
