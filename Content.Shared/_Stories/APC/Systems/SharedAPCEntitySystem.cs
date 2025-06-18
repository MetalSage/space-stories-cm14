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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<APCEntityComponent, BreakageEventArgs>(OnDestruction);
        SubscribeLocalEvent<APCEntityComponent, EntInsertedIntoContainerMessage>(OnModuleAttached);

        InitializeController();
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

    private void OnModuleAttached(Entity<APCEntityComponent> apc, ref EntInsertedIntoContainerMessage args)
    {
        var module = args.Entity;

        if (TryComp<MovementSpeedModifierComponent>(module, out var moduleMovement) &&
            TryComp<MovementSpeedModifierComponent>(apc, out var apcMovement))
        {
            var totalWalk = apcMovement.BaseWalkSpeed + moduleMovement.BaseWalkSpeed;
            var totalSprint = apcMovement.BaseSprintSpeed + moduleMovement.BaseSprintSpeed;
            var totalAcceleration = apcMovement.Acceleration + moduleMovement.Acceleration;

            _movement.ChangeBaseSpeed(apc, totalWalk, totalSprint, totalAcceleration, apcMovement);
        }

        if (TryComp<GunComponent>(module, out var gun))
        {
            Logger.Info($"Found guncomp on module {module.Value} but gun logic is unavailable");
        }

        if (!TryComp(module, out APCModuleComponent? moduleComponent))
            return;

        var holderEv = new APCModuleAlteredEvent(module, APCModulesAlteredType.Attached);
        RaiseLocalEvent(apc, ref holderEv);
    }
}
