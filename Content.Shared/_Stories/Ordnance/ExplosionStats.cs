using Robust.Shared.Maths;

namespace Content.Shared._Stories.Ordnance;

public record struct ExplosionStats
{
    public float Power;
    public float Falloff;
    public int Shards;
    public float FireIntensity;
    public float FireDuration;
    public float FireRadius;
    public string FireEntity;
    public bool FirePenetrating;
    public Color? FireColor;
}

public record struct EngineExplosionParams(float TotalIntensity, float Slope, float MaxIntensity, float Radius);
