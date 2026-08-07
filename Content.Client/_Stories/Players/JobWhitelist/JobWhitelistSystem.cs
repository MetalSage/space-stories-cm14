using Content.Client.Players.PlayTimeTracking;
using Content.Shared.Players.JobWhitelist;
using Robust.Shared.Network;

namespace Content.Client._Stories.Players.JobWhitelist;

public sealed class JobWhitelistSystem : EntitySystem
{
    [Dependency] private readonly JobRequirementsManager _requirementsManager = default!;
    
    private readonly HashSet<string> _whitelist = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<JobWhitelistUpdatedEvent>(OnWhitelistUpdated);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _whitelist.Clear();
    }

    private void OnWhitelistUpdated(JobWhitelistUpdatedEvent ev)
    {
        _whitelist.Clear();
        _whitelist.UnionWith(ev.Whitelist);

        _requirementsManager.NotifyUpdated();
    }

    public bool IsWhitelisted(string jobId)
    {
        return _whitelist.Contains(jobId);
    }
}
