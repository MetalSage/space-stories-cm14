using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[Serializable, NetSerializable]
public sealed partial class VehicleEnterDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class VehicleLeaveDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public enum VehicleVisuals : byte
{
    Destroyed
}

[Serializable, NetSerializable]
public enum VehicleVisualLayers : byte
{
    Base
}