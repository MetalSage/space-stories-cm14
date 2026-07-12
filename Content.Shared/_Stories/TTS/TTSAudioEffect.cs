using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

// ReSharper disable once InconsistentNaming
[Flags]
[Serializable, NetSerializable]
public enum TTSAudioEffect : byte
{
    None = 0,
    Megaphone = 1 << 0,
    StandardRadio = 1 << 1,
    XenoHivemind = 1 << 2,
    Hunter = 1 << 3,
    Ares = 1 << 4,
}
