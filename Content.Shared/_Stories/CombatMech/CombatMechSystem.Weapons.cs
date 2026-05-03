using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.CombatMech;


public sealed partial class CombatMechSystem
{
    private void LinkWeaponToMech(EntityUid weapon, Entity<CombatMechComponent> mech)
    {
        if (!TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return;

        if (weaponComp.LinkedMech == mech.Owner)
            return;

        weaponComp.LinkedMech = mech;
        DirtyField(weapon, weaponComp, nameof(CombatMechWeaponComponent.LinkedMech));
    }

    private void EnsureWeaponUnremoveable(EntityUid weapon)
    {
        var unremoveable = EnsureComp<UnremoveableComponent>(weapon);
        if (!unremoveable.DeleteOnDrop)
            return;

        unremoveable.DeleteOnDrop = false;
        Dirty(weapon, unremoveable);
    }

    private void OnInstallWeaponDoAfter(Entity<CombatMechComponent> ent, ref CombatMechInstallWeaponDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used == null)
            return;

        args.Handled = true;
        InstallWeapon(ent, args.User, args.Used.Value, args.Primary);
    }

    private void OnDetachWeaponDoAfter(Entity<CombatMechComponent> ent, ref CombatMechDetachWeaponDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        DetachWeapon(ent, args.User, args.Primary);
    }

