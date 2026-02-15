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

    public PlayTTSEvent(byte[] data, NetEntity? sourceUid = null, bool isWhisper = false, NetEntity? originalSourceUid = null)
    {
        Data = data;
        SourceUid = sourceUid;
        IsWhisper = isWhisper;
        OriginalSourceUid = originalSourceUid ?? sourceUid;
    }
}
