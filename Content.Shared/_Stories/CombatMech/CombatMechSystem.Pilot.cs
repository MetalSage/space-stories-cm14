using System.Numerics;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.CombatMech;


public sealed partial class CombatMechSystem
{
    private void OnStrapAttempt(Entity<CombatMechComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.User is not { } user)
            return;

        if (!_skills.HasSkill(user, ent.Comp.WeaponSkill, ent.Comp.WeaponSkillRequired))
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("stories-rx47-not-trained-pilot"), ent, user, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        if (GetWeapon(ent, true) == null || GetWeapon(ent, false) == null)
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("stories-rx47-missing-weapons"), ent, user, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        if (!TryComp(user, out HandsComponent? hands) || _hands.CountFreeHands((user, hands)) < 2)
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("stories-rx47-need-both-hands"), ent, user, PopupType.MediumCaution);
            args.Cancelled = true;
        }
    }

    private void OnUnstrapAttempt(Entity<CombatMechComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled ||
            !ent.Comp.HelmetClosed ||
            _forceEjectingPilots.Contains(args.Buckle.Owner))
        {
            return;
        }

        if (args.Popup)
            _popup.PopupClient(Loc.GetString("stories-rx47-faceplate-blocks-exit"), ent, args.User, PopupType.MediumCaution);

        args.Cancelled = true;
    }

    private void OnStrapped(Entity<CombatMechComponent> ent, ref StrappedEvent args)
    {
        var pilot = args.Buckle.Owner;

        ent.Comp.PilotEntity = pilot;
        DirtyField(ent.Owner, ent.Comp, nameof(CombatMechComponent.PilotEntity));

        var inside = EnsureComp<InsideCombatVehicleComponent>(pilot);
        inside.Vehicle = ent;
        DirtyField(pilot, inside, nameof(InsideCombatVehicleComponent.Vehicle));
        _pilotsInCombatMechs.Add(pilot);
        UpdatePilotProtection((pilot, inside));
        ApplyPilotVisuals((pilot, inside));

        _mover.SetRelay(pilot, ent);
        var relay = EnsureComp<InteractionRelayComponent>(pilot);
        _interaction.SetRelay(pilot, ent, relay);

        _audio.PlayPredicted(ent.Comp.EnterSound, ent, pilot);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (_net.IsServer)
        {
            _rmcPulling.TryStopAllPullsFromAndOn(pilot);

            if (!TransferWeaponToPilot(ent, pilot, true) ||
                !TransferWeaponToPilot(ent, pilot, false))
            {
                EjectPilotAfterWeaponTransferFailure(ent, pilot);
                return;
            }
        }

        UpdateAppearance(ent);
    }

    private void OnUnstrapped(Entity<CombatMechComponent> ent, ref UnstrappedEvent args)
    {
        var pilot = args.Buckle.Owner;

        ent.Comp.PilotEntity = null;
        DirtyField(ent.Owner, ent.Comp, nameof(CombatMechComponent.PilotEntity));

        if (TryComp(pilot, out InsideCombatVehicleComponent? inside))
        {
            RestorePilotProtection((pilot, inside));
            RestorePilotVisuals((pilot, inside));
        }
        else
        {
            RestorePilotVisualOffset(pilot);
            _rmcSprite.SetRenderOrder(pilot, 0);
        }

        _pilotsInCombatMechs.Remove(pilot);
        RemCompDeferred<InsideCombatVehicleComponent>(pilot);
        RemComp<RelayInputMoverComponent>(pilot);
        // SetRelay puts the relay target on the mech, not the pilot.
        RemComp<MovementRelayTargetComponent>(ent);
        RemCompDeferred<InteractionRelayComponent>(pilot);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (_net.IsServer)
        {
            TransferWeaponToMech(ent, pilot, true);
            TransferWeaponToMech(ent, pilot, false);
        }

        _audio.PlayPredicted(ent.Comp.EnterSound, ent, pilot);
        UpdateAppearance(ent);
    }

    private bool TransferWeaponToPilot(Entity<CombatMechComponent> ent, EntityUid pilot, bool primary)
    {
        if (GetWeapon(ent, primary) is not { } weapon)
            return false;

        if (!TryComp(pilot, out HandsComponent? pilotHands))
            return false;

        var hand = FindHand(pilot, pilotHands, primary ? HandLocation.Left : HandLocation.Right);
        if (hand == null)
            return false;

        LinkWeaponToMech(weapon, ent);

        // Only toggle UnremoveableComponent if we actually need to drop the weapon out of mech's hand.
        if (_hands.IsHolding(ent.Owner, weapon))
        {
            RemComp<UnremoveableComponent>(weapon);
            var dropped = _hands.TryDrop(ent.Owner, weapon, Transform(ent).Coordinates, checkActionBlocker: false, doDropInteraction: false);
            EnsureWeaponUnremoveable(weapon);
            if (!dropped)
                return false;
        }

        if (!_hands.TryPickup(pilot, weapon, hand, checkActionBlocker: false, animate: false, handsComp: pilotHands))
        {
            TransferWeaponToMech(ent, pilot, primary);
            return false;
        }

        EnsureWeaponUnremoveable(weapon);
        return true;
    }

    private void TransferWeaponToMech(Entity<CombatMechComponent> ent, EntityUid pilot, bool primary)
    {
        if (GetWeapon(ent, primary) is not { } weapon)
            return;

        if (!TryComp(ent.Owner, out HandsComponent? mechHands))
            return;

        var hand = FindHand(ent.Owner, mechHands, primary ? HandLocation.Left : HandLocation.Right);
        if (hand == null)
            return;

        LinkWeaponToMech(weapon, ent);

        if (_hands.IsHolding(ent.Owner, weapon))
        {
            EnsureWeaponUnremoveable(weapon);
            return;
        }

        // Only toggle UnremoveableComponent if we actually need to drop the weapon out of pilot's hand.
        if (_hands.IsHolding(pilot, weapon))
        {
            RemComp<UnremoveableComponent>(weapon);
            var dropped = _hands.TryDrop(pilot, weapon, Transform(ent).Coordinates, checkActionBlocker: false, doDropInteraction: false);
            EnsureWeaponUnremoveable(weapon);
            if (!dropped)
                return;
        }

        if (!_hands.TryPickup(ent.Owner, weapon, hand, checkActionBlocker: false, animate: false, handsComp: mechHands))
        {
            RemComp<UnremoveableComponent>(weapon);
            if (TryComp(weapon, out CombatMechWeaponComponent? weaponComp))
                ClearWeaponMechLink((weapon, weaponComp));
            SetWeapon(ent, primary, null);
            _transform.SetCoordinates(weapon, Transform(ent).Coordinates);
            Log.Warning($"RX47 failed to return {ToPrettyString(weapon)} to {ToPrettyString(ent.Owner)} hand {hand} ({(primary ? "primary" : "secondary")}).");
            return;
        }

        EnsureWeaponUnremoveable(weapon);
    }

    private void EjectPilotAfterWeaponTransferFailure(Entity<CombatMechComponent> mech, EntityUid pilot)
    {
        Log.Warning($"RX47 ejected {ToPrettyString(pilot)} from {ToPrettyString(mech.Owner)} after weapon transfer failed.");

        if (TryComp(pilot, out BuckleComponent? buckle))
        {
            if (_buckle.TryUnbuckle(pilot, pilot, buckle, popup: false))
            {
                UpdateAppearance(mech);
                return;
            }

            // TryUnbuckle was blocked by an event; force-release through the no-check Unbuckle.
            // It still raises UnstrappedEvent so OnUnstrapped runs the normal weapon-return path.
            _buckle.Unbuckle((pilot, buckle), null);
        }

        // Last-resort cleanup if buckle is missing or Unbuckle did not propagate to OnUnstrapped.
        if (mech.Comp.PilotEntity == pilot)
            CleanupFailedPilotStrap(mech, pilot);

        UpdateAppearance(mech);
    }

    private void CleanupFailedPilotStrap(Entity<CombatMechComponent> mech, EntityUid pilot)
    {
        // TryUnbuckle failed; pilot is still physically buckled.
        // Return any weapons that were partially transferred before the failure so the
        // mech slot is not permanently empty and the pilot is not stuck with an
        // unremoveable weapon they cannot drop.
        if (_net.IsServer)
        {
            TransferWeaponToMech(mech, pilot, true);
            TransferWeaponToMech(mech, pilot, false);
        }

        mech.Comp.PilotEntity = null;
        DirtyField(mech.Owner, mech.Comp, nameof(CombatMechComponent.PilotEntity));

        if (TryComp(pilot, out InsideCombatVehicleComponent? inside))
        {
            RestorePilotProtection((pilot, inside));
            RestorePilotVisuals((pilot, inside));
        }
        else
        {
            RestorePilotVisualOffset(pilot);
            _rmcSprite.SetRenderOrder(pilot, 0);
        }

        _pilotsInCombatMechs.Remove(pilot);
        RemCompDeferred<InsideCombatVehicleComponent>(pilot);
        RemComp<RelayInputMoverComponent>(pilot);
        RemComp<MovementRelayTargetComponent>(mech);
        RemCompDeferred<InteractionRelayComponent>(pilot);
        _movementSpeed.RefreshMovementSpeedModifiers(mech);
    }

    private void OnInsideVehicleMove(Entity<InsideCombatVehicleComponent> ent, ref MoveEvent args)
    {
        UpdatePilotVisualOffset(ent);
    }

    private void OnInsideVehicleStartup(Entity<InsideCombatVehicleComponent> ent, ref ComponentStartup args)
    {
        ApplyPilotVisuals(ent);
    }

    private void OnInsideVehicleState(Entity<InsideCombatVehicleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyPilotVisuals(ent);
    }

    private void OnInsideVehicleShutdown(Entity<InsideCombatVehicleComponent> ent, ref ComponentShutdown args)
    {
        RestorePilotVisuals(ent);
    }

    private void OnInsideVehicleGetEyeOffsetAttempt(Entity<InsideCombatVehicleComponent> ent, ref GetEyeOffsetAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnInsideVehicleGetVerbs(Entity<InsideCombatVehicleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.User != ent.Owner || !HasLiveVehicle(ent))
            return;

        // RMCSuicideSystem adds its verb without a stable id, so the localized title is the only handle we have.
        var suicide = Loc.GetString("rmc-suicide");
        args.Verbs.RemoveWhere(v => v.Text == suicide);
    }

    private void ApplyPilotVisuals(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!HasLiveVehicle(pilot) ||
            !TryComp(pilot.Comp.Vehicle, out CombatMechComponent? mech))
        {
            RestorePilotVisuals(pilot);
            return;
        }

        UpdatePilotVisualOffset(pilot);
        _rmcSprite.SetRenderOrder(pilot.Owner, mech.PilotRenderOrder);
        _rmcSprite.UpdateDrawDepth(pilot.Owner);
    }

    private void UpdatePilotVisualOffset(Entity<InsideCombatVehicleComponent> pilot)
    {
        if (!HasLiveVehicle(pilot) ||
            !TryComp(pilot.Comp.Vehicle, out CombatMechComponent? mech))
        {
            RestorePilotVisualOffset(pilot.Owner);
            return;
        }

        var direction = Transform(pilot.Owner).LocalRotation.GetCardinalDir();
        var offset = direction switch
        {
            Direction.North => mech.PilotVisualOffsetNorth,
            Direction.South => mech.PilotVisualOffsetSouth,
            Direction.East or Direction.West => mech.PilotVisualOffsetEastWest,
            _ => mech.PilotVisualOffsetEastWest,
        };

        _rmcSprite.SetOffset(pilot.Owner, offset);
    }

    private void RestorePilotVisualOffset(EntityUid pilot)
    {
        _rmcSprite.SetOffset(pilot, Vector2.Zero);
    }

    private void RestorePilotVisuals(Entity<InsideCombatVehicleComponent> pilot)
    {
        RestorePilotVisualOffset(pilot.Owner);
        _rmcSprite.SetRenderOrder(pilot.Owner, 0);
    }
}
