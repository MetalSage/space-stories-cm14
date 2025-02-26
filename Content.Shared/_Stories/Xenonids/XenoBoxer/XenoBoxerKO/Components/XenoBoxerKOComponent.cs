using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoBoxerKOComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan AuraDuration = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public Color? AuraColor;

    [DataField, AutoNetworkedField]
    public float KOIncreasePerMeleeHit = 0.5f;

    [DataField, AutoNetworkedField]
    public float MaxKO = 15f;
}
