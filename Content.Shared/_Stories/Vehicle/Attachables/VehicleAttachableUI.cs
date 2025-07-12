using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Attachables;

[Serializable, NetSerializable]
public sealed class VehicleAttachableHolderStripUserInterfaceState(Dictionary<string, (string?, bool, string?, string?)> attachableSlots)
    : BoundUserInterfaceState
{
    public Dictionary<string, (string?, bool, string?, string?)> AttachableSlots = attachableSlots;
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

[Serializable, NetSerializable]
public enum VehicleSelectHardpointUI : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class VehicleSelectHardpointBuiMsg(NetEntity choice) : BoundUserInterfaceMessage
{
    public readonly NetEntity Choice = choice;
}

[Serializable, NetSerializable]
public sealed class VehicleHardpointWindowUserInterfaceState() : BoundUserInterfaceState;