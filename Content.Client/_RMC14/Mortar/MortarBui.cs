using Content.Shared._RMC14.Mortar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Shared._RMC14.Rangefinder.Spotting;
using Content.Shared._RMC14.Dropship.Weapon;
using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Mortar;

[UsedImplicitly]
public sealed class MortarBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MortarWindow? _window;
    private NetEntity? _lastSelectedTarget;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MortarWindow>();

        Refresh();
        UpdateTargetsList([]);

        static int Parse(FloatSpinBox spinBox)
        {
            return (int) spinBox.Value;
        }

        static void SetSpinBox(FloatSpinBox spinBox, int limit, int value)
        {
            spinBox.Value = value;
            spinBox.OnValueChanged += args =>
            {
                var value = Math.Clamp(args.Value, -limit, limit);
                spinBox.Value = value;
            };
        }

        if (EntMan.TryGetComponent(Owner, out MortarComponent? mortar))
        {
            SetSpinBox(_window.TargetX, mortar.MaxTarget, mortar.Target.X);
            SetSpinBox(_window.TargetY, mortar.MaxTarget, mortar.Target.Y);
            SetSpinBox(_window.DialX, mortar.MaxDial, mortar.Dial.X);
            SetSpinBox(_window.DialY, mortar.MaxDial, mortar.Dial.Y);
            _window.SetTargetButton.OnPressed += _ =>
                SendPredictedMessage(new MortarTargetBuiMsg((Parse(_window.TargetX), Parse(_window.TargetY))));

            _window.SetOffsetButton.OnPressed += _ =>
                SendPredictedMessage(new MortarDialBuiMsg((Parse(_window.DialX), Parse(_window.DialY))));
        }

        _window.ViewCameraButton.OnPressed += _ => SendPredictedMessage(new MortarViewCamerasMsg());

        _window.FlightTime.OnValueChanged += args =>
        {
            var clamped = Math.Clamp(args.Value, 3, 10);
            _window.FlightTime.Value = clamped;
            var flightTime = TimeSpan.FromSeconds(clamped);
            SendPredictedMessage(new MortarFlightTimeChangedMsg(flightTime));
        };
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out MortarComponent? mortar))
            return;

        static void SetValue(FloatSpinBox? spinBox, int value)
        {
            if (spinBox != null)
                spinBox.Value = value;
        }

        SetValue(_window.TargetX, mortar.Target.X);
        SetValue(_window.TargetY, mortar.Target.Y);
        SetValue(_window.DialX, mortar.Dial.X);
        SetValue(_window.DialY, mortar.Dial.Y);
        _window.MaxDialLabel.Text = Loc.GetString("rmc-mortar-offset-max", ("max", mortar.MaxDial));
    }

    private void UpdateTargetsList(List<MortarTargetInfo> targets)
    {
        if (_window is not { IsOpen: true })
            return;

        _window.TargetsList.RemoveAllChildren();

        foreach (var target in targets)
        {
            var targetCoords = new Vector2i((int)target.Coords.X, (int)target.Coords.Y);
            var isSelected = _lastSelectedTarget == target.Entity;
            var button = new Button
            {
                Text = $"{target.Name} ({targetCoords.X}, {targetCoords.Y})",
                HorizontalExpand = true,
                Margin = new Thickness(2, 1),
                Modulate = isSelected ? new Color(0.4f, 0.7f, 1f) : Color.White
            };

            button.OnPressed += _ =>
            {
                _lastSelectedTarget = target.Entity;
                SendPredictedMessage(new MortarSetTargetEntityMsg(target.Entity, targetCoords));
                UpdateTargetsList(targets);
            };

            _window.TargetsList.AddChild(button);
        }

        if (_window.TargetsCountLabel != null)
            _window.TargetsCountLabel.Text = $"({targets.Count})";

        if (targets.Count > 0)
        {
            _window.TargetsPanel.Visible = true;
        }
        else
        {
            _window.TargetsPanel.Visible = false;
            _window.TargetsList.AddChild(new Label
            {
                Text = Loc.GetString("rmc-mortar-no-targets"),
                HorizontalExpand = true,
                Margin = new Thickness(2, 1)
            });
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is not { IsOpen: true })
            return;

        if (state is not MortarState mortarState)
            return;

        _lastSelectedTarget = mortarState.LockedTarget;

        if (mortarState.LastFlightTime is float lastTime)
        {
            var clamped = Math.Clamp(lastTime, 3, 10);
            if (Math.Abs(_window.FlightTime.Value - clamped) > 0.01f)
                _window.FlightTime.Value = clamped;
        }

        UpdateTargetsList(mortarState.Targets);
    }
}
