using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Attachables;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Content.Shared._Stories.Vehicle;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleWeaponLoaderSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedVehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleWeaponLoaderComponent, InteractHandEvent>(OnLoaderHandInteractUsing);
        SubscribeLocalEvent<VehicleWeaponLoaderComponent, InteractUsingEvent>(OnLoaderInteractUsing);

        SubscribeLocalEvent<VehicleComponent, EntInsertedIntoContainerMessage>(OnAmmoLoaded);
    }

    private void OnLoaderHandInteractUsing(Entity<VehicleWeaponLoaderComponent> loader, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<XenoComponent>(args.User))
            return;

        if (!_vehicle.TryGetVehicle(loader.Owner, out var apc))
            return;

        _ui.OpenUi(apc.Owner, VehicleSelectHardpointUI.Key, args.User);
        args.Handled = true;
    }


    private void OnLoaderInteractUsing(Entity<VehicleWeaponLoaderComponent> loader, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<XenoComponent>(args.User))
            return;

        if (!_vehicle.TryGetVehicle(loader.Owner, out var apc))
            return;

        if (!HasComp<VehicleGunMagazineComponent>(args.Used))
            return;

        var xform = Transform(apc.Owner);

        if (_net.IsServer)
            _container.Insert(args.Used, apc.Comp.AmmoStorage, containerXform: xform);

        args.Handled = true;
    }

    private void OnAmmoLoaded(Entity<VehicleComponent> apc, ref EntInsertedIntoContainerMessage args)
    {
        if (apc.Comp.ActiveHardpoint is not { } hardpoint)
            return;

        if (apc.Comp.AmmoStorage != args.Container)
            return;

        var ev = new VehicleGunReloadEvent(args.Entity);
        RaiseLocalEvent(hardpoint, ref ev);
    }
}
