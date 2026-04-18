using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Barricade;

[RegisterComponent, NetworkedComponent]
public sealed partial class StoriesBarricadeDoorDamageVisualsComponent : Component
{
    [DataField(required: true)]
    public string ClosedDamageSprite = string.Empty;

    [DataField(required: true)]
    public string OpenDamageSprite = string.Empty;

    [DataField]
    public StoriesBarricadeDoorDamageVisualLayers Layer = StoriesBarricadeDoorDamageVisualLayers.Damage;

    [DataField(required: true)]
    public List<FixedPoint2> Thresholds = new();

    [DataField(required: true)]
    public List<string> States = new();
}

[Serializable, NetSerializable]
public enum StoriesBarricadeDoorDamageVisuals
{
    Damage,
}

[Serializable, NetSerializable]
public enum StoriesBarricadeDoorDamageVisualLayers
{
    Damage,
}
