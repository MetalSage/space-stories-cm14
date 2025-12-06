using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Trigger; // Stories-Moved-To-Shared

[RegisterComponent, NetworkedComponent]
public sealed partial class OnShootTriggerAmmoTimerComponent : Component
{
    [DataField]
    public float Delay;

    [DataField]
    public float BeepInterval;

    [DataField]
    public float? InitialBeepDelay;

    [DataField]
    public SoundSpecifier? BeepSound;

    [DataField]
    public Enum TimerStart = TimerStartMode.OnShoot;
}

public enum TimerStartMode : byte
{
    OnShoot,
    OnHitGround
};
