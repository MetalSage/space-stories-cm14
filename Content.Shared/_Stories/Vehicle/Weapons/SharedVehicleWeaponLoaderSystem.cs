using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleWeaponLoaderComponent, InteractHandEvent>(OnLoaderHandInteract);
        SubscribeLocalEvent<VehicleWeaponLoaderComponent, InteractUsingEvent>(OnLoaderInteractUsing);

        Subs.BuiEvents<VehicleWeaponLoaderComponent>(VehicleWeaponLoaderUI.Key,
            subs =>
            {
                subs.Event<VehicleWeaponLoaderSelectHardpointMsg>(OnLoaderSelectHardpoint);
            });
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
            _popup.PopupEntity("No compatible weapon found for this magazine type!", args.User);
            args.Handled = true;
            return;
        }

        if (!_skills.HasAllSkills(args.User, loader.Comp.Skills))
        {
            _popup.PopupEntity($"You lack the required skill to load this weapon!", args.User);
            args.Handled = true;
            return;
        }

        if (gunComp.SpareMagazinesContainer.ContainedEntities.Count >= gunComp.MaxSpareMagazines)
        {
            _popup.PopupEntity("The weapon's magazine storage is full!", args.User);
            args.Handled = true;
            return;
        }

        if (_net.IsServer)
        {
            if (_container.Insert(args.Used, gunComp.SpareMagazinesContainer))
            {
                //_audio.PlayPvs(LoadSound, loader.Owner);
                _popup.PopupEntity($"Magazine loaded into {Name(compatibleHardpoint.Value)}", args.User);
            }
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

        if (!string.IsNullOrEmpty(gun.RequiredSkill))
        {
            if (!_skills.HasSkill(args.Actor, gun.RequiredSkill, gun.RequiredSkillLevel))
            {
                _popup.PopupEntity("You lack the required skill to reload this weapon!", args.Actor);
                return;
            }
        }

        if (gun.SpareMagazinesContainer.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity("No spare magazines available!", args.Actor);
            return;
        }

        var spareMag = gun.SpareMagazinesContainer.ContainedEntities.First();

        if (_net.IsServer)
        {
            if (gun.ActiveMagazineContainer.ContainedEntity != null)
            {
                _container.Remove(gun.ActiveMagazineContainer.ContainedEntity.Value, gun.ActiveMagazineContainer);
                QueueDel(gun.ActiveMagazineContainer.ContainedEntity.Value);
            }

            _container.Remove(spareMag, gun.SpareMagazinesContainer);
            _container.Insert(spareMag, gun.ActiveMagazineContainer);

            //_audio.PlayPvs(LoadSound, hardpoint);
            _popup.PopupEntity($"Magazine loaded into {Name(hardpoint)}", args.Actor);
        }

        loader.Comp.SelectedHardpoint = hardpoint;
        Dirty(loader);
    }
}
