using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.HijackSong;

[Serializable, NetSerializable]
public sealed class PlayHijackSongEvent : EntityEventArgs
{
    public readonly string Song;

    public PlayHijackSongEvent(string song)
    {
        Song = song;
    }
}
