using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializePlasmaCaster()
    {
        SubscribeLocalEvent<HunterBracerComponent, TogglePlasmaCasterEvent>(OnTogglePlasmaCaster);

        SubscribeLocalEvent<PlasmaCasterComponent, DroppedEvent>(OnCasterDropped);
        SubscribeLocalEvent<PlasmaCasterComponent, UseInHandEvent>(
            OnCasterUseInHand,
            new[] { typeof(SharedGunSystem) }
        );
        SubscribeLocalEvent<PlasmaCasterComponent, TakeAmmoEvent>(OnCasterTakeAmmo);
        SubscribeLocalEvent<PlasmaCasterComponent, GunRefreshModifiersEvent>(OnCasterRefreshModifiers);

        SubscribeLocalEvent<PlasmaCasterComponent, AttemptShootEvent>(OnCasterAttemptShoot);
    }

    private void OnCasterAttemptShoot(Entity<PlasmaCasterComponent> ent, ref AttemptShootEvent args)
    {
        var bracerUid = GetEntity(ent.Comp.Bracer);
        if (bracerUid.HasValue && TryComp<HunterBracerComponent>(bracerUid.Value, out var bracer))
        {
            if (!bracer.CasterDeployed)
                args.Cancelled = true;

            if (!IsAuthorized(args.User, bracer))
                args.Cancelled = true;
        }
        else
            args.Cancelled = true;
    }

    private void OnCasterDropped(Entity<PlasmaCasterComponent> ent, ref DroppedEvent args)
    {
        var bracerUid = GetEntity(ent.Comp.Bracer);
        if (bracerUid.HasValue && TryComp<HunterBracerComponent>(bracerUid.Value, out var bracerComp))
            RetractPlasmaCaster(args.User, (bracerUid.Value, bracerComp));
    }

    private void HandleCasterUniqueAction(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } user)
            return;

        if (
            !_hands.TryGetActiveItem(user, out var heldItem)
            || !TryComp<PlasmaCasterComponent>(heldItem, out var caster)
        )
            return;

        var isStun = caster.Mode is PlasmaCasterMode.Stun or PlasmaCasterMode.Immobilizer;
        var newMode = isStun ? PlasmaCasterMode.Lethal : PlasmaCasterMode.Stun;

        ChangeCasterModeLogic(user, (heldItem.Value, caster), newMode);
    }

    private void OnTogglePlasmaCaster(Entity<HunterBracerComponent> ent, ref TogglePlasmaCasterEvent args)
    {
        if (args.Handled)
            return;

        if (!AttemptUsage(args.Performer, ent))
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        var isDeployed = ent.Comp.CasterDeployed;

        if (isDeployed)
            RetractPlasmaCaster(args.Performer, ent);
        else
            DeployPlasmaCaster(args.Performer, ent);
    }

    private void DeployPlasmaCaster(EntityUid user, Entity<HunterBracerComponent> bracer)
    {
        if (!_hands.TryGetEmptyHand(user, out var handName))
        {
            _popup.PopupEntity(Loc.GetString("st-bracer-caster-no-free-hand"), user, user);
            return;
        }

        if (!TryDrainPower(user, bracer, bracer.Comp.PlasmaCasterDeployCost))
            return;

        if (
            !_itemSlots.TryGetSlot(bracer.Owner, PlasmaCasterSlotId, out var bracerSlot)
            || bracerSlot.Item is not { } holderUid
        )
            return;

        if (!_itemSlots.TryGetSlot(holderUid, CasterHolderWeaponSlotId, out var holderSlot))
            return;

        var wasLocked = holderSlot.Locked;
        _itemSlots.SetLock(holderUid, holderSlot, false);

        try
        {
            if (_itemSlots.TryEject(holderUid, holderSlot, user, out var ejectedItem, true))
            {
                if (_hands.TryPickup(user, ejectedItem.Value, handName, false, animate: false))
                {
                    _audio.PlayPvs(bracer.Comp.CasterDeploySound, user);
                    _popup.PopupEntity(Loc.GetString("st-bracer-caster-deployed"), user, user);
                    if (bracer.Comp.TogglePlasmaCasterAction.HasValue)
                        _actions.SetToggled(bracer.Comp.TogglePlasmaCasterAction, true);

                    EnsureComp<UnremoveableComponent>(ejectedItem.Value);
                    bracer.Comp.CasterDeployed = true;
                    Dirty(bracer);
                }
                else
                    _itemSlots.TryInsert(holderUid, holderSlot, ejectedItem.Value, null, true);
            }
        }
        finally
        {
            _itemSlots.SetLock(holderUid, holderSlot, wasLocked);
        }
    }

    private void RetractPlasmaCaster(EntityUid user, Entity<HunterBracerComponent> bracer)
    {
        if (
            !_itemSlots.TryGetSlot(bracer.Owner, PlasmaCasterSlotId, out var bracerSlot)
            || bracerSlot.Item is not { } holderUid
        )
            return;

        if (!_itemSlots.TryGetSlot(holderUid, CasterHolderWeaponSlotId, out var holderSlot))
            return;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (
                TryComp<PlasmaCasterComponent>(held, out var casterComp)
                && GetEntity(casterComp.Bracer) == bracer.Owner
            )
            {
                RemComp<UnremoveableComponent>(held);

                var wasLocked = holderSlot.Locked;
                _itemSlots.SetLock(holderUid, holderSlot, false);
                try
                {
                    if (_itemSlots.TryInsert(holderUid, holderSlot, held, user, true))
                    {
                        _audio.PlayPvs(bracer.Comp.CasterRetractSound, user);
                        _popup.PopupEntity(Loc.GetString("st-bracer-caster-retracted"), user, user);
                        if (bracer.Comp.TogglePlasmaCasterAction.HasValue)
                            _actions.SetToggled(bracer.Comp.TogglePlasmaCasterAction, false);

                        bracer.Comp.CasterDeployed = false;
                        Dirty(bracer);

                        return;
                    }
                }
                finally
                {
                    _itemSlots.SetLock(holderUid, holderSlot, wasLocked);
                }

                EnsureComp<UnremoveableComponent>(held);
                return;
            }
        }
    }

    private void OnCasterUseInHand(Entity<PlasmaCasterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var newMode = ent.Comp.Mode switch
        {
            PlasmaCasterMode.Stun => PlasmaCasterMode.Immobilizer,
            PlasmaCasterMode.Immobilizer => PlasmaCasterMode.Stun,
            PlasmaCasterMode.Lethal => PlasmaCasterMode.Overcharge,
            PlasmaCasterMode.Overcharge => PlasmaCasterMode.Lethal,
            _ => ent.Comp.Mode,
        };

        ChangeCasterModeLogic(args.User, ent, newMode);
    }

    private void ChangeCasterModeLogic(EntityUid user, Entity<PlasmaCasterComponent> ent, PlasmaCasterMode newMode)
    {
        var bracerUid = GetEntity(ent.Comp.Bracer);
        if (bracerUid.HasValue && TryComp<HunterBracerComponent>(bracerUid.Value, out var bracer))
        {
            if (!AttemptUsage(user, (bracerUid.Value, bracer)))
                return;
        }

        if (_net.IsClient)
            return;

        SetCasterMode(ent, newMode, user);
    }

    private void SetCasterMode(Entity<PlasmaCasterComponent> ent, PlasmaCasterMode newMode, EntityUid user)
    {
        if (ent.Comp.Mode == newMode)
            return;

        ent.Comp.Mode = newMode;
        Dirty(ent);

        if (TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammoProvider))
        {
            ammoProvider.Proto = GetCasterModeData(ent.Comp).proto;
            Dirty(ent, ammoProvider);
        }

        _gunSystem.RefreshModifiers(ent.Owner);

        var locId = $"st-bracer-caster-mode-{ent.Comp.Mode.ToString().ToLower()}";
        _popup.PopupEntity(Loc.GetString("st-bracer-caster-mode-changed", ("mode", Loc.GetString(locId))), user, user);

        var bracerUid = GetEntity(ent.Comp.Bracer);
        if (TryComp<HunterBracerComponent>(bracerUid, out var bracer))
            _audio.PlayPvs(bracer.CasterModeCycleSound, user);
    }

    private (EntProtoId proto, SoundSpecifier? sound) GetCasterModeData(PlasmaCasterComponent caster)
    {
        var protoId = caster.Mode switch
        {
            PlasmaCasterMode.Immobilizer => caster.ImmobilizerAmmo,
            PlasmaCasterMode.Lethal => caster.LethalAmmo,
            PlasmaCasterMode.Overcharge => caster.OverchargeAmmo,
            _ => caster.StunAmmo,
        };

        var sound = caster.Mode switch
        {
            PlasmaCasterMode.Immobilizer => caster.ImmobilizerSound,
            PlasmaCasterMode.Lethal => caster.LethalSound,
            PlasmaCasterMode.Overcharge => caster.OverchargeSound,
            _ => caster.StunSound,
        };

        return (protoId, sound);
    }

    private void OnCasterRefreshModifiers(Entity<PlasmaCasterComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var (_, sound) = GetCasterModeData(ent.Comp);
        args.SoundGunshot = sound;

        args.FireRate = ent.Comp.Mode switch
        {
            PlasmaCasterMode.Immobilizer => ent.Comp.ImmobilizerFireRate,
            PlasmaCasterMode.Lethal => ent.Comp.LethalFireRate,
            PlasmaCasterMode.Overcharge => ent.Comp.OverchargeFireRate,
            _ => ent.Comp.StunFireRate,
        };
    }

    private void OnCasterTakeAmmo(Entity<PlasmaCasterComponent> ent, ref TakeAmmoEvent args)
    {
        var bracerUid = GetEntity(ent.Comp.Bracer);
        if (args.Shots <= 0 || !bracerUid.HasValue || args.User is not { } user)
            return;

        if (
            !TryComp<HunterBracerComponent>(bracerUid.Value, out var bracer)
            || !bracer.CasterShotCost.TryGetValue(ent.Comp.Mode, out var costPerShot)
        )
        {
            args.Reason = Loc.GetString("st-bracer-caster-no-power");
            return;
        }

        if (bracer.Charge < costPerShot)
        {
            args.Reason = Loc.GetString("st-bracer-caster-no-power");
            return;
        }

        var (protoId, _) = GetCasterModeData(ent.Comp);

        var maxShots = int.MaxValue;
        if (costPerShot > 0)
            maxShots = (int)Math.Floor(bracer.Charge / costPerShot);

        var shotsToFire = Math.Min(args.Shots, maxShots);

        if (shotsToFire <= 0)
        {
            args.Reason = Loc.GetString("st-bracer-caster-no-power");
            return;
        }

        var totalCost = shotsToFire * costPerShot;
        if (!TryDrainPower(user, (bracerUid.Value, bracer), totalCost))
        {
            args.Reason = Loc.GetString("st-bracer-caster-no-power");
            return;
        }

        args.Ammo.Clear();

        for (var i = 0; i < shotsToFire; i++)
        {
            var ammo = Spawn(protoId, args.Coordinates);
            args.Ammo.Add((ammo, _gunSystem.EnsureShootable(ammo)));
        }
    }
}
