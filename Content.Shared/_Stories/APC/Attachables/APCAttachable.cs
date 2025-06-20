using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared._Stories.Attachables;

[Serializable, NetSerializable]
public sealed partial class APCAttachableAttachDoAfterEvent : SimpleDoAfterEvent
{
    public readonly string SlotId;

    public APCAttachableAttachDoAfterEvent(string slotId)
    {
        SlotId = slotId;
    }
}

[Serializable, NetSerializable]
public sealed partial class APCAttachableDetachDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public readonly record struct APCAttachableAlteredEvent(
    EntityUid Holder,
    APCAttachableAlteredType Alteration,
    EntityUid? User = null
);

[ByRefEvent]
public readonly record struct APCAttachableHolderAttachablesAlteredEvent(
    EntityUid Attachable,
    string SlotId,
    APCAttachableAlteredType Alteration
);

public enum APCAttachableAlteredType : byte
{
    Attached = 1 << 0,
    Detached = 1 << 1,
    AppearanceChanged = 1 << 2
}

