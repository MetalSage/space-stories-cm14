using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared._RMC14.Radio;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using Content.Server._Stories.TTS;
using Content.Shared._Stories.TTS;
using Robust.Shared.Enums;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Server.Radio.EntitySystems;

public sealed class RadioSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!; 
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly TTSSystem _tts = default!; 
    [Dependency] private readonly TtsAudioProcessingSystem _ttsProcessing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly HashSet<string> _messages = new();
    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    private readonly SoundSpecifier _radioSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/radiostatic.ogg")
    {
        Params = new AudioParams
        {
            Volume = -8f,
            Variation = 0.1f,
            MaxDistance = 3.75f,
        },
    }; 

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && component.Channels.Contains(args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid);
            args.Channel = null;
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (!TryComp(uid, out ActorComponent? actor))
            return;

        var playerSession = actor.PlayerSession;
        if (playerSession.Status != SessionStatus.InGame)
            return;

        _netMan.ServerSendMessage(args.ChatMsg, playerSession.Channel);
    }

    private async void ProcessAndSendRadioTts(EntityUid messageSource, string message, RadioChannelPrototype channel, IEnumerable<ICommonSession> recipients)
    {
        if (!_cfg.GetCVar(SCCVars.TTSEnabled))
            return;

        var voiceId = GetVoiceId(messageSource);
        var soundData = await _tts.GenerateTTS(message, voiceId);

        if (soundData == null)
            return;

        var processedSoundData = await _ttsProcessing.ProcessRadioAudio(messageSource, soundData);

        var ttsEvent = new PlayTTSEvent(processedSoundData, sourceUid: null, isWhisper: false, originalSourceUid: GetNetEntity(messageSource));

        var filter = Filter.Empty().AddPlayers(recipients.ToList());
        RaiseNetworkEvent(ttsEvent, filter);
    }

    private string GetVoiceId(EntityUid sourceUid)
    {
        if (TryComp<TTSComponent>(sourceUid, out var tts) && !string.IsNullOrEmpty(tts.VoicePrototypeId) &&
            _prototype.TryIndex<TTSVoicePrototype>(tts.VoicePrototypeId, out var protoVoice))
        {
            return protoVoice.Speaker;
        }
        return "father_grigori";
    }

    public void SendRadioMessage(EntityUid messageSource, string message, ProtoId<RadioChannelPrototype> channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup);
    }

    public void SendRadioMessage(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        if (!_messages.Add(message))
            return;

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        var name = evt.VoiceName;
        name = FormattedMessage.EscapeText(name);

        if (TryComp(messageSource, out JobPrefixComponent? prefix))
        {
            var prefixText = (prefix.AdditionalPrefix != null ? $"{Loc.GetString(prefix.AdditionalPrefix.Value)} " : "") + Loc.GetString(prefix.Prefix);
            if (TryComp(messageSource, out SquadMemberComponent? member) &&
                TryComp(member.Squad, out SquadTeamComponent? team) &&
                team.Radio != null &&
                team.Radio != channel.ID)
            {
                name = $"({Name(member.Squad.Value)} {prefixText}) {name}";
            }
            else
            {
                if (TryComp(messageSource, out FireteamMemberComponent? fireteamMember) && fireteamMember.Fireteam >= 0)
                {
                    prefixText += $" FT{fireteamMember.Fireteam + 1}" + (TryComp(messageSource, out FireteamLeaderComponent? fireteamLeader) ? " TL" : "");
                }
                name = $"({prefixText}) {name}";
            }
        }
        else if (TryComp(messageSource, out RMCRadioPrefixComponent? radioPrefix))
        {
            var prefixText = Loc.GetString(radioPrefix.Prefix);
            name = $"{prefixText} {name}";
        }

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.TryIndex(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        var radioFontSize = speech.FontSize;
        if (TryComp<WearingHeadsetComponent>(messageSource, out var wearingHeadset) &&
            TryComp<RMCHeadsetComponent>(wearingHeadset.Headset, out var headsetComp))
        {
            radioFontSize += headsetComp.RadioTextIncrease ?? 0;
        }
        else if (TryComp<RMCInnateRadioTextIncreaseComponent>(messageSource, out var innateRadioIncrease))
        {
            radioFontSize += innateRadioIncrease.RadioTextIncrease;
        }

        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", channel.Color),
            ("fontType", speech.FontId),
            ("fontSize", radioFontSize), 
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", name),
            ("message", content));

        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            GetNetEntity(messageSource),
            _chatManager.EnsurePlayer(CompOrNull<ActorComponent>(messageSource)?.PlayerSession.UserId)?.Key,
            repeatCheckSender: !HasComp<ChatRepeatIgnoreSenderComponent>(radioSource));
        var chatMsg = new MsgChatMessage { Message = chat };
        var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg);

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        var recipientUids = new List<EntityUid>();
        
        // Stories-Fix: Используем HashSet для отслеживания отправленных клиентов, чтобы избежать дублей (призрак + обычный)
        var sentClients = new HashSet<INetChannel>();

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            RaiseLocalEvent(receiver, ref ev);
            recipientUids.Add(receiver);

            // Регистрируем клиента получателя
            if (transform.ParentUid.IsValid() && TryComp<ActorComponent>(transform.ParentUid, out var actor))
            {
                sentClients.Add(actor.PlayerSession.Channel);
            }
            else if (TryComp<ActorComponent>(receiver, out var directActor)) // На случай встроенного радио у игрока
            {
                sentClients.Add(directActor.PlayerSession.Channel);
            }
        }

        // Stories-Fix: Отдельный список для призраков для TTS, чтобы не смешивать с EntityUid получателей
        var ghostSessions = new List<ICommonSession>();

        // Stories-Fix: Рассылка призракам
        if (canSend)
        {
            foreach (var session in _playerManager.Sessions)
            {
                if (session.AttachedEntity is not { } ent) continue;
                
                // Если это призрак и он еще не получил сообщение (например, через вселение в тело с радио)
                if (HasComp<GhostComponent>(ent))
                {
                    if (sentClients.Contains(session.Channel)) 
                        continue;

                    _netMan.ServerSendMessage(chatMsg, session.Channel);
                    sentClients.Add(session.Channel);
                    ghostSessions.Add(session);
                }
            }
        }

        if (canSend && (recipientUids.Count > 0 || ghostSessions.Count > 0))
        {
            var sessions = new List<ICommonSession>();
            
            // Добавляем обычных получателей
            var actorQuery = GetEntityQuery<ActorComponent>();
            foreach (var uid in recipientUids)
            {
                var parent = Transform(uid).ParentUid;
                var target = actorQuery.HasComponent(uid) ? uid : (actorQuery.HasComponent(parent) ? parent : (EntityUid?) null);

                if (target.HasValue && actorQuery.TryGetComponent(target.Value, out var actor))
                {
                    if (actor.PlayerSession.Status == SessionStatus.InGame)
                        sessions.Add(actor.PlayerSession);
                }
            }

            // Добавляем призраков для TTS
            sessions.AddRange(ghostSessions);

            if (sessions.Count > 0)
            {
                ProcessAndSendRadioTts(messageSource, message, channel, sessions);
            }
        }

        if (canSend && _cfg.GetCVar(SCCVars.TTSEnabled) &&
            !HasComp<XenoComponent>(messageSource) &&
            HasComp<RMCHeadsetComponent>(radioSource))
        {
            var filter = Filter.Pvs(messageSource).RemoveWhereAttachedEntity(HasComp<XenoComponent>);
            _audio.PlayEntity(_radioSound, filter, messageSource, false);
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(chat);
        _messages.Remove(message);
    }

    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }
}
