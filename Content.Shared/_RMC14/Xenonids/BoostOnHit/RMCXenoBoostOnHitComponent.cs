using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.BoostOnHit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCXenoBoostOnHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public TimeSpan DurationTime;

    [DataField, AutoNetworkedField]
    public bool Cooldown;

    [DataField, AutoNetworkedField]
    public TimeSpan CooldownTime;

    [DataField, AutoNetworkedField]
    public float Amount = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan CooldownDuration = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public Color AuraColor = Color.Green;

    [DataField, AutoNetworkedField]
    public LocId BoostMessage = "rmc-xeno-boost-on-hit-boost";

    [DataField, AutoNetworkedField]
    public LocId RechargeMessage = "rmc-xeno-boost-on-hit-recharge";
}