    private void StartInstallWeapon(Entity<CombatMechComponent> mech, EntityUid user, EntityUid weapon, bool primary)
    {
        if (!CanModifyWeapons(mech, user) || !TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return;

        if (weaponComp.LinkedMech != null && !Deleted(weaponComp.LinkedMech.Value))
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-already-linked"), mech, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CombatMechInstallWeaponDoAfterEvent { Primary = primary };
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.WeaponInstallDelay, ev, mech, mech, used: weapon)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var slot = GetSlotName(primary);
            _popup.PopupPredicted(Loc.GetString("stories-rx47-weapon-install-start-self", ("slot", slot)),
                Loc.GetString("stories-rx47-weapon-install-start-others", ("user", user), ("slot", slot)),
                user,
                user);
        }
    }

    private void StartDetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, bool primary)
    {
        if (!CanModifyWeapons(mech, user))
            return;

        if (GetWeapon(mech, primary) == null)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-slot-empty"), mech, user, PopupType.MediumCaution);
            return;
        }

        if (_hands.CountFreeHands(user) <= 0)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-need-free-hand"), mech, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CombatMechDetachWeaponDoAfterEvent { Primary = primary };
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.WeaponDetachDelay, ev, mech, mech)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var slot = GetSlotName(primary);
            _popup.PopupPredicted(Loc.GetString("stories-rx47-weapon-detach-start-self", ("slot", slot)),
                Loc.GetString("stories-rx47-weapon-detach-start-others", ("user", user), ("slot", slot)),
                user,
                user);
        }
    }

    private bool InstallWeapon(Entity<CombatMechComponent> mech, EntityUid user, EntityUid weapon, bool primary)
    {
        if (!CanModifyWeapons(mech, user) || !TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return false;

        if (weaponComp.LinkedMech != null && !Deleted(weaponComp.LinkedMech.Value))
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-already-linked"), mech, user, PopupType.MediumCaution);
            return false;
        }

        if (GetWeapon(mech, primary) != null && !DetachWeapon(mech, user, primary, false))
            return false;

        var mechCoordinates = Transform(mech).Coordinates;
        if (_hands.IsHolding(user, weapon) &&
            !_hands.TryDrop(user, weapon, mechCoordinates, checkActionBlocker: false, doDropInteraction: false))
        {
            return false;
        }

        SetWeapon(mech, primary, weapon);
        LinkWeaponToMech(weapon, mech);

        var mounted = false;
        if (TryComp(mech, out HandsComponent? hands))
        {
            var hand = FindHand(mech, hands, primary ? HandLocation.Left : HandLocation.Right);
            if (hand != null && _hands.TryPickup(mech, weapon, hand, checkActionBlocker: false, animate: false, handsComp: hands))
            {
                EnsureWeaponUnremoveable(weapon);
                mounted = true;
            }
        }

        if (!mounted)
        {
            ClearWeaponMechLink((weapon, weaponComp));
            SetWeapon(mech, primary, null);
            _transform.SetCoordinates(weapon, mechCoordinates);
            return false;
        }

        UpdateAppearance(mech);

        var slot = GetSlotName(primary);
        _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-installed", ("weapon", weapon), ("slot", slot)), mech, user);
        return true;
    }

    private bool DetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, bool primary, bool pickup = true)
    {
        if (GetWeapon(mech, primary) is not { } weapon)
            return false;

        var coordinates = Transform(mech).Coordinates;
        var heldByMech = _hands.IsHolding(mech.Owner, weapon);
        if (heldByMech &&
            !_hands.TryDrop(mech.Owner, weapon, coordinates, checkActionBlocker: false, doDropInteraction: false))
        {
            return false;
        }

        RemComp<UnremoveableComponent>(weapon);

        if (TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            ClearWeaponMechLink((weapon, weaponComp));

        if (!heldByMech)
            _transform.SetCoordinates(weapon, coordinates);

        SetWeapon(mech, primary, null);

        if (pickup)
            _hands.TryPickup(user, weapon, checkActionBlocker: false, animate: false);

        UpdateAppearance(mech);

        if (pickup)
        {
            var slot = GetSlotName(primary);
            _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-detached", ("weapon", weapon), ("slot", slot)), mech, user);
        }

        return true;
    }

    private void OnWeaponGetAlternativeVerbs(Entity<CombatMechWeaponComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (!TryComp(user, out InsideCombatVehicleComponent? inside) ||
            Deleted(inside.Vehicle) ||
            !TryComp(inside.Vehicle, out CombatMechComponent? mech) ||
            !IsMountedWeapon((inside.Vehicle, mech), ent.Owner))
        {
            return;
        }

        if (!HasComp<AttachableHolderComponent>(ent.Owner) ||
            !_container.TryGetContainer(ent.Owner, CombatMechComponent.UnderbarrelSlot, out var container))
        {
            return;
        }

        foreach (var attachable in container.ContainedEntities)
        {
            if (!TryComp(attachable, out AttachableToggleableComponent? toggleable))
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = toggleable.ActionName,
                IconEntity = GetNetEntity(attachable),
                Act = () =>
                {
                    var ev = new AttachableToggleStartedEvent(ent.Owner, user, CombatMechComponent.UnderbarrelSlot);
                    RaiseLocalEvent(attachable, ref ev);
                },
                Priority = 90,
            });

            return;
        }
    }

    private void OnWeaponAttemptShoot(Entity<CombatMechWeaponComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryResolveAndLinkWeaponMech(ent, args.User, out var mech))
        {
            if (_net.IsClient &&
                TryComp(args.User, out InsideCombatVehicleComponent? inside) &&
                !Deleted(inside.Vehicle))
            {
                return;
            }

            ClearWeaponMechLink(ent);
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-not-linked");
            return;
        }

        if (args.User != mech.Owner &&
            (!TryComp(args.User, out InsideCombatVehicleComponent? pilot) || pilot.Vehicle != mech.Owner))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-not-linked");
            return;
        }

        if (!InFiringArc(mech.Owner, ent.Comp.FiringArc, args.ToCoordinates))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-out-of-arc");
        }
    }

    private void OnCombatMechUnderbarrelShoot(CombatMechUnderbarrelShootEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot)
            return;

        if (!_combatMode.IsInCombatMode(pilot))
            return;

        if (msg.Weapon is not { } netWeapon)
            return;

        var weapon = GetEntity(netWeapon);
        if (!TryGetMountedUnderbarrel(pilot, weapon, false, out var attachable, out var gun))
            return;

        ResetMountedUnderbarrelShotCounter(attachable, gun);
        if (_timing.CurTime < gun.NextFire)
            return;

        var coordinates = GetCoordinates(msg.Coordinates);
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        var weaponMap = Transform(weapon).MapID;
        if (mapCoordinates.MapId != weaponMap)
            return;

        if (TryComp(attachable, out RMCFlamerTankComponent? tank))
        {
            var weaponCoordinates = _transform.GetMapCoordinates(weapon);
            var maxRange = tank.MaxRange;
            if ((mapCoordinates.Position - weaponCoordinates.Position).LengthSquared() > maxRange * maxRange)
                return;
        }

        var target = GetEntity(msg.Target);
        if (target is { } targetUid &&
            (Deleted(targetUid) || Transform(targetUid).MapID != weaponMap))
        {
            return;
        }

