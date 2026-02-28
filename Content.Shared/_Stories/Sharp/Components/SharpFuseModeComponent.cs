using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpFuseModeComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ActionId = default!;

    [DataField, AutoNetworkedField]
    public bool LongMode = false;

    [DataField]
    public EntityUid? Action;
}
