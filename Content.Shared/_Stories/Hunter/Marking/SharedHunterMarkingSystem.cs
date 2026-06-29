using Content.Shared._RMC14.Dialog;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Marking;

public abstract class SharedHunterMarkingSystem : EntitySystem
{
    [Dependency]
    private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterComponent, HunterDiedEvent>(OnHunterDied);
    }

    private void OnHunterDied(Entity<HunterComponent> ent, ref HunterDiedEvent args)
    {
        if (args.Prey.HasValue)
        {
            var preyUid = GetEntity(args.Prey.Value);
            if (TryComp<HunterMarkedComponent>(preyUid, out var markedPrey))
            {
                markedPrey.Hunter = null;
                markedPrey.Marks &= ~HunterMarkType.Prey;
                Dirty(preyUid, markedPrey);
            }
        }

        if (args.Thrall.HasValue)
        {
            var thrallUid = GetEntity(args.Thrall.Value);
            if (TryComp<HunterMarkedComponent>(thrallUid, out var markedThrall))
            {
                markedThrall.Hunter = null;
                markedThrall.Marks &= ~HunterMarkType.Thralled;
                Dirty(thrallUid, markedThrall);
            }

            _popup.PopupEntity(Loc.GetString("st-hunter-thrall-master-died"), thrallUid, thrallUid);
        }
    }
}

public sealed partial class OpenMarkPanelEvent : InstantActionEvent
{
}

public sealed partial class RequestMarkForHuntEvent : InstantActionEvent
{
}

[Serializable] [NetSerializable]
public sealed class ConfirmUnmarkPreyEvent;

[Serializable] [NetSerializable]
public sealed class HunterMenuCancelEvent;

[Serializable] [NetSerializable]
public abstract class MarkActionEvent
{
    protected MarkActionEvent(MarkAction action)
    {
        Action = action;
    }

    public MarkAction Action { get; }
}

[Serializable] [NetSerializable]
public sealed class MarkActionChosenEvent : MarkActionEvent
{
    public MarkActionChosenEvent(MarkAction action)
        : base(action)
    {
    }
}

[Serializable] [NetSerializable]
public sealed class MarkTargetChosenEvent : MarkActionEvent
{
    public MarkTargetChosenEvent(MarkAction action, NetEntity target)
        : base(action)
    {
        Target = target;
    }

    public NetEntity Target { get; }
}

[Serializable] [NetSerializable]
public sealed record MarkReasonSubmittedEvent(string Message, MarkAction Action, NetEntity Target)
    : DialogInputEvent(Message);

[Serializable] [NetSerializable]
public sealed class HunterDiedEvent : EntityEventArgs
{
    public readonly NetEntity? Prey;
    public readonly NetEntity? Thrall;

    public HunterDiedEvent(NetEntity? prey, NetEntity? thrall)
    {
        Prey = prey;
        Thrall = thrall;
    }
}

[Serializable] [NetSerializable]
public sealed class UpdateHunterMarkEvent : EntityEventArgs
{
    public readonly NetEntity? Target;
    public readonly UpdateMarkType Type;

    public UpdateHunterMarkEvent(UpdateMarkType type, NetEntity? target)
    {
        Type = type;
        Target = target;
    }
}

[Serializable] [NetSerializable]
public enum UpdateMarkType
{
    Prey,
    Thrall,
}

[Serializable] [NetSerializable]
public enum MarkAction
{
    MarkPrey,
    UnmarkPrey,
    MarkHonored,
    UnmarkHonored,
    MarkDishonored,
    UnmarkDishonored,
    MarkGearCarrier,
    UnmarkGearCarrier,
    MarkThralled,
    UnmarkThralled,
    MarkBlooded,
}
