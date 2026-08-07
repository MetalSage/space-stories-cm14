using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent]
public sealed partial class SharpFlechetteSpreadComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ProjectileProto = default!;

    [DataField]
    public EntProtoId? SourceProjectileProto;

    [DataField]
    public float ProjectileDamageMultiplier = 1f;

    [DataField]
    public int Count = 6;

    [DataField]
    public float SpreadAngle = 60f;

    [DataField]
    public bool EvenSpread = true;

    [DataField]
    public float ProjectileSpeed = 20f;

    [DataField]
    public float MinVelocity = 0.5f;

    [DataField]
    public float MaxVelocity = 1.5f;

    public bool Fired;
}
