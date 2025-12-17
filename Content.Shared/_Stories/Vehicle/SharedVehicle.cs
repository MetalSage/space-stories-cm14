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
public enum VehicleSelectHardpointUI : byte
{
    Key
}

[Serializable, NetSerializable]
public enum VehicleStatusUI : byte
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
    public List<HardpointInfo> Hardpoints = new();
}

[Serializable, NetSerializable]
public sealed class HardpointInfo
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public bool HasActiveMagazine;
    public int SpareCount;
    public int MaxSpares;
    public int CurrentAmmo;
    public int MaxAmmo;
}


[Serializable, NetSerializable]
public sealed class VehicleSelectHardpointBuiMsg(NetEntity choice) : BoundUserInterfaceMessage
{
    public readonly NetEntity Choice = choice;
}

[Serializable, NetSerializable]
public sealed class VehicleHardpointWindowUserInterfaceState(NetEntity? activeHardpoint) : BoundUserInterfaceState
{
    public readonly NetEntity? ActiveHardpoint = activeHardpoint;
}

[Serializable, NetSerializable]
public sealed class VehicleStatusUIState(bool doorState) : BoundUserInterfaceState
{
    public readonly bool DoorState = doorState;
    public List<HardpointInfo> Hardpoints = new();
}

public sealed partial class VehicleLockDoorsEvent : InstantActionEvent;

public sealed partial class VehicleStatusMenuEvent : InstantActionEvent;
