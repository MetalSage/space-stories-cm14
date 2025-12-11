using Content.Shared._Stories.Nuke;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Nuke;

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
        var cantDecrypt = !state.Anchor || state.DecryptionComplete;
        var cantDisengage = !state.Anchor || !state.CanDisengage;

        if (state.DecryptionComplete)
        {
            _window.DecryptionNoticeLabel.Text = "Decryption complete.";
        }
        else
        {
            _window.DecryptionNoticeLabel.Text = $"Decryption time left:\n{state.DecryptionTime} minutes";
        }

        if (state.Timing)
        {
            _window.TimingNoticeLabel.Text = $"Time until detonation:\n{state.TimeLeft}";
            _window.TimingNoticeLabel.StyleClasses.Add("NukeDanger");
        }
        else
        {
            _window.TimingNoticeLabel.Text = "Not currently active.";
            _window.TimingNoticeLabel.StyleClasses.Remove("NukeDanger");
        }

        UpdateToggleButton(
            _window.ToggleSafetyButton,
            state.Safety,
            "Enable safety",
            "Disable safety",
            "ButtonColorCaution");

        UpdateToggleButton(
            _window.ToggleCommandLockoutButton,
            state.CommandLockout,
            "Enable command lockout",
            "Disable command lockout",
            "ButtonColorCaution");

        UpdateToggleButton(
            _window.ToggleAnchorButton,
            state.Anchor,
            "Activate anchor",
            "Deactivate anchor",
            "ButtonColorCaution");

        if (state.Decrypting)
        {
            _window.ToggleEncryptionButton.Text = "Stop decryption";
            _window.ToggleEncryptionButton.Disabled = false;
            _window.ToggleEncryptionButton.StyleClasses.Clear();
            _window.ToggleEncryptionButton.StyleClasses.Add("ButtonColorCaution");
        }
        else
        {
            _window.ToggleEncryptionButton.Text = "Start decryption";
            _window.ToggleEncryptionButton.Disabled = cantDecrypt;
            _window.ToggleEncryptionButton.StyleClasses.Clear();
            _window.ToggleEncryptionButton.StyleClasses.Add("ButtonColorGood");
        }

        if (state.Timing)
        {
            _window.ToggleNukeButton.Text = "Deactivate nuke";
            _window.ToggleNukeButton.Disabled = cantDisengage;
            _window.ToggleNukeButton.StyleClasses.Clear();
            _window.ToggleNukeButton.StyleClasses.Add("ButtonColorCaution");
        }
        else
        {
            _window.ToggleNukeButton.Text = "Activate nuke";
            _window.ToggleNukeButton.Disabled = cantNuke;
            _window.ToggleNukeButton.StyleClasses.Clear();
            _window.ToggleNukeButton.StyleClasses.Add("ButtonColorDanger");
        }
        
        _window.AccessDeniedOverlay.Visible = !state.Allowed;
        _window.ProcessingOverlay.Visible = state.Allowed && state.BeingUsed;
    }

    private static void UpdateToggleButton(Button button, bool isToggled, string offText, string onText, string cautionStyleClass)
    {
        if (isToggled)
        {
            button.Text = onText;
            button.StyleClasses.Clear();
            button.StyleClasses.Add(cautionStyleClass);
        }
        else
        {
            button.Text = offText;
            button.StyleClasses.Clear();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
