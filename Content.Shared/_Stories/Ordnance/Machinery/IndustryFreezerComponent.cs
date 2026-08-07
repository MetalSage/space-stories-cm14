using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;
using System;

namespace Content.Shared._Stories.Ordnance.Machinery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IndustryFreezerComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextProcessTime;

    [DataField, AutoNetworkedField]
    public TimeSpan ProcessInterval = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public int MaxContainersPerInterval = 3;

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> InputReagent1 = "RMCFormaldehyde";

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> InputReagent2 = "Water";

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> OutputReagent = "RMCParaformaldehyde";
}
