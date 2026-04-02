using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Boiler;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoilerAcidAnimationComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2.Zero;
}
