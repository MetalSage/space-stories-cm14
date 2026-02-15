using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Medical.Components.Steps;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class STSurgeryStepMendComponent : Component
{
    [DataField] [AutoNetworkedField]
    public float HealAmount = 65f;

    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(30);
}
