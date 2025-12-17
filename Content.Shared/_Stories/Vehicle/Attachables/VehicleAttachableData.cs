using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Attachables;

[DataDefinition, Serializable, NetSerializable]
public partial struct VehicleAttachableSlot()
{
    [DataField]
    public bool Locked;

    [DataField]
    public ProtoId<HardpointTypePrototype>? HardpointType;

    [DataField]
    public List<EntProtoId<VehicleAttachableComponent>>? StartingAttachables = new();

    [DataField]
    public bool MultiModule;

    [DataField]
    public int MaxModules;

    [DataField]
    public bool HiddenInUI;
}
