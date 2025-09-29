using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Attachables;

public sealed partial class AttachableModifiersSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly VehicleAttachableHolderSystem _holder = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleMovementAttachableComponent, VehicleAttachableAlteredEvent>(OnMovementAttachableAltered);
        SubscribeLocalEvent<VehicleAttachableComponent, VehicleAttachableAlteredEvent>(OnHardpointAttachableAltered);
        SubscribeLocalEvent<VehicleHardpointsMenuComponent, BoundUIOpenedEvent>(OnHardpointsUiOpened);
        SubscribeLocalEvent<VehicleAttachableComponent, DamageModifyEvent>(AttachableDamageModify);
        SubscribeLocalEvent<VehicleAttachableComponent, DamageChangedEvent>(OnAttachableDamaged);
    }

    private void OnMovementAttachableAltered(Entity<VehicleMovementAttachableComponent> attachable, ref VehicleAttachableAlteredEvent args)
    {
        switch (args.Alteration)
        {
            case VehicleAttachableAlteredType.AppearanceChanged:
                break;

            default:
                _movement.RefreshMovementSpeedModifiers(args.Holder);
                break;
        }
    }

    private void OnHardpointAttachableAltered(Entity<VehicleAttachableComponent> attachable, ref VehicleAttachableAlteredEvent args)
    {
        if (!TryComp<VehicleComponent>(args.Holder, out var vehicle))
            return;

        switch (args.Alteration)
        {
            case VehicleAttachableAlteredType.AppearanceChanged:
                break;

            case VehicleAttachableAlteredType.Attached:
                vehicle.Hardpoints.Add(attachable.Owner);
                Dirty(args.Holder, vehicle);
                break;

            case VehicleAttachableAlteredType.Detached:
                vehicle.Hardpoints.Remove(attachable.Owner);
                Dirty(args.Holder, vehicle);
                break;
        }

        UpdateHardpointUi(args.Holder);
    }

    private void OnHardpointsUiOpened(EntityUid uid,
        VehicleHardpointsMenuComponent component,
        BoundUIOpenedEvent args)
    {
        UpdateHardpointUi(uid);
    }

    private void UpdateHardpointUi(EntityUid uid, VehicleHardpointsMenuComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new VehicleHardpointWindowUserInterfaceState();
        _ui.SetUiState(uid, VehicleSelectHardpointUI.Key, state);
    }

    private void AttachableDamageModify(Entity<VehicleAttachableComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage = args.Damage * ent.Comp.DamageMult;

        if (TryComp<DamageableComponent>(ent, out var damageable))
        {
            var maxHealth = ent.Comp.MaxHealth;
            var currentDamage = damageable.TotalDamage;
            var incomingDamage = args.Damage.GetTotal();

            if (currentDamage >= maxHealth)
            {
                args.Damage *= 0f;
            }
            else if (currentDamage + incomingDamage > maxHealth)
            {
                var allowedDamage = maxHealth - currentDamage;
                var factor = allowedDamage / incomingDamage;

                var clampedDamage = new DamageSpecifier();
                foreach (var kv in args.Damage.DamageDict)
                    clampedDamage.DamageDict[kv.Key] = kv.Value * factor;

                args.Damage = clampedDamage;
            }
        }
    }

    private void OnAttachableDamaged(Entity<VehicleAttachableComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Damageable.TotalDamage >= ent.Comp.MaxHealth)
        {
            ent.Comp.Destroyed = true;
            Dirty(ent);

            if (!_holder.TryGetHolder(ent.Owner, out var holder) || holder is null)
            {
                var msg = Loc.GetString("st-destroyed-vehicle-attachable-deleted", ("attachable", ent.Owner));
                _popup.PopupEntity(msg, ent, PopupType.Small);

                QueueDel(ent);
            }

            if (holder is not null && TryComp<VehicleComponent>(holder.Value, out var vehicle))
            {
                vehicle.Hardpoints.Remove(ent.Owner);
                Dirty(holder.Value, vehicle);
                _movement.RefreshMovementSpeedModifiers(holder.Value);
            }
        }
    }
}
