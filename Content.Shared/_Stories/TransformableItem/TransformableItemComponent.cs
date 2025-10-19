using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.TransformableItem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransformableItemComponent : Component
{
    [DataField(required: true)]
    [AutoNetworkedField]
    public List<EntProtoId> Transformations = new();
}
