using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Placeable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMPickupFlagComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Item = "AlphaFlag";

    [DataField, AutoNetworkedField]
    public TimeSpan TakeDelay = TimeSpan.FromSeconds(1);
}
