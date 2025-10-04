using Content.Shared._RMC14.Stun;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShakeOnHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Shakes = 1;

    [DataField, AutoNetworkedField]
    public int Strength = 1;

    [DataField, AutoNetworkedField]
    public RMCSizes Size = RMCSizes.Xeno;

    [DataField, AutoNetworkedField]
    public float ShakeRange = 1f;
}
