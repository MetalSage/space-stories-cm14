using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KnockoutLabelComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan? ExpiresAt = TimeSpan.FromSeconds(5);
}
