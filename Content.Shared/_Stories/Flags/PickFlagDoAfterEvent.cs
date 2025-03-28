using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Placeable;

[Serializable, NetSerializable]
public sealed partial class PickFlagDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Coordinates;

    public PickFlagDoAfterEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}
