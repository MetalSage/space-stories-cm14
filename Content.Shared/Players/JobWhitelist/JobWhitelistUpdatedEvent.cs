using Robust.Shared.Serialization;

namespace Content.Shared.Players.JobWhitelist;

[Serializable, NetSerializable]
public sealed class JobWhitelistUpdatedEvent : EntityEventArgs
{
    public HashSet<string> Whitelist = new();
}
