using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[Serializable, NetSerializable]
public enum VehicleAttachableVisualLayers : byte
{
    Base, 
    Destroyed
}

public sealed partial class VehicleHardpointsMenuActionEvent : InstantActionEvent;

[ByRefEvent]
public record struct VehicleGunReloadEvent(EntityUid Equipment);
