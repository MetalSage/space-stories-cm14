using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent]
public sealed partial class APCModuleComponent : Component
{
    [ViewVariables]
    public EntityUid? APC;

    [DataField]
    public Vector2 Offset = new Vector2i.Zero;
}
