using Content.Server._Stories.Hunter.Spawning;
using Content.Server._Stories.Sponsors;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Players.JobWhitelist;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Clothing;
using Content.Shared._RMC14.Vendors;
using Content.Shared._Stories.Hunter;
using Content.Shared._Stories.Hunter.Equipment;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Profiles;
using Content.Shared._Stories.SCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Hunter;

public sealed partial class HunterSystem : SharedHunterSystem
{
    private const string HunterShipPath = "Maps/_Stories/huntership.yml";
    private const string HunterSpawnPointId = "STSpawnPointHunter";
    private const string HunterMobId = "STMobHunter";
    private const string HunterJobId = "STHunter";
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly HunterSpawningSystem _hunterSpawning = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!;
    [Dependency] private readonly JobWhitelistManager _whitelistManager = default!;
    private int _currentHunters;
    private int _currentSponsorHunters;

    private EntityUid? _hunterShipMapUid;
    private ISawmill _sawmill = default!;

    public bool IsHuntRound { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("hunter.join");

        SubscribeNetworkEvent<RequestJoinHunterEvent>(OnJoinRequest);
        SubscribeNetworkEvent<UpdateHunterProfileEvent>(OnUpdateHunterProfile);
        SubscribeNetworkEvent<RequestHunterLobbyStateEvent>(OnLobbyStateRequest);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        SubscribeLocalEvent<CMAutomatedVendorComponent, BeforeItemsVendedEvent>(OnBeforeItemsVended);
        SubscribeLocalEvent<CMAutomatedVendorComponent, AfterItemVendedEvent>(OnAfterItemVended);

        InitializeIdentity();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public void ForceHuntRound()
    {
        if (IsHuntRound)
            return;

        IsHuntRound = true;

        LoadHunterMap();
        BroadcastLobbyStateUpdate();

        _chatManager.DispatchServerAnnouncement(Loc.GetString("st-hunter-round-started-announcement"), Color.Red);
        _chatManager.SendAdminAnnouncement(Loc.GetString("st-hunter-round-admin-announcement"));
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected || e.NewStatus == SessionStatus.InGame)
            SendLobbyStateUpdate(e.Session);
    }

    private void OnLobbyStateRequest(RequestHunterLobbyStateEvent msg, EntitySessionEventArgs args)
    {
        SendLobbyStateUpdate(args.SenderSession);
    }

    private void BroadcastLobbyStateUpdate()
    {
        CalculateSlots(out var maxBase, out var maxSponsor);

        var msg = new HunterLobbyStateEvent
        {
            IsHuntRound = IsHuntRound,
            AvailableBaseSlots = Math.Max(0, maxBase - _currentHunters),
            AvailableSponsorSlots = Math.Max(0, maxSponsor - _currentSponsorHunters),
            Reason = IsHuntRound ? "" : Loc.GetString("st-hunter-not-hunting-ground"),
        };

        RaiseNetworkEvent(msg);
    }

    private void SendLobbyStateUpdate(ICommonSession session)
    {
        CalculateSlots(out var maxBase, out var maxSponsor);

        var msg = new HunterLobbyStateEvent
        {
            IsHuntRound = IsHuntRound,
            AvailableBaseSlots = Math.Max(0, maxBase - _currentHunters),
            AvailableSponsorSlots = Math.Max(0, maxSponsor - _currentSponsorHunters),
            Reason = IsHuntRound ? "" : Loc.GetString("st-hunter-not-hunting-ground"),
        };

        RaiseNetworkEvent(msg, session.Channel);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        IsHuntRound = false;
        _currentHunters = 0;
        _currentSponsorHunters = 0;
        _hunterShipMapUid = null;
        BroadcastLobbyStateUpdate();
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _hunterShipMapUid = null;
        RollHuntEvent();

        if (IsHuntRound)
        {
            LoadHunterMap();
            _chatManager.DispatchServerAnnouncement(Loc.GetString("st-hunter-round-started-announcement"), Color.Red);
            _chatManager.SendAdminAnnouncement(Loc.GetString("st-hunter-round-admin-announcement"));
        }

        BroadcastLobbyStateUpdate();
    }

    private void RollHuntEvent()
    {
        if (_playerManager.PlayerCount < _cfg.GetCVar(SCCVars.HunterMinPlayersForRound))
        {
            IsHuntRound = false;
            return;
        }

        var chance = _cfg.GetCVar(SCCVars.HunterRoundChance);
        IsHuntRound = _random.Prob(chance);
    }

