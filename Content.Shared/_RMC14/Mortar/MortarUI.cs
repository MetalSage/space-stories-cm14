using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Mortar;

[Serializable, NetSerializable]
public enum MortarUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MortarTargetBuiMsg(Vector2i target) : BoundUserInterfaceMessage
{
    public Vector2i Target = target;
}

[Serializable, NetSerializable]
public sealed class MortarDialBuiMsg(Vector2i target) : BoundUserInterfaceMessage
{
    public Vector2i Target = target;
}

[Serializable, NetSerializable]
public sealed class MortarViewCamerasMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MortarSetTargetEntityMsg(NetEntity targetEntity, Vector2i coordinates) : BoundUserInterfaceMessage
{
    public NetEntity TargetEntity = targetEntity;
    public Vector2i Coordinates = coordinates;
}

[Serializable, NetSerializable]
public sealed class MortarFlightTimeChangedMsg(TimeSpan flightTime) : BoundUserInterfaceMessage
{
    public TimeSpan FlightTime = flightTime;
}

[Serializable, NetSerializable]
public sealed class MortarState : BoundUserInterfaceState
{
    public List<MortarTargetInfo> Targets { get; }
    public NetEntity? LockedTarget { get; }
    public float? LastFlightTime { get; }

    public MortarState(List<MortarTargetInfo> targets, NetEntity? lockedTarget = null, float? lastFlightTime = null)
    {
        Targets = targets;
        LockedTarget = lockedTarget;
        LastFlightTime = lastFlightTime;
    }
}

[Serializable, NetSerializable]
public sealed record MortarTargetInfo(NetEntity Entity, string Name, NetCoordinates Coords);