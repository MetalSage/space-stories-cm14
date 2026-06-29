using Content.Shared.Players.JobWhitelist;

namespace Content.Client._Stories.Players.JobWhitelist;

public sealed class JobWhitelistSystem : EntitySystem
{
    public HashSet<string> WhitelistedJobs { get; } = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<JobWhitelistUpdatedEvent>(OnJobWhitelistUpdated);
    }

    private void OnJobWhitelistUpdated(JobWhitelistUpdatedEvent ev)
    {
        WhitelistedJobs.Clear();
        foreach (var job in ev.Whitelist)
        {
            WhitelistedJobs.Add(job);
        }
    }

    public bool IsWhitelisted(string jobId)
    {
        return WhitelistedJobs.Contains(jobId);
    }
}
