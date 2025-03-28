using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Placeable;

[Serializable, NetSerializable]
public sealed partial class PlaceFlagDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Coordinates;

    public PlaceFlagDoAfterEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}
