using Content.Server._Stories.TTS;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Announce;

public sealed class XenoAnnounceSystem : SharedXenoAnnounceSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    // Stories-TTS-Start
    [Dependency] private readonly TTSSystem _tts = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    // Stories-TTS-End

    public override void Announce(
        EntityUid source,
        Filter filter,
        string message,
        string wrapped,
        SoundSpecifier? sound = null,
        PopupType? popup = null,
        bool needsQueen = false,
        bool includeGhosts = true)
    {
        base.Announce(source, filter, message, wrapped, sound, popup, needsQueen, includeGhosts);

        if (needsQueen)
        {
            if (Hive.GetHive(source) is { } sourceHive)
            {
                if (!Hive.HasHiveQueen(sourceHive))
                    return;
            }
            else
            {
                return;
            }
        }

        if (includeGhosts)
            filter.AddWhereAttachedEntity(HasComp<GhostComponent>);

        if (source.IsValid())
            _adminLogs.Add(LogType.RMCXenoAnnounce, $"{ToPrettyString(source):source} xeno announced message: {message}");

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrapped, source, false, true, null);
        _audio.PlayGlobal(sound, filter, true);

        if (popup == null)
            return;

        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } recipient)
                _popup.PopupEntity(message, recipient, recipient, popup.Value);
        }
    }

    // Stories-TTS-Start
    public override void AnnounceQueenMother(string message)
    {
        var sound = new Content.Shared._RMC14.Bioscan.BioscanComponent().XenoSound;
        var filter = Filter.Empty().AddWhereAttachedEntity(HasComp<Content.Shared._RMC14.Xenonids.XenoComponent>);
        var format = FormatQueenMother(message);

        Announce(default, filter, message, format, sound);

        var voice = _configManager.GetCVar(SCCVars.TTSQueenMotherVoice);
        var ttsMessage = Loc.GetString("tts-announce-queen-mother", ("message", message));

        if (!string.IsNullOrEmpty(voice))
            _tts.PlayGlobalTTS(ttsMessage, voice, filter, true, isAnnounce: true);
    }
    // Stories-TTS-Start
}
