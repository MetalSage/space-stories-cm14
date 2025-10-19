using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TransformableItem;

[Serializable, NetSerializable]
public enum TransformableItemUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class TransformableItemBuiMsg(EntProtoId transformationId) : BoundUserInterfaceMessage
{
    public readonly EntProtoId TransformationId = transformationId;
}
