using Content.Shared._Stories.HijackSong;
using Content.Shared._Stories.SCCVars;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Stories.HijackSong;

public sealed class PlayHijackSongSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    private float _volume = 0.0f;
    private EntityUid? _songEntity = null;

    public override void Initialize()
    {
        SubscribeNetworkEvent<PlayHijackSongEvent>(PlayHijackSong);
        _cfg.OnValueChanged(SCCVars.HijackVolume, OnVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(SCCVars.HijackVolume, OnVolumeChanged);
    }

    private void OnVolumeChanged(float volume)
    {
        _volume = volume;

        if (_songEntity.HasValue)
        {
            var volumeAdjusted = SharedAudioSystem.GainToVolume(_volume);
            _audio.SetVolume(_songEntity.Value, volumeAdjusted);
        }
    }

    private void PlayHijackSong(PlayHijackSongEvent ev)
    {
        var volume = SharedAudioSystem.GainToVolume(_volume);
        var audioParams = AudioParams.Default.WithVolume(volume);

        _songEntity = _audio.PlayGlobal(ev.Song, Filter.Local(), false, audioParams)?.Entity;
    }
}
