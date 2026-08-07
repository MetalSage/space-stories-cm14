using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Stories.Sponsors;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared.Players.JobWhitelist;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Serilog;

namespace Content.Server.Players.JobWhitelist;

public sealed class JobWhitelistManager : IPostInjectInit
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly SponsorsManager _sponsors = default!; // Stories-Sponsor
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _whitelists = new();

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var whitelists = await _db.GetJobWhitelists(session.UserId, cancel);
        cancel.ThrowIfCancellationRequested();
        _whitelists[session.UserId] = whitelists.ToHashSet();
    }

    private void FinishLoad(ICommonSession session)
    {
        SendJobWhitelist(session);
    }

    private void ClientDisconnected(ICommonSession session)
    {
        _whitelists.Remove(session.UserId);
    }

    public async void AddWhitelist(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (_whitelists.TryGetValue(player, out var whitelists))
            whitelists.Add(job);

        await _db.AddJobWhitelist(player, job);

        if (_player.TryGetSessionById(player, out var session))
            SendJobWhitelist(session);
    }

    public bool IsAllowed(ICommonSession session, ProtoId<JobPrototype> job)
    {
        _sponsors.TryGetInfo(session.UserId, out var sponsorData);

        if (!_config.GetCVar(CCVars.GameRoleWhitelist) ||
            sponsorData?.WhitelistRoleTimeBypass == true) // Stories-Sponsor
            return true;

        if (!_prototypes.TryIndex(job, out var jobPrototype))
            return true;

        if (!jobPrototype.Whitelisted)
            return true;

        if (IsWhitelisted(session.UserId, job))
            return true;

        if (jobPrototype.WhitelistParent != null)
            return IsAllowed(session, jobPrototype.WhitelistParent.Value);

        return false;
    }

    public bool IsWhitelisted(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (!_whitelists.TryGetValue(player, out var whitelists))
        {
            Log.Error("Unable to check if player {Player} is whitelisted for {Job}. Stack trace:\\n{StackTrace}",
                player,
                job,
                Environment.StackTrace);
            return false;
        }

        return whitelists.Contains(job);
    }

    public async void RemoveWhitelist(NetUserId player, ProtoId<JobPrototype> job)
    {
        _whitelists.GetValueOrDefault(player)?.Remove(job);
        await _db.RemoveJobWhitelist(player, job);

        if (_player.TryGetSessionById(new NetUserId(player), out var session))
            SendJobWhitelist(session);
    }

    public void SendJobWhitelist(ICommonSession player)
    {
        var ev = new JobWhitelistUpdatedEvent
        {
            Whitelist = _whitelists.GetValueOrDefault(player.UserId) ?? new HashSet<string>()
        };

        _entityManager.EntityNetManager.SendSystemNetworkMessage(ev, player.Channel);
    }

    void IPostInjectInit.PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
