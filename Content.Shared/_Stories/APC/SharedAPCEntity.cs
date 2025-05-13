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
public sealed partial class AttachModuleToAPCDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public enum APCEnterSide
{
    Left,
    Right
}
public sealed partial class APCControlReturnActionEvent : InstantActionEvent
{
}

public sealed class ReturnToBodyAPCEvent : EntityEventArgs
{
    public EntityUid APCController;

    public ReturnToBodyAPCEvent(EntityUid apccontroller)
    {
        APCController = apccontroller;
    }
}

public sealed class GettingAPCControlledEvent : EntityEventArgs
{
    public EntityUid User;
    public EntityUid Controller;
    public GettingAPCControlledEvent(EntityUid user, EntityUid controller)
    {
        User = user;
        Controller = controller;
    }
}

[Serializable, NetSerializable]
public enum APCVisuals : byte
{
    Destroyed
}

[Serializable, NetSerializable]
public enum APCVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public sealed class RequestControlAPCEvent : EntityEventArgs
{
    public NetEntity APCController;
    public NetEntity User;

    public RequestControlAPCEvent(NetEntity apcController, NetEntity user)
    {
        APCController = apcController;
        User = user;
    }
}

[Serializable, NetSerializable]
public record DeattachModuleEvent(NetEntity Attacher, NetEntity Module);

[ByRefEvent, Serializable, NetSerializable]
public record struct APCModuleAttachedEvent(NetEntity APC, NetEntity Module);