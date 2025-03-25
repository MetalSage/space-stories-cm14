using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.DeployTrap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RMCXenoDeployTrapsSystem))]
public sealed partial class RMCXenoDeployedTrapComponent : Component
{
    [ViewVariables]
    public TimeSpan CurrentRootDuration => Empowered ? EmpoweredRootDuration : RootDuration;

    [DataField, AutoNetworkedField]
    public bool Empowered;

    [DataField, AutoNetworkedField]
    public TimeSpan RootDuration = TimeSpan.FromSeconds(1.75f);

    [DataField, AutoNetworkedField]
    public TimeSpan EmpoweredRootDuration = TimeSpan.FromSeconds(3f);
}
