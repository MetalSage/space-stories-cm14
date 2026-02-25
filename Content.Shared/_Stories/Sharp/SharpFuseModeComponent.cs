using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpFuseModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool LongMode = false;

    [DataField]
    public EntityUid? Action;
}
