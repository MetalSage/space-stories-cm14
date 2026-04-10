using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Barricade;

[RegisterComponent, NetworkedComponent]
public sealed partial class StoriesBarricadeDoorDamageVisualsComponent : Component
{
    [DataField(required: true)]
    public string ClosedPrefix = string.Empty;

    [DataField(required: true)]
    public string OpenPrefix = string.Empty;

    [DataField]
    public List<FixedPoint2> Thresholds = new()
    {
        100,
        200,
        300,
    };
}
