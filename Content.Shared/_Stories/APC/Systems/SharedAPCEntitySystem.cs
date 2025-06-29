using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Destructible;
using Content.Shared.Coordinates;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Content.Shared.Popups;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Network;
using Robust.Shared.Containers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._Stories.Attachables;
using Content.Shared.Interaction;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly APCAttachableHolderSystem _attachableHolder = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<APCEntityComponent, BreakageEventArgs>(OnDestruction);
        SubscribeLocalEvent<APCEntityComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<APCGunnerComponent, APCHardpointsMenuActionEvent>(OnAPCHardpointsMenuAction);

        Subs.BuiEvents<APCEntityComponent>(APCSelectHardpointUI.Key,
            subs =>
            {
                subs.Event<APCSelectHardpointBuiMsg>(OnSelectHardpoint);
            });

        InitializeController();
    }

    private void OnRefreshMovementSpeedModifiers(Entity<APCEntityComponent> apc, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<APCAttachableHolderComponent>(apc, out var holderComp) ||
            !holderComp.Slots.ContainsKey(apc.Comp.MovementSlot))
        {
            args.ModifySpeed(0f, 0f);
            return;
        }

        var holder = (apc.Owner, holderComp);

        if (_attachableHolder.TryGetAttachable(holder, apc.Comp.MovementSlot, out var attachable) &&
            TryComp<APCMovementAttachableComponent>(attachable, out var attachableMovement))
        {
            args.ModifySpeed(attachableMovement.WalkSpeed, attachableMovement.SprintSpeed);
        }
        else
        {
            args.ModifySpeed(0f, 0f);
        }
    }

    private void OnAPCHardpointsMenuAction(Entity<APCGunnerComponent> gunner, ref APCHardpointsMenuActionEvent args)
    {
        if (gunner.Comp.APC is not { } apc)
            return;

        _ui.OpenUi(apc, APCSelectHardpointUI.Key, gunner);
    }

    private void OnSelectHardpoint(Entity<APCEntityComponent> apc, ref APCSelectHardpointBuiMsg args)
    {
        apc.Comp.ActiveHardpoint = GetEntity(args.Choice);
        Dirty(apc, apc.Comp);
    }

    private void OnDestruction(EntityUid uid, APCEntityComponent component, BreakageEventArgs args)
    {
        DestroyAPC(uid, component);
    }

    public void DestroyAPC(EntityUid uid, APCEntityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Destroyed = true;
        UpdateAppearance(uid, component);
    }

    public void UpdateAppearance(EntityUid uid, APCEntityComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        _appearance.SetData(uid, APCVisuals.Destroyed, component.Destroyed, appearance);
    }
}
