using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Triggers;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using System;

namespace Content.Client._Stories.Ordnance.Triggers;

[UsedImplicitly]
public sealed class OrdnanceTimerBoundUserInterface : BoundUserInterface
{
    private OrdnanceTimerWindow? _window;

    public OrdnanceTimerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new OrdnanceTimerWindow();
        _window.OpenCentered();
        _window.OnClose += Close;

        _window.TimeSpinBox.InitDefaultButtons();

        _window.TimeSlider.OnValueChanged += args =>
        {
            _window.TimeSpinBox.Value = (int)args.Value;
        };

        _window.TimeSlider.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
                SendMessage(new OrdnanceTimerSetMessage((int)_window.TimeSlider.Value));
        };

        _window.TimeSpinBox.ValueChanged += args =>
        {
            _window.TimeSlider.SetValueWithoutEvent(args.Value);
            SendMessage(new OrdnanceTimerSetMessage(args.Value));
        };

        Refresh();
    }

    public void Refresh()
    {
        if (_window == null) return;

        OrdnanceTimerComponent? timer = null;

        if (EntMan.TryGetComponent(Owner, out OrdnanceTimerComponent? directTimer))
        {
            timer = directTimer;
        }
        else if (EntMan.TryGetComponent(Owner, out OrdnanceAssemblyHolderComponent? holder))
        {
            if (holder.Part1 != null && EntMan.TryGetComponent(holder.Part1.Value, out OrdnanceTimerComponent? t1))
                timer = t1;
            else if (holder.Part2 != null && EntMan.TryGetComponent(holder.Part2.Value, out OrdnanceTimerComponent? t2))
                timer = t2;
        }

        if (timer == null) return;

        _window.TimeSlider.SetValueWithoutEvent(timer.SelectedTime);
        _window.TimeSpinBox.Value = (int)timer.SelectedTime;

        if (timer.Enabled)
        {
            _window.StatusPanel.ModulateSelfOverride = Color.FromHex("#2e8b57");
            _window.StatusLabel.Text = Loc.GetString("stories-ordnance-status-active");
        }
        else
        {
            _window.StatusPanel.ModulateSelfOverride = Color.FromHex("#b22222");
            _window.StatusLabel.Text = Loc.GetString("stories-ordnance-status-disabled");
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
