using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.APC;

[Serializable, NetSerializable]
public sealed partial class EnterAPCDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class LeaveAPCDoAfterEvent : SimpleDoAfterEvent
{
}


[Serializable, NetSerializable]
public enum APCVisuals : byte
{
    Destroyed
}

[Serializable, NetSerializable]
public enum APCEntityVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public enum APCAttachableVisualLayers : byte
{
    Base, 
    Destroyed
}

public sealed partial class APCHardpointsMenuActionEvent : InstantActionEvent;

[ByRefEvent]
public record struct APCGunReloadEvent(EntityUid Equipment);
