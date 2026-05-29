using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Triggers;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using System;

namespace Content.Client._Stories.Ordnance.Triggers;

[UsedImplicitly]
public sealed class OrdnanceProxSensorBoundUserInterface : BoundUserInterface
{
    private OrdnanceProxSensorWindow? _window;

    public OrdnanceProxSensorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new OrdnanceProxSensorWindow();
        _window.OpenCentered();
        _window.OnClose += Close;

        _window.ArmSpinBox.InitDefaultButtons();
        _window.RangeSpinBox.InitDefaultButtons();
        _window.DelaySpinBox.InitDefaultButtons();

        _window.ArmSlider.OnValueChanged += args => { _window.ArmSpinBox.Value = (int)args.Value; };
        _window.ArmSlider.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick) SendUpdate();
        };
        _window.ArmSpinBox.ValueChanged += args =>
        {
            _window.ArmSlider.SetValueWithoutEvent(args.Value);
            SendUpdate();
        };

        _window.RangeSlider.OnValueChanged += args => { _window.RangeSpinBox.Value = (int)args.Value; };
        _window.RangeSlider.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick) SendUpdate();
        };
        _window.RangeSpinBox.ValueChanged += args =>
        {
            _window.RangeSlider.SetValueWithoutEvent(args.Value);
            SendUpdate();
        };

        _window.DelaySlider.OnValueChanged += args => { _window.DelaySpinBox.Value = (int)args.Value; };
        _window.DelaySlider.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick) SendUpdate();
        };
        _window.DelaySpinBox.ValueChanged += args =>
        {
            _window.DelaySlider.SetValueWithoutEvent(args.Value);
            SendUpdate();
        };

        Refresh();
    }

    private void SendUpdate()
    {
        if (_window == null) return;
        SendMessage(new OrdnanceProxSensorConfigMessage((int)_window.ArmSlider.Value, (int)_window.RangeSlider.Value, (int)_window.DelaySlider.Value));
    }

    public void Refresh()
    {
        if (_window == null) return;

        OrdnanceProxSensorComponent? prox = null;

        if (EntMan.TryGetComponent(Owner, out OrdnanceProxSensorComponent? directProx))
        {
            prox = directProx;
        }
        else if (EntMan.TryGetComponent(Owner, out OrdnanceAssemblyHolderComponent? holder))
        {
            if (holder.Part1 != null && EntMan.TryGetComponent(holder.Part1.Value, out OrdnanceProxSensorComponent? p1))
                prox = p1;
            else if (holder.Part2 != null && EntMan.TryGetComponent(holder.Part2.Value, out OrdnanceProxSensorComponent? p2))
                prox = p2;
        }

        if (prox == null) return;

        _window.ArmSlider.SetValueWithoutEvent(prox.ArmingTime);
        _window.ArmSpinBox.Value = (int)prox.ArmingTime;

        _window.RangeSlider.SetValueWithoutEvent(prox.Range);
        _window.RangeSpinBox.Value = (int)prox.Range;

        _window.DelaySlider.SetValueWithoutEvent(prox.Delay);
        _window.DelaySpinBox.Value = (int)prox.Delay;

        if (prox.Armed)
        {
            _window.StatusPanel.ModulateSelfOverride = Color.FromHex("#b22222");
            _window.StatusLabel.Text = Loc.GetString("stories-ordnance-status-armed");
        }
        else if (prox.Enabled && !prox.Armed)
        {
            _window.StatusPanel.ModulateSelfOverride = Color.FromHex("#daa520");
            _window.StatusLabel.Text = Loc.GetString("stories-ordnance-status-arming", ("time", (int)prox.ArmingTimeRemaining));
        }
        else
        {
            _window.StatusPanel.ModulateSelfOverride = Color.FromHex("#2e8b57");
            _window.StatusLabel.Text = Loc.GetString("stories-ordnance-status-not-arming");
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
