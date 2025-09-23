using Content.Shared.Movement.Systems;
using Content.Shared._Stories.Vehicle;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Attachables;

public sealed partial class AttachableModifiersSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleMovementAttachableComponent, VehicleAttachableAlteredEvent>(OnMovementAttachableAltered);
        SubscribeLocalEvent<VehicleGunAttachableComponent, VehicleAttachableAlteredEvent>(OnHardpointAttachableAltered);
        SubscribeLocalEvent<VehicleHardpointsMenuComponent, BoundUIOpenedEvent>(OnHardpointsUiOpened);
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

    private void OnHardpointAttachableAltered(Entity<VehicleGunAttachableComponent> attachable, ref VehicleAttachableAlteredEvent args)
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
}
