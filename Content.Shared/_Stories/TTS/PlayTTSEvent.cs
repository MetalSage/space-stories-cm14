using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

[Serializable] [NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class PlayTTSEvent : EntityEventArgs
{
    public byte[] Data { get; }
    public NetEntity? SourceUid { get; }
    public bool IsWhisper { get; }
    public NetEntity? OriginalSourceUid { get; }
    public float VolumeMultiplier { get; }
    public float? MaxDistanceOverride { get; }

    public PlayTTSEvent(
        byte[] data,
        NetEntity? sourceUid = null,
        bool isWhisper = false,
        NetEntity? originalSourceUid = null,
        float volumeMultiplier = 1f,
        float? maxDistanceOverride = null)
    {
        Data = data;
        SourceUid = sourceUid;
        IsWhisper = isWhisper;
        OriginalSourceUid = originalSourceUid ?? sourceUid;
        VolumeMultiplier = volumeMultiplier;
        MaxDistanceOverride = maxDistanceOverride;
    }
}
