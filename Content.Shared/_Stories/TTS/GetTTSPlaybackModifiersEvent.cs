namespace Content.Shared._Stories.TTS;

// ReSharper disable once InconsistentNaming
public sealed class GetTTSPlaybackModifiersEvent(float baseRange) : EntityEventArgs
{
    private const float Epsilon = 0.0001f;

    public float BaseRange { get; } = MathF.Max(0f, baseRange);
    public float VolumeMultiplier { get; private set; } = 1f;
    public float RangeMultiplier { get; private set; } = 1f;
    public float? MaxDistance { get; private set; }
    public TTSAudioEffect AudioEffects { get; private set; } = TTSAudioEffect.None;

    public bool HasVolumeOverride => MathF.Abs(VolumeMultiplier - 1f) > Epsilon;
    public bool HasDistanceOverride => MaxDistance != null || MathF.Abs(RangeMultiplier - 1f) > Epsilon;
    public bool HasAudioEffects => AudioEffects != TTSAudioEffect.None;
    public float EffectiveMaxDistance => MathF.Max(0f, MaxDistance ?? BaseRange * RangeMultiplier);

    public void AddVolumeMultiplier(float multiplier)
    {
        VolumeMultiplier *= MathF.Max(0f, multiplier);
    }

    public void AddRangeMultiplier(float multiplier)
    {
        RangeMultiplier *= MathF.Max(0f, multiplier);
    }

    public void SetMaxDistance(float maxDistance)
    {
        maxDistance = MathF.Max(0f, maxDistance);
        MaxDistance = MaxDistance == null ? maxDistance : MathF.Max(MaxDistance.Value, maxDistance);
    }

    public void AddAudioEffects(TTSAudioEffect effects)
    {
        AudioEffects |= effects;
    }
}
