using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stories.TTS;

public sealed partial class TTSSystem : EntitySystem
{
    private const int MaxMessageChars = 100 * 2;
    [Dependency] private readonly BracerSystem _bracer = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;

    private readonly List<string> _sampleText =
        new()
        {
            "Внимание, морпехи, заряжайте оружие и готовьтесь к бою. Обнаружена активность ксеноморфов.",
            "Коммандир, у нас тут настоящая бойня! Запрашиваем срочную эвакуацию с Алмаера!",
            "Ш-ш-ш... Чужие... близко... Я чувствую их запах.",
        };

    [Dependency] private readonly TtsAudioProcessingSystem _ttsAudio = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    private bool _isEnabled;

    public override void Initialize()
    {
        _cfg.OnValueChanged(SCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke, new[] { typeof(HeadsetSystem) });
        SubscribeLocalEvent<TTSPlaybackModifierComponent, GetTTSPlaybackModifiersEvent>(OnGetTTSPlaybackModifiers);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        if (ev.IsHunter)
            soundData = await _ttsAudio.ApplyHunterEffect(soundData);

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private bool GetVoicePrototype(string voiceId, [NotNullWhen(true)] out TTSVoicePrototype? voicePrototype)
    {
        if (!_prototypeManager.TryIndex(voiceId, out voicePrototype))
            return _prototypeManager.TryIndex("father_grigori", out voicePrototype);

        return true;
    }

    private void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (args.Message.Length > MaxMessageChars || voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!GetVoicePrototype(voiceId, out var protoVoice))
            return;

        var messageToUse = args.Message;

        if (HasComp<HunterComponent>(uid) && _bracer.IsHunterWithBracer(uid, out var bracer) &&
            bracer.Value.Comp.TranslatorActive)
            messageToUse = args.OriginalMessage;

        if (args.ObfuscatedMessage != null)
        {
            var playbackModifiers = GetPlaybackModifiers(uid, ChatSystem.WhisperClearRange);
            HandleWhisper(uid, messageToUse, protoVoice.Speaker, playbackModifiers);
            return;
        }

        HandleSay(uid, messageToUse, protoVoice.Speaker, GetPlaybackModifiers(uid, ChatSystem.VoiceRange));
    }

    private void OnGetTTSPlaybackModifiers(Entity<TTSPlaybackModifierComponent> ent, ref GetTTSPlaybackModifiersEvent args)
    {
        args.AddVolumeMultiplier(ent.Comp.VolumeMultiplier);
        args.AddRangeMultiplier(ent.Comp.RangeMultiplier);
        args.AddAudioEffects(ent.Comp.AudioEffects);

        if (ent.Comp.MaxDistance != null)
            args.SetMaxDistance(ent.Comp.MaxDistance.Value);
    }

    private TTSPlaybackModifiers GetPlaybackModifiers(EntityUid uid, float baseRange)
    {
        var ev = new GetTTSPlaybackModifiersEvent(baseRange);
        RaiseLocalEvent(uid, ev);

        return new TTSPlaybackModifiers(
            ev.HasVolumeOverride ? ev.VolumeMultiplier : 1f,
            ev.HasDistanceOverride ? ev.EffectiveMaxDistance : (float?) null,
            ev.HasAudioEffects ? ev.AudioEffects : TTSAudioEffect.None);
    }

    private async void HandleSay(EntityUid uid, string message, string speaker, TTSPlaybackModifiers playbackModifiers)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null)
            return;

        soundData = await ProcessTtsAudio(uid, soundData, playbackModifiers.AudioEffects);

        var ttsEvent = new PlayTTSEvent(
            soundData,
            GetNetEntity(uid),
            volumeMultiplier: playbackModifiers.VolumeMultiplier,
            maxDistanceOverride: playbackModifiers.MaxDistanceOverride);

        FilterAndSend(uid, ttsEvent, playbackModifiers.MaxDistanceOverride ?? ChatSystem.VoiceRange);
    }

    private async void HandleWhisper(EntityUid uid, string message, string speaker, TTSPlaybackModifiers playbackModifiers)
    {
        var fullSoundData = await GenerateTTS(message, speaker, true);
        if (fullSoundData is null)
            return;

        fullSoundData = await ProcessTtsAudio(uid, fullSoundData, playbackModifiers.AudioEffects);

        var fullTtsEvent = new PlayTTSEvent(
            fullSoundData,
            GetNetEntity(uid),
            true,
            volumeMultiplier: playbackModifiers.VolumeMultiplier,
            maxDistanceOverride: playbackModifiers.MaxDistanceOverride);

        FilterAndSend(uid, fullTtsEvent, playbackModifiers.MaxDistanceOverride ?? ChatSystem.WhisperClearRange);
    }

    private TTSAudioEffect GetSpecificVoiceEffects(EntityUid uid)
    {
        if (HasComp<HunterComponent>(uid))
            return TTSAudioEffect.Hunter;

        if (HasComp<XenoComponent>(uid))
            return TTSAudioEffect.XenoHivemind;

        return TTSAudioEffect.None;
    }

    private async Task<byte[]> ProcessTtsAudio(EntityUid uid, byte[] data, TTSAudioEffect playbackEffects)
    {
        var effects = GetSpecificVoiceEffects(uid) | playbackEffects;
        return await _ttsAudio.ApplyPlaybackEffects(data, effects);
    }

    private void FilterAndSend(EntityUid source, PlayTTSEvent ev, float range)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = xformQuery.GetComponent(source);
        var sourceCoords = sourceXform.Coordinates;
        var sourceMapId = sourceXform.MapID;

        var isHunter = HasComp<HunterComponent>(source);
        var isMarine = HasComp<MarineComponent>(source);
        var isXeno = HasComp<XenoComponent>(source);

        var isTranslated = false;
        if (isHunter && _bracer.IsHunterWithBracer(source, out var bracer))
            isTranslated = bracer.Value.Comp.TranslatorActive;

        var recipients = new List<ICommonSession>();
        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { } listener)
                continue;

            var listenerXform = xformQuery.GetComponent(listener);
            if (listenerXform.MapID != sourceMapId)
                continue;

            if (!listenerXform.Coordinates.InRange(EntityManager, sourceCoords, range))
                continue;

            if (HasComp<GhostComponent>(listener))
            {
                recipients.Add(player);
                continue;
            }

            if (isXeno)
            {
                if (HasComp<XenoComponent>(listener))
                {
                    recipients.Add(player);
                    continue;
                }

                if (HasComp<HunterComponent>(listener))
                    recipients.Add(player);
            }
            else if (isMarine)
            {
                if (HasComp<XenoComponent>(listener))
                    continue;

                recipients.Add(player);
            }
            else if (isHunter)
            {
                if (isTranslated)
                    recipients.Add(player);
                else
                {
                    if (HasComp<HunterComponent>(listener))
                        recipients.Add(player);
                }
            }
            else
                recipients.Add(player);
        }

        if (recipients.Count > 0)
            RaiseNetworkEvent(ev, Filter.Empty().AddPlayers(recipients));
    }

    public async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        if (!_isEnabled)
            return null;

        var textSanitized = Sanitize(text);
        if (textSanitized == "")
            return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}

public readonly record struct TTSPlaybackModifiers(
    float VolumeMultiplier,
    float? MaxDistanceOverride,
    TTSAudioEffect AudioEffects);

public sealed class TransformSpeakerVoiceEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string VoiceId;

    public TransformSpeakerVoiceEvent(EntityUid sender, string voiceId)
    {
        Sender = sender;
        VoiceId = voiceId;
    }
}
