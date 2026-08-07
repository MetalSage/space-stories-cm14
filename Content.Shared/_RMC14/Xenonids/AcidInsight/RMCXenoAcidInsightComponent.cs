using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.AcidInsight;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RMCXenoAcidInsightSystem))]
public sealed partial class RMCXenoAcidInsightComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Empowered;

    [DataField, AutoNetworkedField]
    public int Stacks;

    [DataField, AutoNetworkedField]
    public int MaxStacks = 9;

    [DataField, AutoNetworkedField]
    public LocId EmpoweredMessage = "rmc-xeno-insight-empowered";
}
