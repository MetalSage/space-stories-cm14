using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Crusher;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STCrusherSplashSystem))]
public sealed partial class STCrusherSplashComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 DamagePercent = FixedPoint2.New(0.5);

    [DataField, AutoNetworkedField]
    public int? MaxTargets = 5;

    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "RMCEffectExtraSlash";
}
