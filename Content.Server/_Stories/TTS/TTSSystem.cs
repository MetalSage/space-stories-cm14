using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Content.Shared.GameTicking;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Marines;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Server.Radio.EntitySystems;
using Robust.Shared.Map;

namespace Content.Server._Stories.TTS;

public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;

    private readonly List<string> _sampleText =
        new()
        {
            "Внимание, морпехи, заряжайте оружие и готовьтесь к бою. Обнаружена активность ксеноморфов.",
            "Коммандир, у нас тут настоящая бойня! Запрашиваем срочную эвакуацию с Алмаера!",
            "Инженер, срочно установите турели на этом перекрестке. Они прорываются!",
            "Медик! У нас тут раненый! Срочно нужна помощь, его всего исцарапали!",
            "Всем отрядам, королева ксеноморфов замечена в центральном улье. Приготовиться к штурму!",
            "Черт, патроны почти на исходе! Кто-нибудь, прикройте, я перезаряжаюсь!",
            "Датчики движения показывают что-то большое и очень быстрое. Всем быть начеку!",
            "Смотрите в оба, эти твари могут прятаться в вентиляции. Не хочу, чтобы одна из них упала мне на голову.",
            "Ш-ш-ш... Чужие... близко... Я чувствую их запах.",
            "Мои когти жаждут плоти этих ходячих мешков с мясом.",
            "Королева... зовет... Мы должны защитить улей любой ценой.",
            "Их фонарики... такие яркие... Разбить! Уничтожить!",
            "Плевок кислотой готов. Кто будет моей следующей целью?",
            "Прячьтесь в тени, сестры. Пусть они подойдут поближе, а потом мы нанесем удар.",
            "Я слышу их крики... сладкая музыка для моих ушей.",
            "Этот металл... он не остановит нас. Мы прорвемся."
        };

    private const int MaxMessageChars = 100 * 2;
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(SCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke, before: new []{ typeof(HeadsetSystem) });
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

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private bool GetVoicePrototype(string voiceId, [NotNullWhen(true)] out TTSVoicePrototype? voicePrototype)
    {
        if (!_prototypeManager.TryIndex(voiceId, out voicePrototype))
        {
            return _prototypeManager.TryIndex("father_grigori", out voicePrototype);
        }

        return true;
    }

    private void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null)
            return;

        var voiceId = component.VoicePrototypeId;
        if (args.Message.Length > MaxMessageChars || voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!GetVoicePrototype(voiceId, out var protoVoice))
            return;

        if (args.ObfuscatedMessage != null)
        {
            HandleWhisper(uid, args.Message, protoVoice.Speaker);
            return;
        }

        HandleSay(uid, args.Message, protoVoice.Speaker);
    }

    private async void HandleSay(EntityUid uid, string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null)
            return;

        var ttsEvent = new PlayTTSEvent(soundData, GetNetEntity(uid));
        FilterAndSend(uid, ttsEvent, ChatSystem.VoiceRange);
    }

    private async void HandleWhisper(EntityUid uid, string message, string speaker)
    {
        var fullSoundData = await GenerateTTS(message, speaker, true);
        if (fullSoundData is null)
            return;

        var fullTtsEvent = new PlayTTSEvent(fullSoundData, GetNetEntity(uid), true);
        FilterAndSend(uid, fullTtsEvent, ChatSystem.WhisperClearRange);
    }

    private void FilterAndSend(EntityUid source, PlayTTSEvent ev, float range)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = xformQuery.GetComponent(source);
        var sourceCoords = sourceXform.Coordinates;

        var isMarineSpeaker = HasComp<MarineComponent>(source);
        var isXenoSpeaker = HasComp<XenoComponent>(source);
        var speakerHasFaction = isMarineSpeaker || isXenoSpeaker;

        var recipients = new List<ICommonSession>();
        foreach (var player in Filter.Pvs(source).Recipients)
        {
            if (player.AttachedEntity is not { } listener)
                continue;

            var listenerXform = xformQuery.GetComponent(listener);
            if (!listenerXform.Coordinates.InRange(EntityManager, sourceCoords, range))
                continue;

            if (speakerHasFaction)
            {
                if ((isMarineSpeaker && HasComp<XenoComponent>(listener)) ||
                    (isXenoSpeaker && HasComp<MarineComponent>(listener)))
                {
                    continue;
                }
            }

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
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}

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
