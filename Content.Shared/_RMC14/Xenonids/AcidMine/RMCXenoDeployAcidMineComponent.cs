using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.AcidMine;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RMCXenoDeployAcidMineSystem))]
public sealed partial class RMCXenoDeployAcidMineComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Empowered;

    [DataField, AutoNetworkedField]
    public EntProtoId PrototypeId = "XenoAcidMine";

    [DataField, AutoNetworkedField]
    public EntProtoId EmpoweredPrototypeId = "EmpoweredXenoAcidMine";

    [DataField, AutoNetworkedField]
    public LocId EmpoweredMessage = "rmc-xeno-deploy-acid-mine-empowered";
}
