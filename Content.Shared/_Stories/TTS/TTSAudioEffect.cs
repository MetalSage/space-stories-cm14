using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public enum TTSAudioEffect : byte
{
    None = 0,
    StandardRadio = 1,
    Megaphone = 2,
    Ares = 3,
    XenoHivemind = 4,
    Hunter = 5,
}
