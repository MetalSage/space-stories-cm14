using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Marines;
using Content.Shared._Stories.Sharp;
using Content.Shared.Access.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._Stories.Sharp;

public sealed class SharpVendorSystem : EntitySystem
{
    private const string SharpEquipmentCaseId = "RMCSharpSpecEquipmentCase";
    private const string SharpSpecialistRoleLoc = "rmc-job-name-weapons-specialist-sharp";
    private const string SharpSpecialistPrefixLoc = "rmc-job-prefix-weapons-specialist-sharp";

    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpSpecialistVendorComponent, AfterItemVendedEvent>(OnAfterItemVended);
    }

    private void OnAfterItemVended(Entity<SharpSpecialistVendorComponent> ent, ref AfterItemVendedEvent args)
    {
        if (MetaData(args.Item).EntityPrototype?.ID != SharpEquipmentCaseId)
            return;

        if (!_idCard.TryGetIdCard(args.User, out var idCard))
            return;

        _idCard.TryChangeJobTitle(idCard.Owner, Loc.GetString(SharpSpecialistRoleLoc), idCard.Comp, args.User);

        if (TryComp<JobPrefixComponent>(args.User, out var prefix) &&
            prefix.AdditionalPrefix == SharpSpecialistPrefixLoc)
        {
            prefix.AdditionalPrefix = null;
            Dirty(args.User, prefix);
        }
    }
}