    private void CalculateSlots(out int baseSlots, out int sponsorSlots)
    {
        var playerCount = _playerManager.PlayerCount;
        var startCount = _cfg.GetCVar(SCCVars.HunterStartCount);
        var ratio = _cfg.GetCVar(SCCVars.HunterPlayerRatio);

        var extraSlots = 0;
        if (ratio > 0)
            extraSlots = (int)Math.Floor((double)playerCount / ratio);

        baseSlots = startCount + extraSlots;
        sponsorSlots = _cfg.GetCVar(SCCVars.HunterSponsorExtraSlots);
    }

    private void OnBeforeItemsVended(Entity<CMAutomatedVendorComponent> ent, ref BeforeItemsVendedEvent args)
    {
        if (
            !_prototypeManager.TryIndex(args.VendedPrototype, out var proto)
            || !proto.HasComponent<HunterProfileArmorBundleComponent>()
        )
            return;

        if (!_playerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        var profile = _prefsManager.GetHunterProfile(session.UserId);
        if (profile == null)
            return;

        args.Items.Clear();

        if (!string.IsNullOrEmpty(profile.ArmorPrototype))
            args.Items.Add(profile.ArmorPrototype);

        if (!string.IsNullOrEmpty(profile.MaskPrototype))
            args.Items.Add(profile.MaskPrototype);

        if (!string.IsNullOrEmpty(profile.GreavesPrototype))
            args.Items.Add(profile.GreavesPrototype);
    }

    private void OnAfterItemVended(Entity<CMAutomatedVendorComponent> ent, ref AfterItemVendedEvent args)
    {
        if (!_playerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        if (!HasComp<HunterComponent>(args.User))
            return;

        var profile = _prefsManager.GetHunterProfile(session.UserId);
        if (profile == null)
            return;

        var meta = MetaData(args.Item);
        if (meta.EntityPrototype != null && meta.EntityPrototype.ID.StartsWith("STHunterCape"))
        {
            if (TryComp<AppearanceComponent>(args.Item, out var appearance))
                _appearance.SetData(args.Item, HunterCapeVisuals.Color, profile.CapeColor, appearance);
        }
        else if (TryComp<HelmetAccessoryHolderComponent>(args.Item, out _))
        {
            if (string.IsNullOrEmpty(profile.HeadAccessory) || profile.HeadAccessory.Id == "Nothing")
                return;

            var accessory = Spawn(profile.HeadAccessory, Transform(args.User).Coordinates);

            if (!_hands.TryPickupAnyHand(args.User, accessory))
                _popup.PopupEntity(Loc.GetString("st-hunter-vendor-hands-full"), args.User, args.User);
        }
    }

    private async void OnUpdateHunterProfile(UpdateHunterProfileEvent msg, EntitySessionEventArgs args)
    {
        await _prefsManager.SetHunterProfile(args.SenderSession.UserId, msg.Profile);
    }

    private void LoadHunterMap()
    {
        if (_hunterShipMapUid != null && Exists(_hunterShipMapUid))
            return;

        var options = new DeserializationOptions { InitializeMaps = true };

        if (_mapLoader.TryLoadMap(new ResPath(HunterShipPath), out var map, out _, options))
        {
            if (map.HasValue)
                _hunterShipMapUid = map.Value.Owner;
            else
                _sawmill.Error("Hunter ship map loaded but returned null entity.");
        }
        else
            _sawmill.Error($"Failed to load hunter ship map from {HunterShipPath}");
    }

    private void OnJoinRequest(RequestJoinHunterEvent msg, EntitySessionEventArgs args)
    {
        var player = (ICommonSession)args.SenderSession;
        var userId = player.UserId;

        void NotifyPlayer(string message)
        {
            _chatManager.DispatchServerMessage(player, Loc.GetString(message));
        }

        SendLobbyStateUpdate(player);

        if (!IsHuntRound)
        {
            NotifyPlayer("st-hunter-join-failed-not-hunt-round");
            return;
        }

        if (_banManager.GetJobBans(userId)?.Contains(HunterJobId) == true)
        {
            NotifyPlayer("st-hunter-join-failed-banned");
            return;
        }

        try
        {
            var hunterProfile = _prefsManager.GetHunterProfile(userId);
            if (hunterProfile == null)
            {
                _sawmill.Error(
                    $"SERVER: CRITICAL - Hunter profile for {userId} is NULL. Cannot proceed with join request."
                );
                NotifyPlayer("st-hunter-join-failed-no-profile");
                return;
            }

            if (
                _gameTicker.PlayerGameStatuses.TryGetValue(userId, out var status)
                && status == PlayerGameStatus.JoinedGame
            )
            {
                NotifyPlayer("st-hunter-join-failed-already-playing");
                return;
            }

            if (!ValidateHunterStatus(player, hunterProfile.Status))
            {
                NotifyPlayer("st-hunter-join-failed-status-whitelist");
                return;
            }

            CalculateSlots(out var maxBaseSlots, out var maxSponsorSlots);
            var isSponsor = _sponsorsManager.TryGetInfo(userId, out var sponsorInfo) && sponsorInfo.CanPlayHunter;

            var approved = false;
            var usedSponsorSlot = false;

            if (_currentHunters < maxBaseSlots)
            {
                approved = true;
                usedSponsorSlot = false;
            }
            else if (isSponsor && _currentSponsorHunters < maxSponsorSlots)
            {
                approved = true;
                usedSponsorSlot = true;
            }

            if (!approved)
            {
                NotifyPlayer("st-hunter-join-failed-no-slots");
                return;
            }

            JoinAsHunter(player, hunterProfile);

            if (usedSponsorSlot)
                _currentSponsorHunters++;
            else
                _currentHunters++;

            BroadcastLobbyStateUpdate();
        }
        catch (Exception e)
        {
            _sawmill.Error(
                $"SERVER: An exception occurred while processing JoinAsHunter request for {player.Name}: {e}"
            );
            NotifyPlayer("st-hunter-join-failed-error");
        }
    }

    private bool ValidateHunterStatus(ICommonSession session, HunterStatus status)
    {
        var userId = session.UserId;
        var isWhitelisted = false;

        switch (status)
        {
            case HunterStatus.Normal:
                if (_whitelistManager.IsWhitelisted(userId, "STHunter"))
                    isWhitelisted = true;
                break;
            case HunterStatus.Council:
                if (_whitelistManager.IsWhitelisted(userId, "STHunterCouncil"))
                    isWhitelisted = true;
                break;
            case HunterStatus.Leader:
                if (_whitelistManager.IsWhitelisted(userId, "STHunterLeader"))
                    isWhitelisted = true;
                break;
        }

        if (isWhitelisted)
            return true;

        if (_sponsorsManager.TryGetInfo(userId, out var info) && info.CanPlayHunter)
        {
            if (status <= info.MaxHunterStatus)
                return true;
        }

        return false;
    }

    private void JoinAsHunter(ICommonSession player, HunterProfile profile)
    {
        try
        {
            LoadHunterMap();

            if (_hunterShipMapUid == null)
            {
                _sawmill.Error("SERVER: CRITICAL - Hunter ship map is not loaded! Cannot spawn hunter. Aborting.");
                return;
            }

            var spawnPoints = new List<EntityCoordinates>();
            var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (query.MoveNext(out _, out var spawnPoint, out var xform))
            {
                if (spawnPoint.Job?.Id == HunterJobId && xform.MapUid == _hunterShipMapUid)
                    spawnPoints.Add(xform.Coordinates);
            }

            if (spawnPoints.Count == 0)
            {
                var protoQuery = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                while (protoQuery.MoveNext(out _, out var meta, out var xform))
                {
                    if (meta.EntityPrototype?.ID == HunterSpawnPointId && xform.MapUid == _hunterShipMapUid)
                        spawnPoints.Add(xform.Coordinates);
                }
            }

            if (spawnPoints.Count == 0)
            {
                _sawmill.Error("SERVER: CRITICAL - No hunter spawn points found on the ship map! Aborting.");
                return;
            }

            var spawnLoc = _random.Pick(spawnPoints);

            _hunterSpawning.SpawnHunter(spawnLoc, profile, player);

            _gameTicker.PlayerJoinGame(player);
        }
        catch (Exception e)
        {
            _sawmill.Error($"SERVER: An exception occurred during the JoinAsHunter process for {player.Name}: {e}");
        }
    }
}
