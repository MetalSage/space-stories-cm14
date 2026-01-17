using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Players.JobWhitelist;

public sealed class JobWhitelistSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public bool IsWhitelisted(string jobId)
    {
        if (_prototypes.TryIndex<JobPrototype>(jobId, out var job))
            return true;

        return true;
    }
}
