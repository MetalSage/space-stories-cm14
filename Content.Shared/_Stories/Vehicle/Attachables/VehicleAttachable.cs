using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Attachables;

[Serializable, NetSerializable]
public sealed partial class VehicleAttachableAttachDoAfterEvent : SimpleDoAfterEvent
{
    public readonly string SlotId;

    public VehicleAttachableAttachDoAfterEvent(string slotId)
    {
        SlotId = slotId;
    }
}

[Serializable, NetSerializable]
public sealed partial class VehicleAttachableDetachDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public readonly record struct VehicleAttachableAlteredEvent(
    EntityUid Holder,
    VehicleAttachableAlteredType Alteration,
    EntityUid? User = null
);

[ByRefEvent]
public readonly record struct VehicleAttachableHolderAttachablesAlteredEvent(
    EntityUid Attachable,
    string SlotId,
    VehicleAttachableAlteredType Alteration
);

public enum VehicleAttachableAlteredType : byte
{
    Attached = 1 << 0,
    Detached = 1 << 1,
    AppearanceChanged = 1 << 2
}

