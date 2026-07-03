using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class PlayTTSEvent : EntityEventArgs
{
    public byte[] Data { get; }
    public string Text { get; }
    public NetEntity? SourceUid { get; }
    public bool IsWhisper { get; }
    public NetEntity? OriginalSourceUid { get; }
    public bool IsRadio { get; }
    public string? RadioChannel { get; }
    public bool IsAnnounce { get; }

    public PlayTTSEvent(byte[] data, string text, NetEntity? sourceUid = null, bool isWhisper = false, NetEntity? originalSourceUid = null, bool isRadio = false, string? radioChannel = null, bool isAnnounce = false)
    {
        Data = data;
        Text = text;
        SourceUid = sourceUid;
        IsWhisper = isWhisper;
        OriginalSourceUid = originalSourceUid ?? sourceUid;
        IsRadio = isRadio;
        RadioChannel = radioChannel;
        IsAnnounce = isAnnounce;
    }
}
