using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[Serializable, NetSerializable]
public enum VehicleAttachableVisualLayers : byte
{
    Base
}

public sealed partial class VehicleSelectHardpointEvent : InstantActionEvent;

public sealed partial class VehicleReloadSpecialGunEvent : InstantActionEvent;
