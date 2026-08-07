using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Medical.Components.Steps;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class STSurgeryStepClampComponent : Component
{
    [DataField] [AutoNetworkedField]
    public float HealAmount = 125f;
}
