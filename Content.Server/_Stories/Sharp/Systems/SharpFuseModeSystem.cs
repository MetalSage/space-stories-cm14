using Content.Shared._Stories.Sharp;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;

namespace Content.Server._Stories.Sharp;

public sealed class SharpFuseModeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpFuseModeComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<SharpFuseModeComponent, SharpToggleFuseEvent>(OnToggleFuse);
        SubscribeLocalEvent<SharpFuseModeComponent, UniqueActionEvent>(OnUniqueAction);
    }

    private void OnGetItemActions(Entity<SharpFuseModeComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        SyncActionState(ent.Comp);
        Dirty(ent);
    }

    private void OnToggleFuse(EntityUid uid, SharpFuseModeComponent comp, ref SharpToggleFuseEvent args)
    {
        ToggleMode((uid, comp), args.Performer);
        args.Handled = true;
    }

    private void OnUniqueAction(EntityUid uid, SharpFuseModeComponent comp, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        ToggleMode((uid, comp), args.UserUid);
        args.Handled = true;
    }

    private void ToggleMode(Entity<SharpFuseModeComponent> ent, EntityUid user)
    {
        ent.Comp.LongMode = !ent.Comp.LongMode;

        SyncActionState(ent.Comp);
        Dirty(ent);

        var popup = Loc.GetString(ent.Comp.LongMode
            ? "stories-sharp-toggleable-fuse-firing-long"
            : "stories-sharp-toggleable-fuse-firing-short");
        _popup.PopupClient(popup, user, user, PopupType.Large);
    }

    private void SyncActionState(SharpFuseModeComponent comp)
    {
        if (comp.Action is { } action)
            _actions.SetToggled(action, comp.LongMode);
    }
}
