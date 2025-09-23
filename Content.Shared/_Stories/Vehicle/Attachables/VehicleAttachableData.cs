using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Attachables;

[DataDefinition, Serializable, NetSerializable]
public partial struct VehicleAttachableSlot()
{
    [DataField]
    public bool Locked;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntProtoId<VehicleAttachableComponent>? StartingAttachable;
}
