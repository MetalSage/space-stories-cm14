using System.Linq;
using Content.Shared._Stories.Vehicle;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
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
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleMovementAttachableComponent, VehicleAttachableAlteredEvent>(OnMovementAttachableAltered);
        SubscribeLocalEvent<VehicleAttachableComponent, VehicleAttachableAlteredEvent>(OnHardpointAttachableAltered);
        SubscribeLocalEvent<VehicleComponent, BoundUIOpenedEvent>(OnHardpointsUiOpened);
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
        if (_net.IsClient)
            return;

        if (!TryComp<VehicleComponent>(args.Holder, out var vehicle))
            return;

        switch (args.Alteration)
        {
            case VehicleAttachableAlteredType.AppearanceChanged:
                break;

            case VehicleAttachableAlteredType.Attached:
                vehicle.Hardpoints.Add(attachable.Owner);
                Dirty(args.Holder, vehicle);
                UpdateVehicleStatusUI((args.Holder, vehicle));
                if (HasComp<VehicleControllableComponent>(attachable.Owner))
                    HandleControllerSeats(vehicle, args.Holder);
                break;

            case VehicleAttachableAlteredType.Detached:
                vehicle.Hardpoints.Remove(attachable.Owner);
                Dirty(args.Holder, vehicle);
                UpdateVehicleStatusUI((args.Holder, vehicle));
                break;
        }

        UpdateHardpointUi(args.Holder);
    }

    private void OnHardpointsUiOpened(EntityUid uid,
        VehicleComponent component,
        BoundUIOpenedEvent args)
    {
        UpdateHardpointUi(uid);
    }

    private void HandleControllerSeats(VehicleComponent vehicle, EntityUid grid)
    {
        var controllersQuery = EntityQueryEnumerator<VehicleControllerComponent, TransformComponent>();
        while (controllersQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            comp.Vehicle = grid;

            foreach (var hardpoint in vehicle.Hardpoints)
            {
                if (!TryComp<VehicleControllableComponent>(hardpoint, out var controllable))
                    continue;

                if (controllable.Id == comp.Id)
                {
                    comp.ControllableEntity = hardpoint;
                    Dirty(uid, comp);
                    break;
                }
            }

            Dirty(uid, comp);
        }
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

                FixedPoint2 accumulatedDamage = FixedPoint2.Zero;
                var damageList = args.Damage.DamageDict.ToList();

                for (int i = 0; i < damageList.Count - 1; i++)
                {
                    var scaled = damageList[i].Value * factor;
                    clampedDamage.DamageDict[damageList[i].Key] = scaled;
                    accumulatedDamage += scaled;
                }

                if (damageList.Count > 0)
                {
                    var lastKey = damageList[damageList.Count - 1].Key;
                    clampedDamage.DamageDict[lastKey] = allowedDamage - accumulatedDamage;
                }

                args.Damage = clampedDamage;
            }
        }
    }

    private void OnAttachableDamaged(Entity<VehicleAttachableComponent> ent, ref DamageChangedEvent args)
    {
        if (_holder.TryGetHolder(ent.Owner, out var holder) &&
            holder is not null &&
            TryComp<VehicleComponent>(holder.Value, out var vehicle))
        {
            UpdateVehicleStatusUI((holder.Value, vehicle));
        }

        if (args.Damageable.TotalDamage >= ent.Comp.MaxHealth)
        {
            ent.Comp.Destroyed = true;
            Dirty(ent);

            if (!_holder.TryGetHolder(ent.Owner, out holder) || holder is null)
            {
                var msg = Loc.GetString("st-destroyed-vehicle-attachable-deleted", ("attachable", ent.Owner));
                _popup.PopupPredicted(msg, msg, ent, ent, PopupType.Small);

                QueueDel(ent);
            }

            if (holder is not null && TryComp<VehicleComponent>(holder.Value, out vehicle))
            {
                vehicle.Hardpoints.Remove(ent.Owner);
                Dirty(holder.Value, vehicle);
                _movement.RefreshMovementSpeedModifiers(holder.Value);
                UpdateVehicleStatusUI((holder.Value, vehicle));
            }
        }
    }

    private void UpdateVehicleStatusUI(Entity<VehicleComponent> vehicle)
    {
        var state = new VehicleStatusUIState();
        _ui.SetUiState(vehicle.Owner, VehicleStatusUI.Key, state);
    }

    private void UpdateHardpointUi(EntityUid uid, VehicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new VehicleHardpointWindowUserInterfaceState(GetNetEntity(component.ActiveHardpoint));
        _ui.SetUiState(uid, VehicleSelectHardpointUI.Key, state);
    }
}
