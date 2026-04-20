using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Shared._Stories.Xenonids.AcidAnimation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoAcidAnimationComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> ActionIds = new();

    [DataField, AutoNetworkedField]
    public ResPath? SpitRsi;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField]
    public float ToggleRateLimit = 0.25f;

    public TimeSpan NextToggleAt;
}
