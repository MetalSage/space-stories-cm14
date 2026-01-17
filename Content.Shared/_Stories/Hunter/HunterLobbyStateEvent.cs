using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter;

[Serializable] [NetSerializable]
public sealed class HunterLobbyStateEvent : EntityEventArgs
{
    public int AvailableBaseSlots;
    public int AvailableSponsorSlots;
    public bool IsHuntRound;
    public string Reason = string.Empty;
}

[Serializable] [NetSerializable]
public sealed class RequestHunterLobbyStateEvent : EntityEventArgs
{
}
