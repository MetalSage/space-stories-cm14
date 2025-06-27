using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Utility;
using Content.Shared.Movement.Systems;
using Content.Shared._Stories.APC;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Attachables;

public sealed partial class AttachableModifiersSystem : EntitySystem
{
    [Dependency] private readonly APCAttachableHolderSystem _attachableHolder = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<APCMovementAttachableComponent, APCAttachableAlteredEvent>(OnMovementAttachableAltered);
        SubscribeLocalEvent<APCHardpointAttachableComponent, APCAttachableAlteredEvent>(OnHardpointAttachableAltered);
        SubscribeLocalEvent<APCHardpointsMenuComponent, BoundUIOpenedEvent>(OnHardpointsUiOpened);
    }

    private void OnMovementAttachableAltered(Entity<APCMovementAttachableComponent> attachable, ref APCAttachableAlteredEvent args)
    {
        switch(args.Alteration)
        {
            case APCAttachableAlteredType.AppearanceChanged:
                break;

            default:
                _movement.RefreshMovementSpeedModifiers(args.Holder);
                break;
        }
    }

    private void OnHardpointAttachableAltered(Entity<APCHardpointAttachableComponent> attachable, ref APCAttachableAlteredEvent args)
    {
        if (!TryComp<APCEntityComponent>(args.Holder, out var apc))
            return;

        switch(args.Alteration)
        {
            case APCAttachableAlteredType.AppearanceChanged:
                break;

            case APCAttachableAlteredType.Attached:
                apc.Hardpoints.Add(attachable.Owner);
                Dirty(args.Holder, apc);
                break;

            case APCAttachableAlteredType.Detached:
                apc.Hardpoints.Remove(attachable.Owner);
                Dirty(args.Holder, apc);
                break;
        }
        
        UpdateHardpointUi(apc.Owner);
    }

    private void OnHardpointsUiOpened(EntityUid uid,
        APCHardpointsMenuComponent component,
        BoundUIOpenedEvent args)
    {
        UpdateHardpointUi(uid);
    }

    private void UpdateHardpointUi(EntityUid uid, APCHardpointsMenuComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new APCHardpointWindowUserInterfaceState();
        _ui.SetUiState(uid, APCSelectHardpointUI.Key, state);
    }
}
