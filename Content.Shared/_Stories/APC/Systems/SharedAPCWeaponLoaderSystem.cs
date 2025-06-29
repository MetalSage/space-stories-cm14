using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Attachables;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCWeaponLoaderSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCWeaponLoaderComponent, InteractHandEvent>(OnLoaderHandInteractUsing);
        SubscribeLocalEvent<APCWeaponLoaderComponent, InteractUsingEvent>(OnLoaderInteractUsing);

        SubscribeLocalEvent<APCEntityComponent, EntInsertedIntoContainerMessage>(OnAmmoLoaded);
    }

    private void OnLoaderHandInteractUsing(Entity<APCWeaponLoaderComponent> loader, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<XenoComponent>(args.User))
            return;

        if (!TryGetAPC(loader.Owner, out var apc))
            return;

        _ui.OpenUi(apc.Owner, APCSelectHardpointUI.Key, args.User);
        args.Handled = true;
    }


    private void OnLoaderInteractUsing(Entity<APCWeaponLoaderComponent> loader, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<XenoComponent>(args.User))
            return;

        if (!TryGetAPC(loader.Owner, out var apc))
            return;

        if (!HasComp<APCGunMagazineComponent>(args.Used))
            return;

        var xform = Transform(apc.Owner);

        if (_net.IsServer)
            _container.Insert(args.Used, apc.Comp.AmmoStorage, containerXform: xform);

        args.Handled = true;
    }

    private void OnAmmoLoaded(Entity<APCEntityComponent> apc, ref EntInsertedIntoContainerMessage args)
    {
        if (apc.Comp.ActiveHardpoint is not { } hardpoint)
            return;

        if (apc.Comp.AmmoStorage != args.Container)
            return;

        var ev = new APCGunReloadEvent(args.Entity);
        RaiseLocalEvent(hardpoint, ref ev);
    }

    private bool TryGetAPC(EntityUid loader, out Entity<APCEntityComponent> apc)
    {
        apc = default;

        if (!TryComp<TransformComponent>(loader, out var xform) ||
            !TryComp<APCEntityGridComponent>(xform.GridUid, out var apcGrid) ||
            !TryGetEntity(apcGrid.APC, out var apcUid) ||
            apcUid is not { } uid ||
            !TryComp<APCEntityComponent>(uid, out var apcComp))
        {
            return false;
        }

        apc = (uid, apcComp);
        return true;
    }
}
