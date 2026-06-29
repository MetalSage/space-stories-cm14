using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Sharp;

/// <summary>
/// Per-level payload used by SHARP mines when they detonate.
/// Index 0 is level 1, index 1 is level 2, etc.
/// </summary>
[RegisterComponent]
public sealed partial class SharpMineLevelsComponent : Component
{
    [DataField]
    public ProtoId<ExplosionPrototype>? ExplosionType;

    [DataField]
    public float ExplosionTileBreakScale = 1f;

    [DataField]
    public int ExplosionMaxTileBreak = int.MaxValue;

    [DataField]
    public bool ExplosionCanCreateVacuum = true;

    [DataField]
    public SoundSpecifier? TileFireSound;

    [DataField(required: true)]
    public List<SharpMineLevel> Levels = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SharpMineLevel
{
    [DataField]
    public float? ExplosiveMaxIntensity;

    [DataField]
    public float? ExplosiveIntensitySlope;

    [DataField]
    public float? ExplosiveTotalIntensity;

    [DataField]
    public EntProtoId? TileFireSpawn;

    [DataField]
    public int? TileFireRange;

    [DataField]
    public int? TileFireIntensity;

    [DataField]
    public int? TileFireDuration;
}
