using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Attachables;

[Serializable, NetSerializable]
public sealed class APCAttachableHolderStripUserInterfaceState(Dictionary<string, (string?, bool, string?, string?)> attachableSlots)
    : BoundUserInterfaceState
{
    public Dictionary<string, (string?, bool, string?, string?)> AttachableSlots = attachableSlots;
}

[Serializable, NetSerializable]
public sealed class APCAttachableHolderChooseSlotUserInterfaceState(List<string> attachableSlots) : BoundUserInterfaceState
{
    public List<string> AttachableSlots = attachableSlots;
}

[Serializable, NetSerializable]
public sealed class APCAttachableHolderDetachMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}

[Serializable, NetSerializable]
public sealed class APCAttachableHolderAttachToSlotMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}

[Serializable, NetSerializable]
public enum APCAttachmentUI : byte
{
    StripKey,
    ChooseSlotKey,
}