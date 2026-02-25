using Robust.Shared.Map;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent]
public sealed partial class SharpStickyDartStopPointComponent : Component
{
    [DataField]
    public MapCoordinates Coordinates;
}
