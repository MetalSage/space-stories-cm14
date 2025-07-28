using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoBoxerKnockoutComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan AuraDuration = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public float AuraOutline = 1f;

    [DataField, AutoNetworkedField]
    public float MaxKnockout = 15f;
}
