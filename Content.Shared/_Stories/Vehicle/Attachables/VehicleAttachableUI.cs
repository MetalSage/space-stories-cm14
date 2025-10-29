using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Attachables;

[Serializable, NetSerializable]
public sealed class VehicleAttachableHolderStripUserInterfaceState(Dictionary<string, (string?, bool)> attachableSlots)
    : BoundUserInterfaceState
{
    public Dictionary<string, (string?, bool)> AttachableSlots = attachableSlots;
}

[Serializable, NetSerializable]
public sealed class VehicleAttachableHolderChooseSlotUserInterfaceState(List<string> attachableSlots) : BoundUserInterfaceState
{
    public List<string> AttachableSlots = attachableSlots;
}

[Serializable, NetSerializable]
public sealed class VehicleAttachableHolderDetachMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}

[Serializable, NetSerializable]
public sealed class VehicleAttachableHolderAttachToSlotMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}

[Serializable, NetSerializable]
public enum VehicleAttachmentUI : byte
{
    StripKey,
    ChooseSlotKey,
}
