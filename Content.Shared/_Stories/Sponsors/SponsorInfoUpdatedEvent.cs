using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Sponsors;

/// <summary>
/// Network event to send sponsor info to the client.
/// </summary>
[Serializable] [NetSerializable]
public sealed class SponsorInfoUpdatedEvent : EntityEventArgs
{
    public SponsorInfo? Info;
}
