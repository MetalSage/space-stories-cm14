using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Reagents;

[DataDefinition]
[Serializable, NetSerializable]
public partial struct ExplosiveData
{
    [DataField]
    public float Power = 0f;

    [DataField]
    public float FalloffModifier = 0f;

    [DataField]
    public float ShrapnelModifier = 1f;
}
