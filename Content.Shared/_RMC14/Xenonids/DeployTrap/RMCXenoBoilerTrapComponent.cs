using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.DeployTrap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RMCXenoDeployTrapsSystem))]
public sealed partial class RMCXenoBoilerTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Activated;

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Ignore = [];

    [DataField, AutoNetworkedField]
    public TimeSpan RootDuration = TimeSpan.FromSeconds(1.75f);
}
