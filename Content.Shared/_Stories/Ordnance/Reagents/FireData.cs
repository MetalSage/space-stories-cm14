using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Reagents;

[DataDefinition]
[Serializable, NetSerializable]
public partial struct FireData
{
    [DataField]
    public bool IsFuel = false;

    [DataField]
    public float Intensity = 0f;

    [DataField]
    public float Duration = 0f;

    [DataField]
    public float Radius = 0f;

    [DataField]
    public float IntensityMod = 0f;

    [DataField]
    public float DurationMod = 0f;

    [DataField]
    public float RadiusMod = 0f;

    [DataField]
    public Color? BurnColor;

    [DataField]
    public string? BurnSprite;

    [DataField]
    public bool FirePenetrating = false;

    [DataField]
    public string FireEntity = "RMCTileFire";
}
