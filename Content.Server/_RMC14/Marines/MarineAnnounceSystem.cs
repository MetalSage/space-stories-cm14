using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server._Stories.TTS;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Radio;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Marines;

public sealed class MarineAnnounceSystem : SharedMarineAnnounceSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ARESCoreSystem _core = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distressSignal = default!;
    [Dependency] private readonly SharedDropshipSystem _dropship = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    // Stories-TTS-Start
    [Dependency] private readonly TTSSystem _tts = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    // Stories-TTS-End

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabAnnouncementLogs";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarineCommunicationsComputerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MarineCommunicationsComputerComponent, BoundUIOpenedEvent>(OnBUIOpened);

        SubscribeLocalEvent<RMCPlanetComponent, RMCPlanetAddedEvent>(OnPlanetAdded);

        Subs.BuiEvents<MarineCommunicationsComputerComponent>(MarineCommunicationsComputerUI.Key,
            subs =>
            {
                subs.Event<MarineCommunicationsDesignatePrimaryLZMsg>(OnMarineCommunicationsDesignatePrimaryLZMsg);
            });
    }

    private void OnMapInit(Entity<MarineCommunicationsComputerComponent> computer, ref MapInitEvent args)
    {
        UpdatePlanetMap(computer);
    }

    private void OnBUIOpened(Entity<MarineCommunicationsComputerComponent> computer, ref BoundUIOpenedEvent args)
    {
        UpdatePlanetMap(computer);
    }

    private void OnPlanetAdded(Entity<RMCPlanetComponent> ent, ref RMCPlanetAddedEvent args)
    {
        var computers = EntityQueryEnumerator<MarineCommunicationsComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            UpdatePlanetMap((uid, computer));
        }
    }

    private void OnMarineCommunicationsDesignatePrimaryLZMsg(
        Entity<MarineCommunicationsComputerComponent> computer,
        ref MarineCommunicationsDesignatePrimaryLZMsg args)
    {
        var user = args.Actor;
        if (!TryGetEntity(args.LZ, out var lz))
        {
            Log.Warning($"{ToPrettyString(user)} tried to designate invalid entity {args.LZ} as primary LZ!");
            return;
        }

        _dropship.TryDesignatePrimaryLZ(user, lz.Value);
        _core.CreateARESLog(computer, LogCat, (string)$"{Name(args.Actor)} designated Primary LZ as: {Name(lz.Value)}");
    }

    private void UpdatePlanetMap(Entity<MarineCommunicationsComputerComponent> computer)
    {
        var planet = _distressSignal.SelectedPlanetMapName ?? string.Empty;
        var operation = _distressSignal.OperationName ?? string.Empty;
        var landingZones = new List<LandingZone>();

        foreach (var (id, metaData) in _dropship.GetPrimaryLZCandidates())
        {
            landingZones.Add(new LandingZone(GetNetEntity(id), metaData.EntityName));
        }

        landingZones.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        var state = new MarineCommunicationsComputerBuiState(planet, operation, landingZones);
        _ui.SetUiState(computer.Owner, MarineCommunicationsComputerUI.Key, state);
    }

    public override void AnnounceToMarines(
        string message,
        string wrappedMessage,
        SoundSpecifier? sound = null,
        Filter? filter = null)
    {
        filter ??= GetMarineFilter();

        var plainMessage = FormattedMessage.RemoveMarkupPermissive(message);

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, plainMessage, wrappedMessage, default, false, true, null);
        _audio.PlayGlobal(sound ?? DefaultAnnouncementSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    // Stories-TTS-Start
    public override void AnnounceSigned(EntityUid sender, string message, string? author = null, string? name = null, SoundSpecifier? sound = null, Filter? filter = null)
    {
        base.AnnounceSigned(sender, message, author, name, sound, filter);

        var aresVoice = _configManager.GetCVar(SCCVars.TTSAresVoice);
        var authorVoice = _configManager.GetCVar(SCCVars.TTSCommandVoice);
        if (TryComp<TTSComponent>(sender, out var tts) && !string.IsNullOrEmpty(tts.VoicePrototypeId))
            authorVoice = tts.VoicePrototypeId;

        author ??= Loc.GetString("rmc-announcement-author");

        var headerMsg = Loc.GetString("tts-announce-header", ("author", author));
        var cleanHeader = FormattedMessage.RemoveMarkupPermissive(headerMsg).Trim();
        var wordCount = cleanHeader.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var delay = TimeSpan.FromSeconds(Math.Max(3.0, wordCount * 0.65));
        var recipientsFilter = filter ?? GetMarineFilter();

        if (!string.IsNullOrEmpty(aresVoice))
            _tts.PlayGlobalTTS(cleanHeader, aresVoice, recipientsFilter, isAnnounce: true, isAres: true);

        if (!string.IsNullOrEmpty(authorVoice))
        {
            Timer.Spawn(delay, () =>
            {
                _tts.PlayGlobalTTS(message, authorVoice, recipientsFilter, isAnnounce: true);
            });
        }
    }
    // Stories-TTS-End

    // Stories-TTS-Start
    public override void AnnounceHighCommand(
        string message,
        string? author = null,
        SoundSpecifier? sound = null,
        string? voiceId = null)
    {
        base.AnnounceHighCommand(message, author, sound, voiceId);

        var wrappedMessage = FormatHighCommand(author, message);
        AnnounceToMarines(message, wrappedMessage, sound);

        var aresVoice = _configManager.GetCVar(SCCVars.TTSAresVoice);
        var authorVoice = voiceId ?? _configManager.GetCVar(SCCVars.TTSHighCommandVoice);
        author ??= Loc.GetString("rmc-announcement-author-highcommand");

        var headerMsg = Loc.GetString("tts-announce-header", ("author", author));
        var cleanHeader = FormattedMessage.RemoveMarkupPermissive(headerMsg).Trim();
        var wordCount = cleanHeader.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var delay = TimeSpan.FromSeconds(Math.Max(3.0, wordCount * 0.65));
        var recipientsFilter = GetMarineFilter();

        if (!string.IsNullOrEmpty(aresVoice))
            _tts.PlayGlobalTTS(cleanHeader, aresVoice, recipientsFilter, isAnnounce: true, isAres: true);

        if (!string.IsNullOrEmpty(authorVoice))
        {
            Timer.Spawn(delay, () =>
            {
                _tts.PlayGlobalTTS(message, authorVoice, recipientsFilter, isAnnounce: true);
            });
        }
    }
    // Stories-TTS-End

    public override void AnnounceRadio(
        EntityUid sender,
        string message,
        ProtoId<RadioChannelPrototype> channel)
    {
        AnnounceRadio(sender, message, channel, true);
    }

    public void AnnounceRadio(
        EntityUid sender,
        string message,
        ProtoId<RadioChannelPrototype> channel,
        bool playTTS)
    {
        base.AnnounceRadio(sender, message, channel);

        _adminLogs.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(sender):source} marine announced radio message: {message}");
        _radio.SendRadioMessage(sender, message, channel, sender);
    }

    public override void AnnounceARESStaging(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null,
        LocId? announcement = null)
    {
        AnnounceARESStaging(source, message, sound, announcement, true);
    }

    public void AnnounceARESStaging(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null,
        LocId? announcement = null,
        bool playTTS = true)
    {
        base.AnnounceARESStaging(source, message, sound, announcement);

        var wrappedMessage = FormatARESStaging(announcement, message);

        AnnounceToMarines(message, wrappedMessage, sound);
        if (source != null)
            _adminLogs.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(source.Value):player} ARES announced message: {wrappedMessage}");

        var voice = _configManager.GetCVar(SCCVars.TTSAresVoice);

        var rawHeader = Loc.GetString(announcement ?? "rmc-announcement-ares-command", ("message", ""));
        var cleanHeader = FormattedMessage.RemoveMarkupPermissive(rawHeader).Trim();
        var ttsMessage = $"{cleanHeader} {message}";

        if (playTTS && !string.IsNullOrEmpty(voice))
            _tts.PlayGlobalTTS(ttsMessage, voice, GetMarineFilter(), isAnnounce: true, isAres: true);
    }

    public override void AnnounceSquad(string message, EntProtoId<SquadTeamComponent> squad, SoundSpecifier? sound = null)
    {
        base.AnnounceSquad(message, squad, sound);

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));

        var plainMessage = FormattedMessage.RemoveMarkupPermissive(message);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, plainMessage, message, default, false, true, null);
        _audio.PlayGlobal(sound ?? DefaultSquadSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    public override void AnnounceSquad(string message, EntityUid squad, SoundSpecifier? sound = null)
    {
        base.AnnounceSquad(message, squad, sound);

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));

        var plainMessage = FormattedMessage.RemoveMarkupPermissive(message);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, plainMessage, message, default, false, true, null);
        _audio.PlayGlobal(sound ?? DefaultSquadSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    public override void AnnounceSingle(string message, EntityUid receiver, SoundSpecifier? sound = null)
    {
        base.AnnounceSingle(message, receiver, sound);

        var plainMessage = FormattedMessage.RemoveMarkupPermissive(message);
        if (TryComp(receiver, out ActorComponent? actor))
            _chatManager.ChatMessageToOne(ChatChannel.Radio, plainMessage, message, default, false, actor.PlayerSession.Channel);

        _audio.PlayEntity(sound, receiver, receiver, AudioParams.Default.WithVolume(-2f));
    }
}
