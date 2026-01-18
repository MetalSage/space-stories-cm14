using System.Linq;
using Content.Server._RMC14.Marines;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Dialog;
using Content.Shared._Stories.Hunter.Marking;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Overseer;
using Content.Shared.Actions;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Server._Stories.Hunter.Marking;

public sealed class HunterMarkingSystem : SharedHunterMarkingSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly OverseerSystem _overseer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterComponent, OpenMarkPanelEvent>(OnOpenMarkPanel);
        SubscribeLocalEvent<HunterComponent, RequestMarkForHuntEvent>(OnRequestMarkForHunt);
        SubscribeLocalEvent<HunterComponent, MarkActionChosenEvent>(OnMarkActionChosen);
        SubscribeLocalEvent<HunterComponent, MarkTargetChosenEvent>(OnMarkTargetChosen);
        SubscribeLocalEvent<HunterComponent, MarkReasonSubmittedEvent>(OnMarkReasonSubmitted);
        SubscribeLocalEvent<HunterComponent, ConfirmUnmarkPreyEvent>(OnConfirmUnmarkPrey);
    }

    private void OnOpenMarkPanel(Entity<HunterComponent> hunter, ref OpenMarkPanelEvent args)
    {
        if (args.Handled)
            return;

        var options = new List<DialogOption>();

        if (hunter.Comp.Prey == null)
        {
            options.Add(
                new DialogOption(Loc.GetString("st-hunter-action-mark-prey"),
                    new MarkActionChosenEvent(MarkAction.MarkPrey))
            );
        }
        else
        {
            options.Add(
                new DialogOption(Loc.GetString("st-hunter-action-unmark-prey"),
                    new MarkActionChosenEvent(MarkAction.UnmarkPrey))
            );
        }

        options.Add(
            new DialogOption(Loc.GetString("st-hunter-action-mark-honored"),
                new MarkActionChosenEvent(MarkAction.MarkHonored))
        );
        options.Add(
            new DialogOption(Loc.GetString("st-hunter-action-unmark-honored"),
                new MarkActionChosenEvent(MarkAction.UnmarkHonored))
        );
        options.Add(
            new DialogOption(Loc.GetString("st-hunter-action-mark-dishonored"),
                new MarkActionChosenEvent(MarkAction.MarkDishonored))
        );
        options.Add(
            new DialogOption(
                Loc.GetString("st-hunter-action-unmark-dishonored"),
                new MarkActionChosenEvent(MarkAction.UnmarkDishonored)
            )
        );

        options.Add(
            new DialogOption(
                Loc.GetString("st-hunter-action-mark-gear-carrier"),
                new MarkActionChosenEvent(MarkAction.MarkGearCarrier)
            )
        );
        options.Add(
            new DialogOption(
                Loc.GetString("st-hunter-action-unmark-gear-carrier"),
                new MarkActionChosenEvent(MarkAction.UnmarkGearCarrier)
            )
        );

        if (hunter.Comp.Thrall == null)
        {
            options.Add(
                new DialogOption(Loc.GetString("st-hunter-action-mark-thralled"),
                    new MarkActionChosenEvent(MarkAction.MarkThralled))
            );
        }
        else
        {
            options.Add(
                new DialogOption(
                    Loc.GetString("st-hunter-action-unmark-thralled"),
                    new MarkActionChosenEvent(MarkAction.UnmarkThralled)
                )
            );
        }

        options.Add(
            new DialogOption(Loc.GetString("st-hunter-action-mark-blooded"),
                new MarkActionChosenEvent(MarkAction.MarkBlooded))
        );

        _dialog.OpenOptions(
            args.Performer,
            Loc.GetString("st-hunter-mark-panel-title"),
            options,
            Loc.GetString("st-hunter-mark-panel-prompt")
        );
        args.Handled = true;
    }

    private void OnRequestMarkForHunt(Entity<HunterComponent> hunter, ref RequestMarkForHuntEvent args)
    {
        if (args.Handled)
            return;

        if (hunter.Comp.Prey != null)
        {
            var options = new List<DialogOption>
            {
                new(Loc.GetString("st-hunter-confirm-yes"), new ConfirmUnmarkPreyEvent()),
                new(Loc.GetString("st-hunter-confirm-no"), new HunterMenuCancelEvent()),
            };
            _dialog.OpenOptions(
                hunter.Owner,
                Loc.GetString("st-hunter-unmark-prey-title"),
                options,
                Loc.GetString("st-hunter-unmark-prey-prompt")
            );
        }
        else
            OpenTargetSelection(hunter.Owner, MarkAction.MarkPrey, Loc.GetString("st-hunter-choose-prey-title"));

        args.Handled = true;
    }

    private void OnConfirmUnmarkPrey(Entity<HunterComponent> hunter, ref ConfirmUnmarkPreyEvent args)
    {
        if (hunter.Comp.Prey is not { } preyNet)
            return;

        if (!TryGetEntity(preyNet, out var preyUid))
            return;

        ProcessMark(hunter.Owner, preyUid.Value, MarkAction.UnmarkPrey);
    }

    private void OnMarkActionChosen(Entity<HunterComponent> hunter, ref MarkActionChosenEvent args)
    {
        if (args.Action == MarkAction.UnmarkPrey)
        {
            if (hunter.Comp.Prey is not { } preyNet || !TryGetEntity(preyNet, out var preyUid))
                return;
            ProcessMark(hunter.Owner, preyUid.Value, args.Action);
            return;
        }

        if (args.Action == MarkAction.UnmarkThralled)
        {
            if (hunter.Comp.Thrall is not { } thrallNet || !TryGetEntity(thrallNet, out var thrallUid))
                return;
            ProcessMark(hunter.Owner, thrallUid.Value, args.Action);
            return;
        }

        OpenTargetSelection(hunter.Owner, args.Action, Loc.GetString("st-hunter-choose-target-title"));
    }

    private void OnMarkTargetChosen(Entity<HunterComponent> hunter, ref MarkTargetChosenEvent args)
    {
        var performer = hunter.Owner;

        if (!TryGetEntity(args.Target, out var targetUid) || targetUid is not EntityUid target || !Exists(target))
            return;

        switch (args.Action)
        {
            case MarkAction.MarkHonored:
            case MarkAction.MarkDishonored:
            case MarkAction.MarkThralled:
            case MarkAction.MarkBlooded:
                var actionReasonKey = args.Action switch
                {
                    MarkAction.MarkHonored => "st-hunter-reason-type-honored",
                    MarkAction.MarkDishonored => "st-hunter-reason-type-dishonored",
                    MarkAction.MarkThralled => "st-hunter-reason-type-thralled",
                    MarkAction.MarkBlooded => "st-hunter-reason-type-blooded",
                    _ => args.Action.ToString().Replace("Mark", ""),
                };

                var localizedAction = Loc.HasString(actionReasonKey) ? Loc.GetString(actionReasonKey) : actionReasonKey;

                var reasonTitle = Loc.GetString("st-hunter-reason-title", ("action", localizedAction));

                _dialog.OpenInput(
                    performer,
                    reasonTitle,
                    new MarkReasonSubmittedEvent("", args.Action, args.Target),
                    characterLimit: 120
                );
                break;
            default:
                ProcessMark(performer, target, args.Action);
                break;
        }
    }

    private void OnMarkReasonSubmitted(Entity<HunterComponent> hunter, ref MarkReasonSubmittedEvent args)
    {
        var performer = hunter.Owner;

        if (!TryGetEntity(args.Target, out var targetUid) || targetUid is not EntityUid target || !Exists(target))
            return;

        if (string.IsNullOrWhiteSpace(args.Message))
        {
            _popup.PopupEntity(Loc.GetString("st-hunter-reason-required"), performer, performer);
            return;
        }

        ProcessMark(performer, target, args.Action, args.Message);
    }

    private void OpenTargetSelection(EntityUid performer, MarkAction action, string title)
    {
        var range = 7f;
        var targetsInRange = _lookup
            .GetEntitiesInRange(performer, range)
            .Where(e => HasComp<MobStateComponent>(e) && e != performer)
            .ToList();
        if (targetsInRange.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("st-hunter-no-targets-in-range"), performer, performer);
            return;
        }

        var options = targetsInRange
            .Select(target => new DialogOption(Name(target), new MarkTargetChosenEvent(action, GetNetEntity(target))))
            .ToList();
        _dialog.OpenOptions(performer, title, options);
    }

    private void ProcessMark(EntityUid performer, EntityUid target, MarkAction action, string? reason = null)
    {
        if (!TryComp<HunterComponent>(performer, out var hunterComp))
            return;

        var markedComp = EnsureComp<HunterMarkedComponent>(target);

        string GetLocalizedMarkType(HunterMarkType type)
        {
            var key = $"st-hunter-mark-type-{type.ToString().ToLowerInvariant()}";
            if (Loc.HasString(key))
                return Loc.GetString(key);

            return type.ToString();
        }

        void ShowPopup(string message)
        {
            _popup.PopupEntity(message, performer, performer);
        }

        void ApplyMark(HunterMarkType type)
        {
            if ((markedComp.Marks & type) != 0)
            {
                ShowPopup(Loc.GetString("st-hunter-already-marked", ("target", Name(target))));
                return;
            }

            markedComp.Hunter ??= GetNetEntity(performer);
            markedComp.Marks |= type;
            var message = Loc.GetString(
                "st-hunter-log-marked",
                ("hunter", Name(performer)),
                ("target", Name(target)),
                ("type", GetLocalizedMarkType(type))
            );
            AnnounceToOverseers(message);
            ShowPopup(message);
        }

        void ApplyMarkWithReason(HunterMarkType type, ref string? reasonField)
        {
            if ((markedComp.Marks & type) != 0)
            {
                ShowPopup(Loc.GetString("st-hunter-already-marked", ("target", Name(target))));
                return;
            }

            if (reason is null)
                return;
            markedComp.Hunter ??= GetNetEntity(performer);
            markedComp.Marks |= type;
            reasonField = reason;
            var message = Loc.GetString(
                "st-hunter-log-marked-with-reason",
                ("hunter", Name(performer)),
                ("target", Name(target)),
                ("type", GetLocalizedMarkType(type)),
                ("reason", reason)
            );
            AnnounceToOverseers(message);
            ShowPopup(message);
        }

        bool CanUnmark()
        {
            if (!TryGetEntity(markedComp.Hunter, out var hunterUid))
                return true;

            if (hunterUid != performer)
            {
                ShowPopup(Loc.GetString("st-hunter-cannot-undo-another"));
                return false;
            }

            return true;
        }

        void RemoveMark(HunterMarkType type)
        {
            if ((markedComp.Marks & type) == 0)
            {
                ShowPopup(Loc.GetString("st-hunter-not-marked", ("target", Name(target))));
                return;
            }

            if (!CanUnmark())
                return;

            markedComp.Marks &= ~type;
            var message = Loc.GetString(
                "st-hunter-log-unmarked",
                ("hunter", Name(performer)),
                ("target", Name(target)),
                ("type", GetLocalizedMarkType(type))
            );
            AnnounceToOverseers(message);
            ShowPopup(message);
        }

        void RemoveMarkWithReason(HunterMarkType type, ref string? reasonField)
        {
            if ((markedComp.Marks & type) == 0)
            {
                ShowPopup(Loc.GetString("st-hunter-not-marked", ("target", Name(target))));
                return;
            }

            if (!CanUnmark())
                return;

            markedComp.Marks &= ~type;
            reasonField = null;
            var message = Loc.GetString(
                "st-hunter-log-unmarked",
                ("hunter", Name(performer)),
                ("target", Name(target)),
                ("type", GetLocalizedMarkType(type))
            );
            AnnounceToOverseers(message);
            ShowPopup(message);
        }

        switch (action)
        {
            case MarkAction.MarkPrey:
                if (hunterComp.Prey != null)
                {
                    ShowPopup(Loc.GetString("st-hunter-already-hunting"));
                    return;
                }

                RaiseLocalEvent(performer, new UpdateHunterMarkEvent(UpdateMarkType.Prey, GetNetEntity(target)));
                ApplyMark(HunterMarkType.Prey);
                break;
            case MarkAction.UnmarkPrey:
                if (!TryGetEntity(hunterComp.Prey, out var preyUid) || preyUid != target)
                {
                    ShowPopup(Loc.GetString("st-hunter-not-your-prey"));
                    return;
                }

                RaiseLocalEvent(performer, new UpdateHunterMarkEvent(UpdateMarkType.Prey, null));
                RemoveMark(HunterMarkType.Prey);
                break;
            case MarkAction.MarkHonored:
                ApplyMarkWithReason(HunterMarkType.Honored, ref markedComp.HonoredReason);
                break;
            case MarkAction.UnmarkHonored:
                RemoveMarkWithReason(HunterMarkType.Honored, ref markedComp.HonoredReason);
                break;
            case MarkAction.MarkDishonored:
                ApplyMarkWithReason(HunterMarkType.Dishonored, ref markedComp.DishonoredReason);
                break;
            case MarkAction.UnmarkDishonored:
                RemoveMarkWithReason(HunterMarkType.Dishonored, ref markedComp.DishonoredReason);
                break;
            case MarkAction.MarkGearCarrier:
                ApplyMark(HunterMarkType.GearCarrier);
                break;
            case MarkAction.UnmarkGearCarrier:
                RemoveMark(HunterMarkType.GearCarrier);
                break;
            case MarkAction.MarkThralled:
                if (hunterComp.Thrall != null)
                {
                    ShowPopup(Loc.GetString("st-hunter-already-has-thrall"));
                    return;
                }

                RaiseLocalEvent(performer, new UpdateHunterMarkEvent(UpdateMarkType.Thrall, GetNetEntity(target)));
                ApplyMarkWithReason(HunterMarkType.Thralled, ref markedComp.ThralledReason);
                break;
            case MarkAction.UnmarkThralled:
                if (!TryGetEntity(hunterComp.Thrall, out var thrallUid) || thrallUid != target)
                {
                    ShowPopup(Loc.GetString("st-hunter-not-your-thrall"));
                    return;
                }

                RaiseLocalEvent(performer, new UpdateHunterMarkEvent(UpdateMarkType.Thrall, null));
                RemoveMarkWithReason(HunterMarkType.Thralled, ref markedComp.ThralledReason);
                break;
            case MarkAction.MarkBlooded:
                ApplyMarkWithReason(HunterMarkType.Blooded, ref markedComp.BloodedReason);
                break;
        }

        if (markedComp.Marks == HunterMarkType.None)
            RemComp<HunterMarkedComponent>(target);
        else
            Dirty(target, markedComp);
    }

    private void AnnounceToOverseers(string message)
    {
        var overseer = _overseer.EnsureOverseer();
        _marineAnnounce.AnnounceRadio(overseer, message, "STOverseer");
    }
}
