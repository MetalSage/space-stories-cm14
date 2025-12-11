using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleWeaponLoaderSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedVehicleSystem _vehicle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleWeaponLoaderComponent, InteractHandEvent>(OnLoaderHandInteract);
        SubscribeLocalEvent<VehicleWeaponLoaderComponent, InteractUsingEvent>(OnLoaderInteractUsing);
        SubscribeLocalEvent<VehicleGunMagazineComponent, MapInitEvent>(OnMagazineMapInit);

        SubscribeLocalEvent<VehicleWeaponLoaderComponent, BoundUIOpenedEvent>(OnWeaponLoaderUIOpened);

        Subs.BuiEvents<VehicleWeaponLoaderComponent>(VehicleWeaponLoaderUI.Key,
            subs =>
            {
                subs.Event<VehicleWeaponLoaderSelectHardpointMsg>(OnLoaderSelectHardpoint);
            });
    }

    private void OnWeaponLoaderUIOpened(Entity<VehicleWeaponLoaderComponent> loader, ref BoundUIOpenedEvent args)
    {
        UpdateLoaderUI(loader);
    }

    private void OnMagazineMapInit(Entity<VehicleGunMagazineComponent> magazine, ref MapInitEvent args)
    {
        UpdateAppearance(magazine);
    }

    private void OnLoaderHandInteract(Entity<VehicleWeaponLoaderComponent> loader, ref InteractHandEvent args)
    {
        if (args.Handled || HasComp<XenoComponent>(args.User))
            return;

        if (!_vehicle.TryGetVehicle(loader.Owner, out var vehicle))
            return;

        _ui.OpenUi(loader.Owner, VehicleWeaponLoaderUI.Key, args.User);
        args.Handled = true;
    }

    private void OnLoaderInteractUsing(Entity<VehicleWeaponLoaderComponent> loader, ref InteractUsingEvent args)
    {
        if (args.Handled || HasComp<XenoComponent>(args.User))
            return;

        if (!TryComp<VehicleGunMagazineComponent>(args.Used, out var magazine))
            return;

        if (!_vehicle.TryGetVehicle(loader.Owner, out var vehicle))
            return;

        EntityUid? compatibleHardpoint = null;
        VehicleGunComponent? gunComp = null;

        foreach (var hardpoint in vehicle.Comp.Hardpoints)
        {
            if (!TryComp<VehicleGunComponent>(hardpoint, out var gun))
                continue;

            if (!gun.AcceptedMagazineTypes.Contains(magazine.MagazineType))
                continue;

            compatibleHardpoint = hardpoint;
            gunComp = gun;
            break;
        }

        if (compatibleHardpoint == null || gunComp == null)
        {
            _popup.PopupCursor(Loc.GetString("st-vehicle-loader-no-compatible-weapon"), args.User);
            args.Handled = true;
            return;
        }

        if (!_skills.HasAllSkills(args.User, loader.Comp.Skills))
        {
            _popup.PopupCursor(Loc.GetString("st-vehicle-loader-skill-required"), args.User);
            args.Handled = true;
            return;
        }

        if (gunComp.SpareMagazinesContainer.ContainedEntities.Count >= gunComp.MaxSpareMagazines)
        {
            _popup.PopupCursor(Loc.GetString("st-vehicle-loader-storage-full"), args.User);
            args.Handled = true;
            return;
        }

        if (_net.IsServer && _container.Insert(args.Used, gunComp.SpareMagazinesContainer))
        {
            _popup.PopupEntity(Loc.GetString("st-vehicle-loader-magazine-loaded",
                ("weapon", Name(compatibleHardpoint.Value))), loader.Owner, args.User);
            _audio.PlayPvs(loader.Comp.LoadSound, loader.Owner);
            UpdateLoaderUI(loader);
        }

        args.Handled = true;
    }

    private void OnLoaderSelectHardpoint(Entity<VehicleWeaponLoaderComponent> loader, ref VehicleWeaponLoaderSelectHardpointMsg args)
    {
        var hardpoint = GetEntity(args.Hardpoint);

        if (!TryComp<VehicleGunComponent>(hardpoint, out var gun))
            return;

        if (!_vehicle.TryGetVehicle(loader.Owner, out var vehicle))
            return;

        if (!vehicle.Comp.Hardpoints.Contains(hardpoint))
            return;

        if (!_skills.HasAllSkills(args.Actor, loader.Comp.Skills))
        {
            _popup.PopupCursor(Loc.GetString("st-vehicle-loader-skill-required"), args.Actor);
            return;
        }

        if (gun.SpareMagazinesContainer.ContainedEntities.Count == 0)
        {
            _popup.PopupCursor(Loc.GetString("st-vehicle-loader-no-spare-magazines"), args.Actor);
            return;
        }

        var spareMag = gun.SpareMagazinesContainer.ContainedEntities.First();

        if (_net.IsServer)
        {
            if (gun.ActiveMagazineContainer.ContainedEntity != null)
            {
                var oldMag = gun.ActiveMagazineContainer.ContainedEntity.Value;
                _container.Remove(oldMag, gun.ActiveMagazineContainer);

                var loaderCoords = _transform.GetMapCoordinates(loader.Owner);
                _transform.SetMapCoordinates(oldMag, loaderCoords);

                if (TryComp<VehicleGunMagazineComponent>(oldMag, out var oldMagComp))
                    UpdateAppearance((oldMag, oldMagComp));
            }

            _container.Remove(spareMag, gun.SpareMagazinesContainer);
            _container.Insert(spareMag, gun.ActiveMagazineContainer);

            _popup.PopupEntity(Loc.GetString("st-vehicle-loader-magazine-loaded",
                ("weapon", Name(hardpoint))), loader.Owner, args.Actor);
            _audio.PlayPvs(loader.Comp.LoadSound, loader.Owner);
            Dirty(hardpoint, gun);

            UpdateLoaderUI(loader);
            _vehicle.UpdateVehicleStatusUI(vehicle);
        }

        loader.Comp.SelectedHardpoint = hardpoint;
        Dirty(loader);
    }

    private void UpdateAppearance(Entity<VehicleGunMagazineComponent> magazine)
    {
        var state = magazine.Comp.Shots > 0 ? VehicleAmmoState.Fill : VehicleAmmoState.Empty;
        _appearance.SetData(magazine, VehicleAmmoVisuals.Layer, state);
    }

    public void UpdateLoaderUI(Entity<VehicleWeaponLoaderComponent> loader)
    {
        if (!_vehicle.TryGetVehicle(loader.Owner, out var vehicle))
            return;

        var hardpoints = new List<HardpointInfo>();

        foreach (var hardpoint in vehicle.Comp.Hardpoints)
        {
            if (!TryComp<VehicleGunComponent>(hardpoint, out var gun))
                continue;

            var hasActiveMag = gun.ActiveMagazineContainer.ContainedEntity != null;
            var spareCount = gun.SpareMagazinesContainer.ContainedEntities.Count;
            var maxSpares = gun.MaxSpareMagazines;

            int currentAmmo = 0;
            int maxAmmo = 0;
            if (hasActiveMag && TryComp<VehicleGunMagazineComponent>(gun.ActiveMagazineContainer.ContainedEntity!.Value, out var activeMag))
            {
                currentAmmo = activeMag.Shots;
                maxAmmo = activeMag.Capacity;
            }

            hardpoints.Add(new HardpointInfo
            {
                Entity = GetNetEntity(hardpoint),
                Name = Name(hardpoint),
                HasActiveMagazine = hasActiveMag,
                SpareCount = spareCount,
                MaxSpares = maxSpares,
                CurrentAmmo = currentAmmo,
                MaxAmmo = maxAmmo
            });
        }

        var state = new VehicleWeaponLoaderWindowState
        {
            Hardpoints = hardpoints
        };

        _ui.SetUiState(loader.Owner, VehicleWeaponLoaderUI.Key, state);
    }
}
