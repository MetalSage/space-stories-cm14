using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Preferences;
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

public sealed partial class APCHardpointsMenuActionEvent : InstantActionEvent;
