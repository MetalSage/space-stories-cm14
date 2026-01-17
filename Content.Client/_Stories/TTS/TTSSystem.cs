using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Stories.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private ISawmill _sawmill = default!;
    private MemoryContentRoot? _contentRoot;
    private static readonly ResPath Prefix = ResPath.Root / "TTS";

    private const float WhisperFade = 4f;
    public const int VoiceRange = 10;
    public const int WhisperClearRange = 2;
    public const int WhisperMuffledRange = 5;

    private const float MinimalVolume = -10f;

    private int _fileIdx = 0;
    private readonly HashSet<NetEntity> _mutedPlayers = new();

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");

        if (_contentRoot == null)
        {
            _contentRoot = new MemoryContentRoot();
            _res.AddRoot(Prefix, _contentRoot);
        }

        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public void RequestPreviewTTS(string voiceId, bool isHunter = false)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId, isHunter));
    }

    public void Mute(NetEntity netEntity)
    {
        _mutedPlayers.Add(netEntity);
    }

    public void Unmute(NetEntity netEntity)
    {
        _mutedPlayers.Remove(netEntity);
    }

    public bool IsMuted(NetEntity netEntity)
    {
        return _mutedPlayers.Contains(netEntity);
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        if (!_cfg.GetCVar(SCCVars.TTSEnabledClient))
            return;

        if (ev.OriginalSourceUid.HasValue && IsMuted(ev.OriginalSourceUid.Value))
            return;

        if (_contentRoot == null)
        {
            _sawmill.Error("TTS content root is not initialized, skipping playback.");
            return;
        }

        var name = "Unknown";
        if (ev.OriginalSourceUid.HasValue && TryGetEntity(ev.OriginalSourceUid.Value, out var sourceEnt))
        {
            name = MetaData(sourceEnt.Value).EntityName;
        }
        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {name} entity");

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        var audioResource = new AudioResource();
        audioResource.Load(IoCManager.Instance!, Prefix / filePath);

        float volumeCVar;
        if (ev.SourceUid == null)
        {
            volumeCVar = _cfg.GetCVar(SCCVars.TTSVolumeRadio);
        }
        else if (TryGetEntity(ev.SourceUid.Value, out var source) && source.HasValue)
        {
            if (HasComp<MarineComponent>(source.Value))
                volumeCVar = _cfg.GetCVar(SCCVars.TTSVolumeMarines);
            else if (HasComp<XenoComponent>(source.Value))
                volumeCVar = _cfg.GetCVar(SCCVars.TTSVolumeXenos);
            else
                volumeCVar = _cfg.GetCVar(SCCVars.TTSVolumeOther);
        }
        else
        {
            volumeCVar = _cfg.GetCVar(SCCVars.TTSVolumeOther);
        }

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper, volumeCVar))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        if (ev.SourceUid != null && TryGetEntity(ev.SourceUid.Value, out var sourceUid))
        {
            _audio.PlayEntity(audioResource.AudioStream, sourceUid.Value, new ResolvedPathSpecifier(filePath), audioParams);
        }
        else
        {
            _audio.PlayGlobal(audioResource.AudioStream, new ResolvedPathSpecifier(filePath), audioParams);
        }

        _contentRoot.RemoveFile(filePath);
    }

    private float AdjustVolume(bool isWhisper, float volumeCVar)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(volumeCVar);

        if (isWhisper)
        {
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        }

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? WhisperMuffledRange : VoiceRange;
    }
}
