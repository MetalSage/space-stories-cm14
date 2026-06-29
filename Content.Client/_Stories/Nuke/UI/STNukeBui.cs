using Content.Shared._Stories.Nuke;
using Robust.Shared.Localization;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stories.Nuke.UI;

public sealed class STNukeBui : BoundUserInterface
{
    [ViewVariables]
    private STNukeWindow? _window;

    public STNukeBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<STNukeWindow>();

        _window.ToggleNukeButton.OnPressed += _ => SendMessage(new STNukeToggleMessage());
        _window.ToggleSafetyButton.OnPressed += _ => SendMessage(new STNukeToggleSafetyMessage());
        _window.ToggleCommandLockoutButton.OnPressed += _ => SendMessage(new STNukeToggleCommandLockoutMessage());
        _window.ToggleAnchorButton.OnPressed += _ => SendMessage(new STNukeToggleAnchorMessage());
        _window.ToggleEncryptionButton.OnPressed += _ => SendMessage(new STNukeToggleEncryptionMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState rawState)
    {
        base.UpdateState(rawState);

        if (rawState is not STNukeBuiState state)
            return;

        UpdateNukeWindow(state);
    }

    private void UpdateNukeWindow(STNukeBuiState state)
    {
        if (_window == null)
            return;

        var cantNuke = !state.Anchor || state.Safety || !state.DecryptionComplete;
        var cantDecrypt = !state.Anchor || state.DecryptionComplete || state.Safety;
        var cantDisengage = !state.Anchor || !state.CanDisengage;

        _window.DecryptionNoticeLabel.Text = Loc.GetString(
            "st-nuke-ui-decryption-status",
            ("state",
                state.DecryptionComplete ? "ready" :
                state.Decrypting ? "progress" :
                "required"),
            ("time", state.DecryptionTime));

        _window.TimingNoticeLabel.Text = Loc.GetString(
            "st-nuke-ui-timing-status",
            ("active", state.Timing),
            ("time", state.TimeLeft));

        SetToggleText(
            _window.ToggleSafetyButton,
            "st-nuke-ui-safety-toggle",
            state.Safety);

        SetToggleText(
            _window.ToggleCommandLockoutButton,
            "st-nuke-ui-command-lockout-toggle",
            state.CommandLockout);

        SetToggleText(
            _window.ToggleAnchorButton,
            "st-nuke-ui-anchor-toggle",
            state.Anchor);

        SetToggleText(
            _window.ToggleEncryptionButton,
            "st-nuke-ui-decryption-toggle",
            state.Decrypting,
            disabled: !state.Decrypting && cantDecrypt);

        SetToggleText(
            _window.ToggleNukeButton,
            "st-nuke-ui-nuke-toggle",
            state.Timing,
            disabled: state.Timing ? cantDisengage : cantNuke);
    }

    private static void SetToggleText(Button button, string locKey, bool state, bool disabled = false)
    {
        button.Text = Loc.GetString(locKey, ("enabled", state));
        button.Disabled = disabled;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
