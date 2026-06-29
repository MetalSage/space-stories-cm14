using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Medical.Components.Steps;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class STSurgeryStepStabilizeComponent : Component
{
    [DataField] [AutoNetworkedField]
    public float HealAmount = 40f;

    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(30);
}
