using System.Numerics;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers;
using Content.Shared.FixedPoint;
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
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Player;
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
        SetInstallInProgress(ent, args.Slot, false);

        if (args.Cancelled || args.Handled || args.Used == null || Deleted(args.Used.Value))
            return;

        if (!_interaction.InRangeUnobstructed(args.User, ent.Owner))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        InstallWeapon(ent, args.User, args.Used.Value, args.Slot);
    }

    private void OnDetachWeaponDoAfter(Entity<CombatMechComponent> ent, ref CombatMechDetachWeaponDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        DetachWeapon(ent, args.User, args.Slot);
    }

    private void StartInstallWeapon(Entity<CombatMechComponent> mech, EntityUid user, EntityUid weapon, WeaponSlot slot)
    {
        if (!CanModifyWeapons(mech, user) || !TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return;

        var slotName = GetSlotName(slot);

        if (GetWeapon(mech, slot) != null)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-slot-occupied", ("slot", slotName)),
                mech, user, PopupType.MediumCaution);
            return;
        }

        if (IsInstallInProgress(mech, slot))
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-install-in-progress", ("slot", slotName)),
                mech, user, PopupType.MediumCaution);
            return;
        }

        if (weaponComp.LinkedMech != null && !Deleted(weaponComp.LinkedMech.Value))
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-already-linked"), mech, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CombatMechInstallWeaponDoAfterEvent { Slot = slot };
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.WeaponInstallDelay, ev, mech, mech, used: weapon)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        SetInstallInProgress(mech, slot, true);

        if (!_timing.IsFirstTimePredicted)
            return;

        _popup.PopupPredicted(Loc.GetString("stories-rx47-weapon-install-start-self", ("slot", slotName)),
            Loc.GetString("stories-rx47-weapon-install-start-others", ("user", user), ("slot", slotName)),
            user, user);
    }

    private void StartDetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, WeaponSlot slot)
    {
        if (!CanModifyWeapons(mech, user))
            return;

        if (GetWeapon(mech, slot) == null)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-weapon-slot-empty"), mech, user, PopupType.MediumCaution);
            return;
        }

        if (_hands.CountFreeHands(user) <= 0)
        {
            _popup.PopupClient(Loc.GetString("stories-rx47-need-free-hand"), mech, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CombatMechDetachWeaponDoAfterEvent { Slot = slot };
        var doAfter = new DoAfterArgs(EntityManager, user, mech.Comp.WeaponDetachDelay, ev, mech, mech)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
        };

        if (!_doAfter.TryStartDoAfter(doAfter) || !_timing.IsFirstTimePredicted)
            return;

        var slotName = GetSlotName(slot);
        _popup.PopupPredicted(Loc.GetString("stories-rx47-weapon-detach-start-self", ("slot", slotName)),
            Loc.GetString("stories-rx47-weapon-detach-start-others", ("user", user), ("slot", slotName)),
            user, user);
    }

    private bool IsInstallInProgress(Entity<CombatMechComponent> mech, WeaponSlot slot)
    {
        return slot == WeaponSlot.Primary
            ? mech.Comp.PrimaryWeaponInstallInProgress
            : mech.Comp.SecondaryWeaponInstallInProgress;
    }

    private void SetInstallInProgress(Entity<CombatMechComponent> mech, WeaponSlot slot, bool installing)
    {
        if (slot == WeaponSlot.Primary)
            mech.Comp.PrimaryWeaponInstallInProgress = installing;
        else
            mech.Comp.SecondaryWeaponInstallInProgress = installing;
    }

    private bool InstallWeapon(Entity<CombatMechComponent> mech, EntityUid user, EntityUid weapon, WeaponSlot slot)
    {
        if (!CanModifyWeapons(mech, user) || !TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return false;

        if (GetWeapon(mech, slot) == weapon && weaponComp.LinkedMech == mech.Owner)
            return true;

        var slotName = GetSlotName(slot);

        // Server-only path (doafter completion): PopupClient is a no-op on the server.
        if (GetWeapon(mech, slot) != null)
        {
            _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-slot-occupied", ("slot", slotName)),
                mech, user, PopupType.MediumCaution);
            return false;
        }

        if (weaponComp.LinkedMech is { } linkedMech && !Deleted(linkedMech))
        {
            if (TryComp(linkedMech, out CombatMechComponent? linkedComp) &&
                IsMountedWeapon((linkedMech, linkedComp), weapon))
            {
                _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-already-linked"), mech, user, PopupType.MediumCaution);
                return false;
            }

            ClearWeaponMechLink((weapon, weaponComp));
        }

        var safeDropCoordinates = GetSafeWeaponDropCoordinates(mech, user);
        if (_hands.IsHolding(user, weapon) &&
            !_hands.TryDrop(user, weapon, safeDropCoordinates, checkActionBlocker: false, doDropInteraction: false))
        {
            return false;
        }

        SetWeapon(mech, slot, weapon);
        LinkWeaponToMech(weapon, mech);

        var mounted = false;
        if (TryComp(mech, out HandsComponent? hands))
        {
            var hand = FindHand(mech, hands, GetHandLocationFor(slot));
            if (hand != null && _hands.TryPickup(mech, weapon, hand, checkActionBlocker: false, animate: false, handsComp: hands))
            {
                EnsureWeaponUnremoveable(weapon);
                mounted = true;
            }
        }

        if (!mounted)
        {
            ClearWeaponMechLink((weapon, weaponComp));
            SetWeapon(mech, slot, null);
            _transform.SetCoordinates(weapon, safeDropCoordinates);
            return false;
        }

        UpdateAppearance(mech);

        _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-installed", ("weapon", weapon), ("slot", slotName)), mech, user);
        return true;
    }

    private void DetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, WeaponSlot slot)
    {
        if (GetWeapon(mech, slot) is not { } weapon)
            return;

        var coordinates = GetSafeWeaponDropCoordinates(mech, user);
        var heldByMech = _hands.IsHolding(mech.Owner, weapon);

        // UnremoveableComponent cancels the drop's CanRemove check — strip first, drop, then re-add.
        RemComp<UnremoveableComponent>(weapon);

        if (heldByMech &&
            !_hands.TryDrop(mech.Owner, weapon, coordinates, checkActionBlocker: false, doDropInteraction: false))
        {
            EnsureWeaponUnremoveable(weapon);
            return;
        }

        if (TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            ClearWeaponMechLink((weapon, weaponComp));

        if (!heldByMech)
            _transform.SetCoordinates(weapon, coordinates);

        SetWeapon(mech, slot, null);

        _hands.TryPickup(user, weapon, checkActionBlocker: false, animate: false);

        UpdateAppearance(mech);

        _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-detached", ("weapon", weapon), ("slot", GetSlotName(slot))), mech, user);
    }

    private EntityCoordinates GetSafeWeaponDropCoordinates(Entity<CombatMechComponent> mech, EntityUid user)
    {
        var mechXform = Transform(mech.Owner);
        var mechMap = _transform.GetMapCoordinates(mech.Owner, mechXform);
        var userMap = _transform.GetMapCoordinates(user);
        var dropDistance = mech.Comp.WeaponDetachDropDistance;
        if (mechMap.MapId != userMap.MapId)
            return mechXform.Coordinates.Offset(new Vector2(0f, -dropDistance));

        var direction = userMap.Position - mechMap.Position;
        if (direction.LengthSquared() < DirectionEpsilon)
            direction = new Vector2(0f, -1f);
        else
            direction = direction.Normalized();

        var parentRotation = _transform.GetWorldRotation(mechXform.ParentUid);
        var localDirection = (-parentRotation).RotateVec(direction);
        return mechXform.Coordinates.Offset(localDirection * dropDistance);
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

        var underbarrelSlot = mech.UnderbarrelSlot;
        if (!HasComp<AttachableHolderComponent>(ent.Owner) ||
            !_container.TryGetContainer(ent.Owner, underbarrelSlot, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return;
        }

        var attachable = container.ContainedEntities[0];
        if (!TryComp(attachable, out AttachableToggleableComponent? toggleable))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = toggleable.ActionName,
            IconEntity = GetNetEntity(attachable),
            Act = () =>
            {
                var ev = new AttachableToggleStartedEvent(ent.Owner, user, underbarrelSlot);
                RaiseLocalEvent(attachable, ref ev);
            },
            Priority = 90,
        });
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
            args.Message = Loc.GetString("stories-rx47-weapon-pilot-mismatch");
            return;
        }

        if (!InFiringArc(mech.Owner, ent.Comp.FiringArc, args.ToCoordinates))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-weapon-out-of-arc");
        }
    }

    private void OnWeaponContainerRemoveAttempt(Entity<CombatMechWeaponComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.LinkedMech is not { } mechUid || Deleted(mechUid) ||
            !TryComp(mechUid, out CombatMechComponent? mech))
        {
            return;
        }

        if (args.Container.ID != mech.GunMagazineContainerId &&
            args.Container.ID != mech.GunChamberContainerId)
        {
            return;
        }

        args.Cancel();
    }

    private void OnWeaponInteractUsing(Entity<CombatMechWeaponComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Handled = true;
        PopupCannotModifyPiloted(ent, args.User);
    }

    private void OnWeaponItemSlotEjectAttempt(Entity<CombatMechWeaponComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is not { } user || !IsMountedWeaponForPilot(user, ent))
            return;

        args.Cancelled = true;
        PopupCannotModifyPiloted(ent, user);
    }

    private void OnWeaponTryAmmoEject(Entity<CombatMechWeaponComponent> ent, ref RMCTryAmmoEjectEvent args)
    {
        if (!IsMountedWeaponForPilot(args.User, ent))
            return;

        args.Cancelled = true;
        PopupCannotModifyPiloted(ent, args.User);
    }

    private bool EnsureWeapon(Entity<CombatMechComponent> mech, WeaponSlot slot)
    {
        if (GetWeapon(mech, slot) is { })
            return true;

        if (!TryComp(mech, out HandsComponent? hands))
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not spawn default weapon: no hands component.");
            return false;
        }

        var handLocation = GetHandLocationFor(slot);
        var hand = FindHand(mech, hands, handLocation);
        if (hand == null)
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not spawn default weapon: missing {handLocation} hand.");
            return false;
        }

        var proto = slot == WeaponSlot.Primary ? mech.Comp.PrimaryWeapon : mech.Comp.SecondaryWeapon;
        if (string.IsNullOrEmpty(proto))
            return true;

        if (!_proto.HasIndex<EntityPrototype>(proto))
        {
            Log.Error($"RX47 {ToPrettyString(mech.Owner)} default weapon prototype '{proto}' does not exist.");
            return false;
        }

        var spawned = Spawn(proto, Transform(mech).Coordinates);

        if (!HasComp<CombatMechWeaponComponent>(spawned))
        {
            Log.Error($"RX47 {ToPrettyString(mech.Owner)} default weapon prototype {proto} lacks CombatMechWeaponComponent.");
            QueueDel(spawned);
            return false;
        }

        if (!_hands.TryPickup(mech, spawned, hand, checkActionBlocker: false, animate: false, handsComp: hands))
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not pick up spawned default weapon {ToPrettyString(spawned)}.");
            QueueDel(spawned);
            return false;
        }

        SetWeapon(mech, slot, spawned);
        LinkWeaponToMech(spawned, mech);
        EnsureWeaponUnremoveable(spawned);
        return true;
    }

    private void PopupCannotModifyPiloted(EntityUid target, EntityUid user)
    {
        _popup.PopupClient(Loc.GetString("stories-rx47-cannot-modify-piloted"), target, user, PopupType.MediumCaution);
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

    private EntityUid? GetWeapon(Entity<CombatMechComponent> ent, WeaponSlot slot)
    {
        var weapon = slot == WeaponSlot.Primary ? ent.Comp.PrimaryWeaponEntity : ent.Comp.SecondaryWeaponEntity;
        if (weapon == null || Deleted(weapon.Value))
            return null;

        return weapon.Value;
    }

    private bool IsMountedWeapon(Entity<CombatMechComponent> mech, EntityUid weapon)
    {
        return GetWeapon(mech, WeaponSlot.Primary) == weapon || GetWeapon(mech, WeaponSlot.Secondary) == weapon;
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
        if (args.Cancelled)
            return;

        if (!TryResolveMountedAttachable(ent.Owner, args.User, out var weapon, out var mech))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("stories-rx47-underbarrel-pilot-only");
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
        if (args.Cancelled)
            return;

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

    private void SyncWeaponTankToMountedFlamer(Entity<CombatMechWeaponFlamerTankComponent> ent)
    {
        if (!TryGetMountedFlamerLocalTank(ent, out var localSolution))
            return;

        if (!TryGetMountedWeaponFlamerTank(ent, out var weaponSolution))
        {
            ClearSolution(localSolution);
            return;
        }

        CopySolution(weaponSolution, localSolution);
    }

    private bool TryGetMountedWeaponFlamerTank(
        Entity<CombatMechWeaponFlamerTankComponent> ent,
        out Entity<SolutionComponent> solution)
    {
        solution = default;

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
        var sourceSol = source.Comp.Solution;
        var targetSol = target.Comp.Solution;

        if (SolutionsEquivalent(sourceSol, targetSol))
            return;

        _solution.RemoveAllSolution(target);
        if (sourceSol.Volume <= FixedPoint2.Zero)
            return;

        foreach (var reagent in sourceSol.Contents)
            _solution.TryAddReagent(target, reagent, out _);
    }

    private void ClearSolution(Entity<SolutionComponent> solution)
    {
        _solution.RemoveAllSolution(solution);
    }

    private static bool SolutionsEquivalent(Solution a, Solution b)
    {
        if (a.Volume != b.Volume)
            return false;

        if (a.Contents.Count != b.Contents.Count)
            return false;

        for (var i = 0; i < a.Contents.Count; i++)
        {
            var aq = a.Contents[i];
            var bq = b.Contents[i];
            if (aq.Reagent != bq.Reagent || aq.Quantity != bq.Quantity)
                return false;
        }

        return true;
    }

    private bool TryResolveMountedAttachable(
        EntityUid attachable,
        EntityUid user,
        out Entity<CombatMechWeaponComponent> weapon,
        out Entity<CombatMechComponent> mech)
    {
        weapon = default;
        mech = default;

        if (!TryGetContainingCombatMechWeapon(attachable, out var holderWeapon) ||
            !TryComp(holderWeapon, out CombatMechWeaponComponent? weaponComp))
        {
            return false;
        }

        var weaponEnt = (holderWeapon, weaponComp);
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

    private void ClearWeaponMechLink(Entity<CombatMechWeaponComponent> weapon)
    {
        if (weapon.Comp.LinkedMech == null)
            return;

        weapon.Comp.LinkedMech = null;
        DirtyField(weapon.Owner, weapon.Comp, nameof(CombatMechWeaponComponent.LinkedMech));
    }

    private void SetWeapon(Entity<CombatMechComponent> mech, WeaponSlot slot, EntityUid? weapon)
    {
        var state = mech.Comp.EmptyWeaponState;
        if (weapon != null && TryComp(weapon.Value, out CombatMechWeaponComponent? weaponComp))
            state = BuildWeaponState(weaponComp.ArmState, slot);

        if (slot == WeaponSlot.Primary)
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

    private static HandLocation GetHandLocationFor(WeaponSlot slot) =>
        slot == WeaponSlot.Primary ? HandLocation.Left : HandLocation.Right;

    private string GetSlotName(WeaponSlot slot) =>
        Loc.GetString(slot == WeaponSlot.Primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");

    private static string BuildWeaponState(string armState, WeaponSlot slot) =>
        $"weapon_{armState}_{(slot == WeaponSlot.Primary ? "left" : "right")}";

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
