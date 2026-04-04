using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Shared.Player;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    public const float PresetFailedCooldownIncrease = 30f;

    /// <summary>
    /// The selected preset that will be used at the start of the next round.
    /// </summary>
    public GamePresetPrototype? Preset { get; private set; }

    /// <summary>
    /// The preset that's currently active.
    /// </summary>
    public GamePresetPrototype? CurrentPreset { get; private set; }

    /// <summary>
    /// Countdown to the preset being reset to the server default.
    /// </summary>
    public int? ResetCountdown;

    private bool StartPreset(ICommonSession[] origReadyPlayers, bool force)
    {
        var startAttempt = new RoundStartAttemptEvent(origReadyPlayers, force);
        RaiseLocalEvent(startAttempt);

        if (!startAttempt.Cancelled)
            return true;

        var presetTitle = CurrentPreset != null ? Loc.GetString(CurrentPreset.ModeTitle) : string.Empty;

        void FailedPresetRestart()
        {
            SendServerMessage(Loc.GetString("game-ticker-start-round-cannot-start-game-mode-restart",
                ("failedGameMode", presetTitle)));
            RestartRound();
            DelayStart(TimeSpan.FromSeconds(PresetFailedCooldownIncrease));
        }

            if (_cfg.GetCVar(CCVars.GameLobbyFallbackEnabled))
            {
                var fallbackPresets = _cfg.GetCVar(CCVars.GameLobbyFallbackPreset).Split(",");
                var startFailed = true;

            foreach (var preset in fallbackPresets)
            {
                ClearGameRules();
                SetGamePreset(preset);
                AddGamePresetRules();
                StartGamePresetRules();

                startAttempt.Uncancel();
                RaiseLocalEvent(startAttempt);

                if (!startAttempt.Cancelled)
                {
                    _chatManager.SendAdminAnnouncement(
                        Loc.GetString("game-ticker-start-round-cannot-start-game-mode-fallback",
                            ("failedGameMode", presetTitle),
                            ("fallbackMode", Loc.GetString(preset))));
                    RefreshLateJoinAllowed();
                    startFailed = false;
                    break;
                }
            }

            if (startFailed)
            {
                FailedPresetRestart();
                return false;
            }
        }

        else
        {
            FailedPresetRestart();
            return false;
        }

        return true;
    }

        private void InitializeGamePreset()
        {
            SetGamePreset(LobbyEnabled ? _cfg.GetCVar(CCVars.GameLobbyDefaultPreset) : "sandbox");
        }

    public void SetGamePreset(GamePresetPrototype? preset, bool force = false, int? resetDelay = null)
    {
        // Do nothing if this game ticker is a dummy!
        if (DummyTicker)
            return;

        if (resetDelay is not null)
        {
            ResetCountdown = resetDelay.Value;
            // Reset counter is checked and changed at the end of each round
            // So if the game is in the lobby, the first requested round will happen before the check, and we need one less check
            if (CurrentPreset is null)
                ResetCountdown = resetDelay.Value - 1;
        }
        else
        {
            ResetCountdown = null;
        }

        Preset = preset;
        ValidateMap();
        UpdateInfoText();

        if (force)
        {
            StartRound(true);
        }
    }

    public void SetGamePreset(string preset, bool force = false)
    {
        var proto = FindGamePreset(preset);
        if(proto != null)
            SetGamePreset(proto, force);
    }

    public GamePresetPrototype? FindGamePreset(string preset)
    {
        if (_prototypeManager.TryIndex(preset, out GamePresetPrototype? presetProto))
            return presetProto;

        foreach (var proto in _prototypeManager.EnumeratePrototypes<GamePresetPrototype>())
        {
            foreach (var alias in proto.Alias)
            {
                if (preset.Equals(alias, StringComparison.InvariantCultureIgnoreCase))
                    return proto;
            }
        }

        return null;
    }

    public bool TryFindGamePreset(string preset, [NotNullWhen(true)] out GamePresetPrototype? prototype)
    {
        prototype = FindGamePreset(preset);

        return prototype != null;
    }

    public bool IsMapEligible(GameMapPrototype map)
    {
        if (Preset == null)
            return true;

        if (Preset.MapPool == null || !_prototypeManager.TryIndex<GameMapPoolPrototype>(Preset.MapPool, out var pool))
            return true;

        return pool.Maps.Contains(map.ID);
    }

    private void ValidateMap()
    {
        if (Preset == null || _gameMapManager.GetSelectedMap() is not { } map)
            return;

        if (Preset.MapPool == null ||
            !_prototypeManager.TryIndex<GameMapPoolPrototype>(Preset.MapPool, out var pool))
            return;

        if (pool.Maps.Contains(map.ID))
            return;

        _gameMapManager.SelectMapRandom();
    }

    [PublicAPI]
    private bool AddGamePresetRules()
    {
        if (DummyTicker || Preset == null)
            return false;

        // Stories-LowPop-Start
        // Stories uses a preset family here so the lobby can always point at one stable preset ID
        // while the concrete round variant is chosen at the moment the round actually starts.
        var preset = ResolveRoundStartPreset(Preset);
        // Stories-LowPop-End
        CurrentPreset = preset;
        foreach (var rule in preset.Rules)
        {
            AddGameRule(rule);
        }

        return true;
    }

    private GamePresetPrototype ResolveRoundStartPreset(GamePresetPrototype preset)
    {
        if (preset.RoundStartResolvePresets.Count == 0)
            return preset;

        // Stories-LowPop-Start
        var playerCount = ReadyPlayerCount();
        // Resolve a preset family (for example normal + low population variants) at the moment the round starts,
        // so the selected variant matches the current ready player count rather than the previous round.
        var candidates = new List<GamePresetPrototype>();
        foreach (var candidateId in preset.RoundStartResolvePresets)
        {
            if (_prototypeManager.TryIndex(candidateId, out GamePresetPrototype? candidate))
            {
                candidates.Add(candidate);
                continue;
            }

            _sawmill.Error($"Invalid roundStartResolvePresets entry '{candidateId}' on preset {preset.ID}.");
        }

        if (candidates.Count == 0)
            return preset;

        // Preserve declaration order from the prototype so content can define an explicit priority
        // between normal and special-case variants.
        var selectable = candidates
            .Where(candidate => !ShouldIgnorePresetOnFirstRoundAfterRestart(candidate))
            .Where(candidate => (candidate.MinPlayers == null || playerCount >= candidate.MinPlayers) &&
                                (candidate.MaxPlayers == null || playerCount <= candidate.MaxPlayers))
            .ToList();

        if (selectable.Count > 0)
            return selectable[0];

        // Stories-LowPop-End
        var fallback = candidates.FirstOrDefault(candidate => !ShouldIgnorePresetOnFirstRoundAfterRestart(candidate));
        return fallback ?? preset;
    }

    private bool ShouldIgnorePresetOnFirstRoundAfterRestart(GamePresetPrototype preset)
    {
        // Stories-LowPop: RoundId == 0 means the first lobby after a server restart.
        return RoundId == 0 && preset.IgnoreOnFirstRoundAfterRestart;
    }

    private void TryResetPreset()
    {
        if (ResetCountdown is null || ResetCountdown-- > 0)
            return;

        InitializeGamePreset();
        ResetCountdown = null;
    }

    public void StartGamePresetRules()
    {
        // May be touched by the preset during init.
        var rules = new List<EntityUid>(GetAddedGameRules());
        foreach (var rule in rules)
        {
            StartGameRule(rule);
        }
    }

        private void IncrementRoundNumber()
        {
            var playerIds = _playerGameStatuses.Keys.Select(player => player.UserId).ToArray();
            var serverName = _cfg.GetCVar(CCVars.AdminLogsServerName);

    // TODO FIXME AAAAAAAAAAAAAAAAAAAH THIS IS BROKEN
    // Task.Run as a terrible dirty workaround to avoid synchronization context deadlock from .Result here.
    // This whole setup logic should be made asynchronous so we can properly wait on the DB AAAAAAAAAAAAAH
    var task = Task.Run(async () =>
    {
        var server = await _dbEntryManager.ServerEntity;
        return await _db.AddNewRound(server, playerIds);
    });

        _taskManager.BlockWaitOnTask(task);
        RoundId = task.GetAwaiter().GetResult();
    }
}
