using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.DeployTrap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCXenoDeployTrapsSystem))]
public sealed partial class RMCXenoDeployTrapsComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Additional = 2;

    [DataField, AutoNetworkedField]
    public EntProtoId PrototypeId = "XenoBoilerTrap";

    [DataField, AutoNetworkedField]
    public EntProtoId EmpoweredPrototypeId = "EmpoweredXenoBoilerTrap";
}
