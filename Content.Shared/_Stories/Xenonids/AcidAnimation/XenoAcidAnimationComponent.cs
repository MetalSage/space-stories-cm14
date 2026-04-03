using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Xenonids.AcidAnimation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoAcidAnimationComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField(required: true)]
    public ResPath SpitRsi = default!;

    [DataField]
    public Vector2 Offset = Vector2.Zero;

    [DataField]
    public bool HideNorth = true;
}
