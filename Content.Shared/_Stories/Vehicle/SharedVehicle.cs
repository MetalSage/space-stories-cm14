using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[Serializable, NetSerializable]
public sealed partial class VehicleEnterDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VehicleLeaveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MotionDetectorScanDoAfterEvent : SimpleDoAfterEvent;


[Serializable, NetSerializable]
public enum VehicleVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public enum VehicleWeaponLoaderUI : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class VehicleWeaponLoaderSelectHardpointMsg : BoundUserInterfaceMessage
{
    public NetEntity Hardpoint;

    public VehicleWeaponLoaderSelectHardpointMsg(NetEntity hardpoint)
    {
        Hardpoint = hardpoint;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleWeaponLoaderWindowState : BoundUserInterfaceState
{
    public List<NetEntity> Hardpoints;
    public NetEntity? SelectedHardpoint;

    public VehicleWeaponLoaderWindowState(List<NetEntity> hardpoints, NetEntity? selectedHardpoint)
    {
        Hardpoints = hardpoints;
        SelectedHardpoint = selectedHardpoint;
    }
}

public sealed partial class VehicleLockDoorsEvent : InstantActionEvent;
