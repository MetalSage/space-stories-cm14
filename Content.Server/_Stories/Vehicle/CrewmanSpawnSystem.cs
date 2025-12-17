using Content.Server._RMC14.Rules;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stories.Vehicle;

public sealed class CrewmanSpawnSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private int _vehicleCrewmanSlots = 2;
    private static ProtoId<JobPrototype> _crewmanJob = "CMVehicleCrewman";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning, after: new[] { typeof(CMDistressSignalRuleSystem) });
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        if (!_prototypes.TryIndex(_crewmanJob, out var jobProto))
            return;

        if (jobProto.MinPlayers > 0 && _player.PlayerCount < jobProto.MinPlayers)
            return;

        var spawners = new List<EntityUid>();
        var spawnerQuery = EntityQueryEnumerator<SpawnPointComponent>();
        while (spawnerQuery.MoveNext(out var spawnerId, out var spawnPoint))
        {
            if (spawnPoint.Job == _crewmanJob)
                spawners.Add(spawnerId);
        }

        if (spawners.Count == 0)
        {
            Log.Warning($"No spawn points found for crewman job: {_crewmanJob}");
            return;
        }

        var priorities = Enum.GetValues<JobPriority>().Length;
        var candidates = new List<ICommonSession>[priorities];
        for (var i = 0; i < candidates.Length; i++)
        {
            candidates[i] = new List<ICommonSession>();
        }

        foreach (var player in ev.PlayerPool)
        {
            if (!ev.Profiles.TryGetValue(player.UserId, out var profile))
                continue;

            if (profile.JobPriorities.TryGetValue(_crewmanJob, out var priority) &&
                priority > JobPriority.Never)
            {
                candidates[(int)priority].Add(player);
            }
        }

        var spawned = 0;
        for (var i = candidates.Length - 1; i >= 0 && spawned < _vehicleCrewmanSlots; i--)
        {
            var candidateList = candidates[i];

            while (candidateList.Count > 0 && spawned < _vehicleCrewmanSlots)
            {
                var player = _random.PickAndTake(candidateList);

                if (!SpawnJob(player, _crewmanJob, jobProto, spawners, ev))
                    return;

                ev.PlayerPool.Remove(player);
                spawned++;
            }
        }
    }

    private bool SpawnJob(
        ICommonSession player,
        ProtoId<JobPrototype> jobId,
        JobPrototype jobProto,
        List<EntityUid> spawners,
        RulePlayerSpawningEvent ev)
    {
        if (spawners.Count == 0)
            return false;

        var spawner = _random.PickAndTake(spawners);

        _gameTicker.PlayerJoinGame(player);
        var profile = _gameTicker.GetPlayerProfile(player);
        var coordinates = _transform.GetMoverCoordinates(spawner);
        var mob = _stationSpawning.SpawnPlayerMob(coordinates, jobId, profile, null);

        if (!_mind.TryGetMind(player.UserId, out var mind))
            mind = _mind.CreateMind(player.UserId);

        _mind.TransferTo(mind.Value, mob);
        _roles.MindAddJobRole(mind.Value, jobPrototype: jobId);

        var spawnEv = new PlayerSpawnCompleteEvent(
            mob,
            player,
            jobId,
            false,
            true,
            0,
            default,
            profile
        );
        RaiseLocalEvent(mob, spawnEv, true);

        return true;
    }
}
