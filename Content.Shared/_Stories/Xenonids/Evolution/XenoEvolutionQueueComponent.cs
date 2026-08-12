using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Evolution;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoEvolutionQueueComponent : Component
{
    [DataField]
    public TimeSpan TierEnteredAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? OfferedUntil;

    [DataField]
    public int OfferedTier;

    [DataField]
    public TimeSpan? EvolvingUntil;

    [DataField]
    public TimeSpan? PassedUntil;
}
