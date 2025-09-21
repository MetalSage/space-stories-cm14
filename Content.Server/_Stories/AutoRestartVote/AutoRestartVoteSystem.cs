using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared.GameTicking;
using Content.Server._RMC14.Rules;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Stories.AutoRestartVote;

public sealed class AutoRestartVoteSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distress = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ContainerSystem _containers = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    
    private TimeSpan _lastVoteTime = TimeSpan.Zero;
    private bool _toggled;
    private int _minMarinesForHijackBlock;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
        Subs.BuiEvents<DropshipNavigationComputerComponent>(DropshipHijackerUiKey.Key,
            subs => subs.Event<DropshipHijackerRefuse>(OnHijackerRefuse));

        _toggled = _cfg.GetCVar(SCCVars.AutoRestartEnabled);
        _cfg.OnValueChanged(SCCVars.AutoRestartEnabled, OnToggleChanged, true);
        
        Subs.CVar(_cfg, SCCVars.MinMarinesForHijackRefuseBlock, v => _minMarinesForHijackBlock = v, true);
    }

    private void RoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _lastVoteTime = TimeSpan.Zero;
        _toggled = _cfg.GetCVar(SCCVars.AutoRestartEnabled);
    }

    private void OnToggleChanged(bool value)
    {
        _toggled = value;
    }

    public bool ToggleAutoRestart()
    {
        _toggled = !_toggled;
        _cfg.SetCVar(SCCVars.AutoRestartEnabled, _toggled);
        return _toggled;
    }

    public override void Update(float frameTime)
    {
        if (!_toggled || _gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var curTime = _gameTiming.CurTime;
        var roundDuration = curTime - _gameTicker.RoundStartTimeSpan;
        var voteInterval = TimeSpan.FromMinutes(_cfg.GetCVar(SCCVars.AutoRestartVoteInterval));
        var roundMaxDuration = TimeSpan.FromHours(_cfg.GetCVar(SCCVars.AutoRestartRoundMaxHours));

        if (curTime - _lastVoteTime < voteInterval)
            return;

        if (roundDuration >= roundMaxDuration)
        {
            _lastVoteTime = curTime;
            _voteManager.CreateForceRestartVote();
        }
    }

    private void OnHijackerRefuse(Entity<DropshipNavigationComputerComponent> ent,
        ref DropshipHijackerRefuse args)
    {
        var marinesOnPlanet = 0;
        var marineQuery = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent, ActorComponent>();
        
        while (marineQuery.MoveNext(out var marineId, out _, out var mobState, out var transform, out var actor))
        {
            if (!_mobState.IsAlive(marineId, mobState))
                continue;
                
            if (_containers.IsEntityInContainer(marineId))
                continue;
                
            if (actor.PlayerSession == null)
                continue;
                
            if (_rmcPlanet.IsOnPlanet(transform))
                marinesOnPlanet++;
        }
        
        if (marinesOnPlanet >= _minMarinesForHijackBlock)
            return;

        _ui.CloseUi(ent.Owner, DropshipHijackerUiKey.Key, args.Actor);

        CMDistressSignalRuleComponent? distress = null; 
        var distressQuery = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        while (distressQuery.MoveNext(out var _, out var comp))
        {
            if (comp.Hijack)
                continue;

            distress = comp;
            break;
        }

        if (distress == null)
            return;

        _distress.EndRound(distress, DistressSignalRuleResult.MinorXenoVictory, 
            "st-distress-signal-minorxenovictory-refuse");
        RemCompDeferred<DropshipNavigationComputerComponent>(ent.Owner);
    }
}
