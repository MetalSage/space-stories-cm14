using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.DeployTrap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCXenoTrappedComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 DamageBonus = 1.75f;
}
