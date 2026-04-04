using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Rules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMDistressSignalRuleComponent : Component
{
    /// <summary>
    /// Squads available for normal round start and late join squad assignment.
    /// This list can be replaced at round start by the low population configuration below.
    /// </summary>
    [DataField]
    public List<EntProtoId> SquadIds = new() { "SquadAlpha", "SquadBravo", "SquadCharlie", "SquadDelta" };

    [DataField]
    public List<EntProtoId> ExtraSquadIds = new() { "SquadIntel", "SquadFORECON" };

    /// <summary>
    /// If the round starts at or below this many players, only <see cref="LowPopSquadIds"/> are used.
    /// Adjust this value if command wants a different low population threshold.
    /// </summary>
    [DataField]
    public int LowPopSquadThreshold = 50;

    /// <summary>
    /// Squads used when the round starts at low population.
    /// </summary>
    [DataField]
    public List<EntProtoId> LowPopSquadIds = new() { "SquadAlpha", "SquadBravo" };

    /// <summary>
    /// When enabled, low population rounds clamp the global Squad Leader job slots separately
    /// from the usual squad role scaling. This is useful when only a subset of squads is active.
    /// </summary>
    [DataField]
    public bool LimitSquadLeadersOnLowPop = true;

    /// <summary>
    /// Global Squad Leader slot cap applied on low population rounds.
    /// </summary>
    [DataField]
    public int LowPopMaxSquadLeaders = 2;

    /// <summary>
    /// Job id affected by <see cref="LimitSquadLeadersOnLowPop"/>.
    /// Exposed so balance can be adjusted without changing system logic.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype> LowPopSquadLeaderJob = "CMSquadLeader";

    /// <summary>
    /// Per-squad role caps applied to active low population squads after they are spawned.
    /// This lets command keep key squad roles available on Alpha/Bravo without touching normal rounds.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> LowPopSquadRoleOverrides = new()
    {
        { "CMSquadLeader", 1 },
        { "CMFireteamLeader", 4 },
        { "CMWeaponsSpecialist", 2 },
        { "CMSmartGunOperator", 2 },
        { "CMCombatTech", 6 },
        { "CMHospitalCorpsman", 8 },
    };

    [DataField]
    public bool LowPopSquadsActive;

    [DataField]
    public Dictionary<EntProtoId, EntityUid> Squads = new();

    [DataField]
    public EntityUid? XenoMap;

    [DataField]
    public EntProtoId HiveId = "CMXenoHive";

    [DataField]
    public EntityUid Hive;

    // TODO RMC14
    [DataField]
    public bool Hijack;

    [DataField]
    public ProtoId<JobPrototype> QueenJob = "CMXenoQueen";

    [DataField]
    public EntProtoId QueenEnt = "CMXenoQueen";

    [DataField]
    public ProtoId<JobPrototype> XenoSelectableJob = "CMXenoSelectableXeno";

    [DataField]
    public EntProtoId LarvaEnt = "CMXenoLarva";

    [DataField]
    public EntProtoId<IFFFactionComponent> MarineFaction = "FactionMarine";

    [DataField]
    public EntProtoId<IFFFactionComponent> SurvivorFaction = "FactionSurvivor";

    [DataField, AutoPausedField]
    public TimeSpan? QueenDiedCheck;

    [DataField]
    public TimeSpan QueenDiedDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public DistressSignalRuleResult? Result;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextCheck;

    [DataField]
    public TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan? AbandonedAt;

    [DataField]
    public TimeSpan AbandonedDelay = TimeSpan.FromMinutes(5);

    [DataField]
    public SoundSpecifier HijackSong = new SoundCollectionSpecifier("RMCHijack", AudioParams.Default.WithVolume(-8));

    [DataField]
    public bool HijackSongPlayed;

    [DataField]
    public SoundSpecifier MajorMarineAudio = new SoundCollectionSpecifier("RMCMarineMajor");

    [DataField]
    public SoundSpecifier MinorMarineAudio = new SoundCollectionSpecifier("RMCMarineMinor");

    [DataField]
    public SoundSpecifier MajorXenoAudio = new SoundCollectionSpecifier("RMCXenoMajor");

    [DataField]
    public SoundSpecifier MinorXenoAudio = new SoundCollectionSpecifier("RMCXenoMinor");

    // TODO RMC14
    // [DataField]
    // public SoundSpecifier AllDiedAudio = new SoundCollectionSpecifier("CMAllDied");

    [DataField]
    public EntProtoId? LandingZoneGas = "RMCLandingZoneGas";

    [DataField]
    public ProtoId<JobPrototype> CivilianSurvivorJob = "CMSurvivor";

    [DataField]
    public List<(ProtoId<JobPrototype> Job, int Amount)> SurvivorJobs = new()
    {
        ("CMSurvivorEngineer", 4),
        ("CMSurvivorDoctor", 3),
        ("CMSurvivorSecurity", 2),
        ("CMSurvivorCorporate", 2),
        ("CMSurvivorScientist", 2),
        ("CMSurvivor", -1),
    };

    [DataField]
    public List<ProtoId<JobPrototype>> IgnoreMaximumSurvivorJobs = new() { "RMCSurvivorCommandingOfficer" };

    [DataField]
    public Dictionary<ProtoId<JobPrototype>, List<(ProtoId<JobPrototype> Variant, int Amount)>>? SurvivorJobVariants;

    [DataField]
    public Dictionary<ProtoId<JobPrototype>, ProtoId<JobPrototype>>? SurvivorJobOverrides;

    [DataField]
    public Dictionary<ProtoId<JobPrototype>, List<(ProtoId<JobPrototype> Special, int Amount)>>? SurvivorJobVariantScenarios;

    [DataField]
    public TimeSpan AresGreetingDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier AresGreetingAudio = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/ares_online.ogg");

    [DataField]
    public bool AresGreetingDone;

    [DataField]
    public TimeSpan AresMapDelay = TimeSpan.FromSeconds(20);

    [DataField]
    public bool AresMapDone;

    [DataField]
    public TimeSpan? StartTime;

    [DataField]
    public bool ScalingDone;

    [DataField]
    public double Scale = 1;

    [DataField]
    public double MaxScale = 1;

    [DataField]
    public TimeSpan? EndAtAllClear;

    [DataField]
    public TimeSpan AllClearEndDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public ProtoId<RadioChannelPrototype> AllClearChannel = "MarineCommand";

    [DataField]
    public TimeSpan RoundEndCheckDelay = TimeSpan.FromMinutes(1);

    [DataField]
    public ResPath Thunderdome = new("/Maps/_RMC14/thunderdome.yml");

    public List<string> AuxiliaryMaps = new() {
        "/Maps/_RMC14/admin_fax.yml"
    };

    [DataField]
    public ProtoId<JobPrototype> XenoSurvivorCorpseJob = "CMSurvivorHost";

    [DataField]
    public TimeSpan XenoSurvivorCorpseBurstDelay = TimeSpan.FromSeconds(0);

    [DataField]
    public TimeSpan? ForceEndAt;

    [DataField]
    public LocId? CustomRoundEndMessage;

    [DataField]
    public bool SpawnPlanet = true;

    [DataField]
    public bool SpawnSurvivors = true;

    [DataField]
    public bool SpawnXenos = true;

    [DataField]
    public bool DoJobSlotScaling = true;

    [DataField]
    public bool AutoEnd = true;

    [DataField]
    public bool StartARESAnnouncements = true;

    [DataField]
    public bool Bioscan = true;

    [DataField]
    public bool SetHunger = true;

    [DataField]
    public bool RequireXenoPlayers = true;

    [DataField]
    public bool QueenBoostRemoved;

    [DataField]
    public bool RecalculatedPower;

    [DataField]
    public bool Nuked; // Stories-Nuke
}