#pragma warning disable RA0002
        gun.ShootCoordinates = coordinates;
        gun.Target = target;
        // ShootCoordinates and Target are transient: set immediately before AttemptShoot and cleared in the
        // finally block within the same tick, so they never persist across a network snapshot. DirtyField is
        // called only for the fields that outlive the tick (ShotCounter).
        try
        {
            if (TryComp(attachable, out CombatMechWeaponFlamerTankComponent? flamerTank))
                SyncWeaponTankToMountedFlamer((attachable, flamerTank), weapon);

            _gun.AttemptShoot(pilot, attachable, gun, userSession: args.SenderSession);
        }
        finally
        {
            gun.ShootCoordinates = null;
            gun.Target = null;

            // The input system never sends a stop-shoot event, so SemiAuto would latch after the first shot.
            // Reset ShotCounter so the next predictive event can fire again (rate-limited by NextFire).
            ResetMountedUnderbarrelShotCounter(attachable, gun);
        }
#pragma warning restore RA0002
    }

    private void ResetMountedUnderbarrelShotCounter(EntityUid attachable, GunComponent gun)
    {
        if (gun.ShotCounter == 0)
            return;

#pragma warning disable RA0002
        gun.ShotCounter = 0;
        if (_net.IsServer)
            EntityManager.DirtyField(attachable, gun, nameof(GunComponent.ShotCounter));
#pragma warning restore RA0002
    }

    private void OnWeaponContainerRemoveAttempt(Entity<CombatMechWeaponComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != CombatMechComponent.GunMagazineContainerId &&
            args.Container.ID != CombatMechComponent.GunChamberContainerId)
            return;

        if (ent.Comp.LinkedMech == null || Deleted(ent.Comp.LinkedMech.Value))
            return;

        args.Cancel();
    }

    private void OnWeaponInteractUsing(Entity<CombatMechWeaponComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnWeaponItemSlotEjectAttempt(Entity<CombatMechWeaponComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is not { } user || !IsMountedWeaponForPilot(user, ent))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, user, PopupType.MediumCaution);
    }

    private void OnWeaponTryAmmoEject(Entity<CombatMechWeaponComponent> ent, ref RMCTryAmmoEjectEvent args)
    {
        if (!IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnWeaponUseInHand(Entity<CombatMechWeaponComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsMountedWeaponForPilot(args.User, ent))
            return;

        if (TryToggleMountedAttachable(ent.Owner, args.User))
        {
            args.Handled = true;
            return;
        }

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), ent, args.User, PopupType.MediumCaution);
    }

    private void EnsureWeapon(Entity<CombatMechComponent> mech, bool primary)
    {
        if (GetWeapon(mech, primary) is { })
            return;

        if (!TryComp(mech, out HandsComponent? hands))
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not spawn default weapon: no hands component.");
            return;
        }

        var hand = FindHand(mech, hands, primary ? HandLocation.Left : HandLocation.Right);
        if (hand == null)
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not spawn default weapon: missing {(primary ? "left" : "right")} hand.");
            return;
        }

        var proto = primary ? mech.Comp.PrimaryWeapon : mech.Comp.SecondaryWeapon;
        if (string.IsNullOrEmpty(proto))
            return;

        if (!_proto.HasIndex<EntityPrototype>(proto))
        {
            Log.Error($"RX47 {ToPrettyString(mech.Owner)} default weapon prototype '{proto}' does not exist.");
            return;
        }

        var spawned = Spawn(proto, Transform(mech).Coordinates);

        if (!TryComp(spawned, out CombatMechWeaponComponent? weaponComp))
        {
            Log.Error($"RX47 {ToPrettyString(mech.Owner)} default weapon prototype {proto} lacks CombatMechWeaponComponent.");
            QueueDel(spawned);
            return;
        }

        if (!_hands.TryPickup(mech, spawned, hand, checkActionBlocker: false, animate: false, handsComp: hands))
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not pick up spawned default weapon {ToPrettyString(spawned)}.");
            QueueDel(spawned);
            return;
        }

        SetWeapon(mech, primary, spawned);
        LinkWeaponToMech(spawned, mech);
        EnsureWeaponUnremoveable(spawned);
    }

    private string? FindHand(EntityUid uid, HandsComponent hands, HandLocation location)
    {
        foreach (var hand in _hands.EnumerateHands((uid, hands)))
        {
            if (!_hands.TryGetHand(uid, hand, out var data))
                continue;

            if (data.Value.Location == location)
                return hand;
        }

        return null;
    }

    private EntityUid? GetWeapon(Entity<CombatMechComponent> ent, bool primary)
    {
        var weapon = primary ? ent.Comp.PrimaryWeaponEntity : ent.Comp.SecondaryWeaponEntity;
        if (weapon == null || Deleted(weapon.Value))
            return null;

        return weapon.Value;
    }

    private bool IsMountedWeapon(Entity<CombatMechComponent> mech, EntityUid weapon)
    {
        return GetWeapon(mech, true) == weapon || GetWeapon(mech, false) == weapon;
    }

    private bool IsMountedWeaponForPilot(EntityUid user, Entity<CombatMechWeaponComponent> weapon)
    {
        if (!TryComp(user, out InsideCombatVehicleComponent? inside) ||
            Deleted(inside.Vehicle) ||
            !TryComp(inside.Vehicle, out CombatMechComponent? mech))
        {
            return false;
        }

        return IsMountedWeapon((inside.Vehicle, mech), weapon);
    }

    private bool TryResolveAndLinkWeaponMech(
        Entity<CombatMechWeaponComponent> weapon,
        EntityUid user,
        out Entity<CombatMechComponent> mech)
    {
        if (weapon.Comp.LinkedMech is { } linked &&
            !Deleted(linked) &&
            TryComp(linked, out CombatMechComponent? linkedComp) &&
            IsMountedWeapon((linked, linkedComp), weapon.Owner))
        {
            mech = (linked, linkedComp);
            return true;
        }

        if (TryComp(user, out InsideCombatVehicleComponent? inside) &&
            !Deleted(inside.Vehicle) &&
            TryComp(inside.Vehicle, out CombatMechComponent? insideComp))
        {
            if (IsMountedWeapon((inside.Vehicle, insideComp), weapon.Owner))
            {
                LinkWeaponToMech(weapon, (inside.Vehicle, insideComp));
                mech = (inside.Vehicle, insideComp);
                return true;
            }

        }

        mech = default;
        return false;
    }

    private void OnWeaponGetIFFGunUser(Entity<CombatMechWeaponComponent> ent, ref GetIFFGunUserEvent args)
    {
        if (args.GunUser != null ||
            ent.Comp.LinkedMech is not { } mech ||
            Deleted(mech) ||
            !TryComp(mech, out CombatMechComponent? mechComp))
        {
            return;
        }

        args.GunUser = GetPilot((mech, mechComp));
    }

    private void OnMountedAttachableAttemptShoot(Entity<CombatMechUnderbarrelComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled ||
            !TryResolveMountedAttachable(ent.Owner, args.User, out var weapon, out var mech))
        {
            return;
        }

        if (!InFiringArc(mech.Owner, weapon.Comp.FiringArc, args.ToCoordinates))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-out-of-arc");
        }
    }

    private void OnWeaponFlamerAttemptShoot(Entity<CombatMechWeaponFlamerTankComponent> ent, ref AttemptShootEvent args)
    {
        SyncWeaponTankToMountedFlamer(ent);
    }

    private void OnWeaponFlamerGetAmmoCount(Entity<CombatMechWeaponFlamerTankComponent> ent, ref GetAmmoCountEvent args)
    {
        SyncWeaponTankToMountedFlamer(ent);
    }

    private void OnWeaponFlamerGunShot(Entity<CombatMechWeaponFlamerTankComponent> ent, ref GunShotEvent args)
    {
        if (!TryGetMountedFlamerLocalTank(ent, out var localSolution) ||
            !TryGetMountedWeaponFlamerTank(ent, out var weaponSolution))
        {
            return;
        }

        CopySolution(localSolution, weaponSolution);
    }

    private void SyncWeaponTankToMountedFlamer(Entity<CombatMechWeaponFlamerTankComponent> ent, EntityUid? weapon = null)
    {
        if (!TryGetMountedFlamerLocalTank(ent, out var localSolution))
            return;

        if (!TryGetMountedWeaponFlamerTank(ent, out var weaponSolution, weapon))
        {
            ClearSolution(localSolution);
            return;
        }

        CopySolution(weaponSolution, localSolution);
    }

    private bool TryGetMountedWeaponFlamerTank(
        Entity<CombatMechWeaponFlamerTankComponent> ent,
        out Entity<SolutionComponent> solution,
        EntityUid? weapon = null)
    {
        solution = default;

        if (weapon is { } directWeapon &&
            HasComp<CombatMechWeaponComponent>(directWeapon))
        {
            return TryGetWeaponFlamerTank(directWeapon, ent.Comp.WeaponTankContainerId, out solution);
        }

        if (!TryGetContainingCombatMechWeapon(ent.Owner, out var holder))
            return false;

        return TryGetWeaponFlamerTank(holder, ent.Comp.WeaponTankContainerId, out solution);
    }

    private bool TryGetContainingCombatMechWeapon(EntityUid uid, out EntityUid weapon)
    {
        weapon = default;

        var current = uid;
        while (_container.TryGetContainingContainer((current, null), out var container))
        {
            current = container.Owner;
            if (!HasComp<CombatMechWeaponComponent>(current))
                continue;

            weapon = current;
            return true;
        }

        return false;
    }

    private bool TryGetWeaponFlamerTank(EntityUid weapon, string containerId, out Entity<SolutionComponent> solution)
    {
        solution = default;

        if (!_container.TryGetContainer(weapon, containerId, out var tankContainer) ||
            tankContainer.ContainedEntities.Count == 0)
        {
            return false;
        }

        var tankId = tankContainer.ContainedEntities[0];
        if (!TryComp(tankId, out RMCFlamerTankComponent? tank) ||
            !_solution.TryGetSolution(tankId, tank.SolutionId, out var solutionEnt))
        {
            return false;
        }

        solution = solutionEnt.Value;
        return true;
    }

    private bool TryGetMountedFlamerLocalTank(
        Entity<CombatMechWeaponFlamerTankComponent> ent,
        out Entity<SolutionComponent> solution)
    {
        solution = default;

        if (TryComp(ent.Owner, out RMCFlamerTankComponent? selfTank) &&
            _solution.TryGetSolution(ent.Owner, selfTank.SolutionId, out var selfSolutionEnt))
        {
            solution = selfSolutionEnt.Value;
            return true;
        }

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.LocalTankContainerId, out var tankContainer) ||
            tankContainer.ContainedEntities.Count == 0)
        {
            return false;
        }

        var tankId = tankContainer.ContainedEntities[0];
        if (!TryComp(tankId, out RMCFlamerTankComponent? tank) ||
            !_solution.TryGetSolution(tankId, tank.SolutionId, out var solutionEnt))
        {
            return false;
        }

        solution = solutionEnt.Value;
        return true;
    }

    private void CopySolution(Entity<SolutionComponent> source, Entity<SolutionComponent> target)
    {
        ClearSolution(target);
        if (source.Comp.Solution.Volume > 0)
            _solution.ForceAddSolution(target, source.Comp.Solution.Clone());
    }

    private void ClearSolution(Entity<SolutionComponent> solution)
    {
        _solution.RemoveAllSolution(solution);
    }

    private bool TryResolveMountedAttachable(
        EntityUid attachable,
        EntityUid user,
        out Entity<CombatMechWeaponComponent> weapon,
        out Entity<CombatMechComponent> mech)
    {
        weapon = default;
        mech = default;

        if (!_container.TryGetOuterContainer(attachable, Transform(attachable), out var container) ||
            !TryComp(container.Owner, out CombatMechWeaponComponent? weaponComp))
        {
            return false;
        }

        var weaponEnt = (container.Owner, weaponComp);
        if (!TryResolveAndLinkWeaponMech(weaponEnt, user, out mech))
            return false;

        if (user != mech.Owner &&
            (!TryComp(user, out InsideCombatVehicleComponent? pilot) || pilot.Vehicle != mech.Owner))
        {
            return false;
        }

        weapon = weaponEnt;
        return true;
    }

    private bool TryGetMountedUnderbarrel(
        EntityUid user,
        EntityUid weapon,
        bool requireActive,
        out EntityUid attachable,
        out GunComponent gun)
    {
        attachable = default;
        gun = default!;

        if (!TryComp(weapon, out CombatMechWeaponComponent? weaponComp) ||
            !IsMountedWeaponForPilot(user, (weapon, weaponComp)) ||
            !_container.TryGetContainer(weapon, CombatMechComponent.UnderbarrelSlot, out var container) ||
            container.Count <= 0)
        {
            return false;
        }

        foreach (var contained in container.ContainedEntities)
        {
            if (!HasComp<CombatMechUnderbarrelComponent>(contained) ||
                !TryComp(contained, out AttachableToggleableComponent? toggleable) ||
                requireActive && !toggleable.Active ||
                !TryComp(contained, out GunComponent? gunComp))
            {
                continue;
            }

            attachable = contained;
            gun = gunComp;
            return true;
        }

        return false;
    }

    private void ClearWeaponMechLink(Entity<CombatMechWeaponComponent> weapon)
    {
        if (weapon.Comp.LinkedMech == null)
            return;

        weapon.Comp.LinkedMech = null;
        DirtyField(weapon.Owner, weapon.Comp, nameof(CombatMechWeaponComponent.LinkedMech));
    }

    private void SetWeapon(Entity<CombatMechComponent> mech, bool primary, EntityUid? weapon)
    {
        var state = CombatMechComponent.EmptyWeaponState;
        if (weapon != null && TryComp(weapon.Value, out CombatMechWeaponComponent? weaponComp))
            state = BuildWeaponState(weaponComp.ArmState, primary);

        if (primary)
        {
            mech.Comp.PrimaryWeaponEntity = weapon;
            mech.Comp.PrimaryWeaponState = state;
            DirtyField(mech.Owner, mech.Comp, nameof(CombatMechComponent.PrimaryWeaponEntity));
            DirtyField(mech.Owner, mech.Comp, nameof(CombatMechComponent.PrimaryWeaponState));
        }
        else
        {
            mech.Comp.SecondaryWeaponEntity = weapon;
            mech.Comp.SecondaryWeaponState = state;
            DirtyField(mech.Owner, mech.Comp, nameof(CombatMechComponent.SecondaryWeaponEntity));
            DirtyField(mech.Owner, mech.Comp, nameof(CombatMechComponent.SecondaryWeaponState));
        }
    }

    private string GetSlotName(bool primary) =>
        Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");

    private static string BuildWeaponState(string armState, bool primary) =>
        $"weapon_{armState}_{(primary ? "left" : "right")}";

    private bool CanModifyWeapons(Entity<CombatMechComponent> mech, EntityUid user)
    {
        if (_skills.HasSkill(user, mech.Comp.WeaponSkill, mech.Comp.WeaponSkillRequired))
            return true;

        _popup.PopupClient(Loc.GetString("stories-rx47-weapon-not-trained"), mech, user, PopupType.MediumCaution);
        return false;
    }

    private bool TryGetHeldMechWeapon(EntityUid user, out EntityUid weapon)
    {
        weapon = EntityUid.Invalid;

        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (!HasComp<CombatMechWeaponComponent>(held))
                continue;

            weapon = held;
            return true;
        }

        return false;
    }

    private bool TryToggleMountedAttachable(EntityUid weapon, EntityUid user)
    {
        if (!_container.TryGetContainer(weapon, CombatMechComponent.UnderbarrelSlot, out var container) || container.Count <= 0)
            return false;

        // RX47 underbarrel slots are locked single-slot containers, so the first entity is the active module.
        var attachable = container.ContainedEntities[0];
        if (!HasComp<AttachableToggleableComponent>(attachable))
            return false;

        var ev = new AttachableToggleStartedEvent(weapon, user, CombatMechComponent.UnderbarrelSlot);
        RaiseLocalEvent(attachable, ref ev);
        return true;
    }

    private bool InFiringArc(EntityUid mech, float arc, EntityCoordinates? target)
    {
        if (target == null)
            return false;

        var from = _transform.GetMapCoordinates(mech);
        var to = _transform.ToMapCoordinates(target.Value).Position;
        var diff = to - from.Position;
        if (diff.LengthSquared() < FiringTargetEpsilon)
            return false;

        var facing = _transform.GetWorldRotation(mech);
        var targetAngle = diff.ToWorldAngle();
        var delta = Math.Abs(Angle.ShortestDistance(facing, targetAngle).Degrees);
        return delta <= arc / 2f;
    }
}
