using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
[Access(typeof(SharedStabilizingCrystalSystem))]
public sealed partial class StabilizingCrystalComponent : Component
{
    [DataField]
    public float EnergyToRestore = 250f;

    [DataField] [AutoNetworkedField]
    public bool Used;

    [DataField]
    public string UsedIconState = "stabilizing_crystal_used";

    [DataField]
    public SoundSpecifier UseSound = new SoundPathSpecifier("/Audio/Items/crystal_resonating.ogg");
}
