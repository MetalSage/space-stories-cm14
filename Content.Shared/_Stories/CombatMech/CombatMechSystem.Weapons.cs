using System.Numerics;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared._RMC14.Xenonids.Paralyzing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.CombatMech;


public sealed partial class CombatMechSystem
{
    private void LinkWeaponToMech(EntityUid weapon, Entity<CombatMechComponent> mech)
    {
        if (!TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
            return;

        weaponComp.LinkedMech = mech;
        Dirty(weapon, weaponComp);
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
            var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
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
            var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
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

        DetachWeapon(mech, user, primary, false);

        if (_hands.IsHolding(user, weapon))
            _hands.TryDrop(user, weapon, Transform(mech).Coordinates, checkActionBlocker: false, doDropInteraction: false);

        SetWeapon(mech, primary, weapon);

        weaponComp.LinkedMech = mech;
        Dirty(weapon, weaponComp);

        if (TryComp(mech, out HandsComponent? hands))
        {
            var hand = FindHand(mech, hands, primary ? HandLocation.Left : HandLocation.Right);
            if (hand != null && _hands.TryPickup(mech, weapon, hand, checkActionBlocker: false, animate: false, handsComp: hands))
                EnsureComp<UnremoveableComponent>(weapon);
        }

        UpdateAppearance(mech);

        var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
        _popup.PopupEntity(Loc.GetString("stories-rx47-weapon-installed", ("weapon", weapon), ("slot", slot)), mech, user);
        return true;
    }

    private bool DetachWeapon(Entity<CombatMechComponent> mech, EntityUid user, bool primary, bool pickup = true)
    {
        if (GetWeapon(mech, primary) is not { } weapon)
            return false;

        RemComp<UnremoveableComponent>(weapon);

        if (TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
        {
            weaponComp.LinkedMech = null;
            Dirty(weapon, weaponComp);
        }

        if (_hands.IsHolding(mech.Owner, weapon))
            _hands.TryDrop(mech.Owner, weapon, Transform(mech).Coordinates, checkActionBlocker: false, doDropInteraction: false);
        else
            _transform.SetCoordinates(weapon, Transform(mech).Coordinates);

        SetWeapon(mech, primary, null);

        if (pickup)
            _hands.TryPickup(user, weapon, checkActionBlocker: false, animate: false);

        UpdateAppearance(mech);

        if (pickup)
        {
            var slot = Loc.GetString(primary ? "stories-rx47-left-slot" : "stories-rx47-right-slot");
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
            !_container.TryGetContainer(ent.Owner, UnderbarrelSlot, out var container))
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
                    var ev = new AttachableToggleStartedEvent(ent.Owner, user, UnderbarrelSlot);
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

        if (!TryResolveWeaponMech(ent, args.User, out var mech))
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

        if (msg.Weapon is not { } netWeapon ||
            !TryGetMountedUnderbarrel(pilot, GetEntity(netWeapon), false, out var attachable, out var gun))
            return;

#pragma warning disable RA0002
        gun.ShootCoordinates = GetCoordinates(msg.Coordinates);
        gun.Target = GetEntity(msg.Target);

        _gun.AttemptShoot(pilot, attachable, gun, userSession: args.SenderSession);

        // The input system never sends a stop-shoot event, so SemiAuto would latch after the first shot.
        // Reset ShotCounter so the next predictive event can fire again (rate-limited by NextFire).
        gun.ShotCounter = 0;
        EntityManager.DirtyField(attachable, gun, nameof(GunComponent.ShotCounter));
#pragma warning restore RA0002
    }

    private void OnWeaponContainerRemoveAttempt(Entity<CombatMechWeaponComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != "gun_magazine" && args.Container.ID != "gun_chamber")
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

        var spawned = Spawn(proto, Transform(mech).Coordinates);

        if (!_hands.TryPickup(mech, spawned, hand, checkActionBlocker: false, animate: false, handsComp: hands))
        {
            Log.Warning($"RX47 {ToPrettyString(mech.Owner)} could not pick up spawned default weapon {ToPrettyString(spawned)}.");
            QueueDel(spawned);
            return;
        }

        SetWeapon(mech, primary, spawned);

        var weaponComp = EnsureComp<CombatMechWeaponComponent>(spawned);
        weaponComp.LinkedMech = mech;
        Dirty(spawned, weaponComp);
        EnsureComp<UnremoveableComponent>(spawned);
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

    private bool TryResolveWeaponMech(
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

            if (_net.IsClient && IsHolding(user, weapon.Owner))
            {
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
        if (!TryResolveWeaponMech(weaponEnt, user, out mech))
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
            !_container.TryGetContainer(weapon, UnderbarrelSlot, out var container) ||
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
        Dirty(weapon);
    }

    private bool IsHolding(EntityUid user, EntityUid item)
    {
        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (held == item)
                return true;
        }

        return false;
    }

    private void SetWeapon(Entity<CombatMechComponent> mech, bool primary, EntityUid? weapon)
    {
        if (primary)
            mech.Comp.PrimaryWeaponEntity = weapon;
        else
            mech.Comp.SecondaryWeaponEntity = weapon;

        var state = string.Empty;
        if (weapon != null && TryComp(weapon.Value, out CombatMechWeaponComponent? weaponComp))
            state = $"weapon_{weaponComp.ArmState}_{(primary ? "left" : "right")}";

        if (primary)
            mech.Comp.PrimaryWeaponState = state;
        else
            mech.Comp.SecondaryWeaponState = state;

        Dirty(mech);
    }

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
        if (!_container.TryGetContainer(weapon, UnderbarrelSlot, out var container) || container.Count <= 0)
            return false;

        // RX47 underbarrel slots are locked single-slot containers, so the first entity is the active module.
        var attachable = container.ContainedEntities[0];
        if (!HasComp<AttachableToggleableComponent>(attachable))
            return false;

        var ev = new AttachableToggleStartedEvent(weapon, user, UnderbarrelSlot);
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
        if (diff.LengthSquared() < 0.01f)
            return false;

        var facing = _transform.GetWorldRotation(mech);
        var targetAngle = diff.ToWorldAngle();
        var delta = Math.Abs(Angle.ShortestDistance(facing, targetAngle).Degrees);
        return delta <= arc / 2f;
    }
}
