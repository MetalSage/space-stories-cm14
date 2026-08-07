using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

// ReSharper disable once InconsistentNaming
[Serializable] [NetSerializable]
public sealed class RequestPreviewTTSEvent(string voiceId, bool isHunter = false) : EntityEventArgs
{
    public string VoiceId { get; } = voiceId;

    public bool IsHunter { get; } = isHunter;
}
