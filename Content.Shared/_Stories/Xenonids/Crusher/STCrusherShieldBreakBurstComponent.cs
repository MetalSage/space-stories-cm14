using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Crusher;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STCrusherShieldSystem))]
public sealed partial class STCrusherShieldBreakBurstComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.5f;
}
