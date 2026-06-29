using Content.Shared._Stories.Hunter;

namespace Content.Client._Stories.Hunter;

public sealed class HunterSystem : SharedHunterSystem
{
    public bool IsHuntRound { get; private set; }
    public int AvailableBaseSlots { get; private set; }
    public int AvailableSponsorSlots { get; private set; }
    public string JoinFailReason { get; private set; } = string.Empty;

    public event Action? OnLobbyStateUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<HunterLobbyStateEvent>(OnLobbyStateReceived);
    }

    public void RequestLobbyState()
    {
        RaiseNetworkEvent(new RequestHunterLobbyStateEvent());
    }

    private void OnLobbyStateReceived(HunterLobbyStateEvent message)
    {
        IsHuntRound = message.IsHuntRound;
        AvailableBaseSlots = message.AvailableBaseSlots;
        AvailableSponsorSlots = message.AvailableSponsorSlots;
        JoinFailReason = message.Reason;

        OnLobbyStateUpdated?.Invoke();
    }

    public bool CanJoin(bool isSponsor, out string reason)
    {
        if (!IsHuntRound)
        {
            reason = string.IsNullOrEmpty(JoinFailReason)
                ? Loc.GetString("st-hunter-not-hunting-ground")
                : JoinFailReason;
            return false;
        }

        if (AvailableBaseSlots > 0)
        {
            reason = string.Empty;
            return true;
        }

        if (isSponsor && AvailableSponsorSlots > 0)
        {
            reason = string.Empty;
            return true;
        }

        reason = string.IsNullOrEmpty(JoinFailReason) ? Loc.GetString("st-hunter-slots-full") : JoinFailReason;
        return false;
    }
}
