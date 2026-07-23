using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STSelfRechargingSolutionSystem))]
public sealed partial class STSelfRechargingSolutionComponent : Component
{
    [DataField, AutoNetworkedField]
    public string SolutionId = "Welder";

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "WeldingFuel";

    [DataField, AutoNetworkedField]
    public FixedPoint2 RechargeAmount = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan RechargeEvery = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NextRecharge;
}
