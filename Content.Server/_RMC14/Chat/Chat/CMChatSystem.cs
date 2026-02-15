using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Speech.EntitySystems;
using Content.Server.Speech.Prototypes;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Chat.Chat;

public sealed class CMChatSystem : SharedCMChatSystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ReplacementAccentSystem _wordreplacement = default!;
    [Dependency] private readonly BracerSystem _bracer = default!; // Stories-Hunter

    private static readonly ProtoId<ReplacementAccentPrototype> ChatSanitize = "CMChatSanitize";
    private static readonly ProtoId<ReplacementAccentPrototype> MarineChatSanitize = "CMChatSanitizeMarine";
    private static readonly ProtoId<ReplacementAccentPrototype> XenoChatSanitize = "CMChatSanitizeXeno";
    private static readonly Regex MultiBroadcastRegex = new(@"^[:.]([^ ]+)\s+(.*)"); // Stories-Hunter

    private readonly List<ICommonSession> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MarineComponent, ChatMessageAfterGetRecipients>(OnMarineAfterGetRecipients);
        SubscribeLocalEvent<XenoComponent, ChatMessageAfterGetRecipients>(OnXenoAfterGetRecipients);
        SubscribeLocalEvent<HunterComponent, ChatMessageAfterGetRecipients>(OnHunterAfterGetRecipients); // Stories-Hunter
    }

    private void OnMarineAfterGetRecipients(Entity<MarineComponent> ent, ref ChatMessageAfterGetRecipients args)
    {
        _toRemove.Clear();

        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer)
                continue;

            // Stories-Hunter-Start
            if (HasComp<HunterComponent>(session.AttachedEntity))
                continue;
            // Stories-Hunter-End

            if (HasComp<XenoComponent>(session.AttachedEntity))
                _toRemove.Add(session);
        }

        foreach (var session in _toRemove)
        {
            args.Recipients.Remove(session);
        }
    }

    private void OnXenoAfterGetRecipients(Entity<XenoComponent> ent, ref ChatMessageAfterGetRecipients args)
    {
        _toRemove.Clear();

        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer)
                continue;

            // Stories-Hunter-Start
            if (HasComp<HunterComponent>(session.AttachedEntity))
                continue;
            // Stories-Hunter-End

            if (HasComp<MarineComponent>(session.AttachedEntity))
            {
                _toRemove.Add(session);
                continue;
            }
        }

        foreach (var session in _toRemove)
        {
            args.Recipients.Remove(session);
        }
    }

    // Stories-Hunter-Start
    private void OnHunterAfterGetRecipients(Entity<HunterComponent> ent, ref ChatMessageAfterGetRecipients args)
    {
        var isTranslated = false;
        if (_bracer.IsHunterWithBracer(ent, out var bracer))
        {
            isTranslated = bracer.Value.Comp.TranslatorActive;
        }

        if (isTranslated)
            return;

        _toRemove.Clear();

        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer)
                continue;

            if (session.AttachedEntity is not { } listener)
            {
                _toRemove.Add(session);
                continue;
            }

            if (!HasComp<HunterComponent>(listener))
            {
                _toRemove.Add(session);
            }
        }

        foreach (var session in _toRemove)
        {
            args.Recipients.Remove(session);
        }
    }
    // Stories-Hunter-End

    public override string SanitizeMessageReplaceWords(EntityUid source, string msg)
    {
        msg = _wordreplacement.ApplyReplacements(msg, ChatSanitize);

        var factionSanitize = HasComp<XenoComponent>(source) ? XenoChatSanitize : MarineChatSanitize;
        msg = _wordreplacement.ApplyReplacements(msg, factionSanitize);

        return msg;
    }

    public override void ChatMessageToOne(
        ChatChannel channel,
        string message,
        string wrappedMessage,
        EntityUid source,
        bool hideChat,
        INetChannel client,
        Color? colorOverride = null,
        bool recordReplay = false,
        string? audioPath = null,
        float audioVolume = 0,
        NetUserId? author = null)
    {
        _chat.ChatMessageToOne(
            channel,
            message,
            wrappedMessage,
            source,
            hideChat,
            client,
            colorOverride,
            recordReplay,
            audioPath,
            audioVolume,
            author
        );
    }

    public override void ChatMessageToMany(
        string message,
        string wrappedMessage,
        Filter filter,
        ChatChannel channel,
        EntityUid source = default,
        bool hideChat = false,
        Color? colorOverride = null,
        bool recordReplay = false,
        string? audioPath = null,
        float audioVolume = 0,
        NetUserId? author = null)
    {
        _chat.ChatMessageToManyFiltered(
            filter,
            channel,
            message,
            wrappedMessage,
            source,
            hideChat,
            recordReplay,
            colorOverride,
            audioPath,
            audioVolume
        );
    }

    public override void Emote(
        EntityUid source,
        string message,
        string? nameOverride = null,
        bool checkRadioPrefix = true,
        bool ignoreActionBlocker = false)
    {
        ICommonSession? player = null;
        if (TryComp(source, out ActorComponent? actor))
            player = actor.PlayerSession;

        _chatSystem.TrySendInGameICMessage(
            source,
            message,
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            false,
            null,
            player,
            nameOverride,
            checkRadioPrefix,
            ignoreActionBlocker
        );
    }

    public List<string>? TryMultiBroadcast(EntityUid source, string message)
    {
        // Stories-Hunter-Start
        var match = MultiBroadcastRegex.Match(message);
        if (!match.Success)
            return null;

        var keysPart = match.Groups[1].Value;
        var messagePart = match.Groups[2].Value;

        if (!keysPart.Contains(','))
            return null;

        var keys = keysPart.Split(',')
            .Select(k => k.Trim().ToLowerInvariant())
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();

        if (keys.Count < 2)
            return null;

        foreach (var key in keys)
        {
            if (!_chatSystem._keyCodes.ContainsKey(key))
            {
                _popup.PopupEntity($"Неверный ключ канала для мультитрансляции: '{key}'", source, source);
                return null;
            }
        }
        // Stories-Hunter-End

        if (!HasComp<InventoryComponent>(source))
            return null;

        var time = _timing.CurTime;
        Entity<HeadsetMultiBroadcastComponent>? headset = null;
        var ears = _inventory.GetSlotEnumerator(source, SlotFlags.EARS);
        while (ears.MoveNext(out var ear))
        {
            if (ear.ContainedEntity is not { } contained)
                continue;

            if (TryComp(contained, out HeadsetMultiBroadcastComponent? headsetComp))
            {
                headset = (contained, headsetComp);
                break;
            }
        }

        if (headset == null)
            return null;

        // Stories-Hunter-Start
        if (keys.Count > headset.Value.Comp.Maximum)
        {
            _popup.PopupEntity($"Вы не можете транслировать более чем в {headset.Value.Comp.Maximum} каналов одновременно.", source, source);
            return null;
        }

        var timeLeft = headset.Value.Comp.Last + headset.Value.Comp.Cooldown - time;
        if (headset.Value.Comp.Last != TimeSpan.Zero && timeLeft > TimeSpan.Zero)
        {
            _popup.PopupEntity(
                $"Вы использовали систему мультитрансляции слишком часто. Подождите еще {timeLeft.Value.TotalSeconds:F0} секунд.",
                source,
                source,
                PopupType.MediumCaution
            );
            return null;
        }

        var generatedMessages = new List<string>();
        foreach (var key in keys)
        {
            generatedMessages.Add($":{key} {messagePart}");
        }

        headset.Value.Comp.Last = time;
        Dirty(headset.Value);

        return generatedMessages;
        // Stories-Hunter-End
    }
}
