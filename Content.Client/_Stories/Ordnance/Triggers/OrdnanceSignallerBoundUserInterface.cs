using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Triggers;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using System;

namespace Content.Client._Stories.Ordnance.Triggers;

[UsedImplicitly]
public sealed class OrdnanceSignallerBoundUserInterface : BoundUserInterface
{
    private OrdnanceSignallerWindow? _window;

    public OrdnanceSignallerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new OrdnanceSignallerWindow();
        _window.OpenCentered();
        _window.OnClose += Close;

        _window.CodeSpinBox.InitDefaultButtons();

        _window.FreqSlider.OnValueChanged += args => { _window.FreqSpinBox.Value = args.Value; };
        _window.FreqSlider.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick) SendUpdateMessage();
        };
        _window.FreqSpinBox.OnValueChanged += args =>
        {
            _window.FreqSlider.SetValueWithoutEvent(args.Value);
            SendUpdateMessage();
        };

        _window.CodeSlider.OnValueChanged += args => { _window.CodeSpinBox.Value = (int)args.Value; };
        _window.CodeSlider.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick) SendUpdateMessage();
        };
        _window.CodeSpinBox.ValueChanged += args =>
        {
            _window.CodeSlider.SetValueWithoutEvent(args.Value);
            SendUpdateMessage();
        };

        _window.TriggerButton.OnPressed += _ => SendMessage(new OrdnanceSignallerTriggerMessage());

        Refresh();
    }

    public void Refresh()
    {
        if (_window == null) return;

        OrdnanceSignallerComponent? sig = null;

        if (EntMan.TryGetComponent(Owner, out OrdnanceSignallerComponent? directSig))
        {
            sig = directSig;
        }
        else if (EntMan.TryGetComponent(Owner, out OrdnanceAssemblyHolderComponent? holder))
        {
            if (holder.Part1 != null && EntMan.TryGetComponent(holder.Part1.Value, out OrdnanceSignallerComponent? s1))
                sig = s1;
            else if (holder.Part2 != null && EntMan.TryGetComponent(holder.Part2.Value, out OrdnanceSignallerComponent? s2))
                sig = s2;
        }

        if (sig == null) return;

        _window.FreqSlider.SetValueWithoutEvent(sig.Frequency);
        _window.FreqSpinBox.Value = sig.Frequency;

        _window.CodeSlider.SetValueWithoutEvent(sig.Code);
        _window.CodeSpinBox.Value = sig.Code;
    }

    private void SendUpdateMessage()
    {
        if (_window == null) return;
        SendMessage(new OrdnanceSignallerUpdateMessage(_window.FreqSlider.Value, (int)_window.CodeSlider.Value));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
