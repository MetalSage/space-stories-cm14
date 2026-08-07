using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
[Access(typeof(BracerSystem))]
public sealed partial class BracerSelfDestructingComponent : Component
{
    [DataField] [AutoNetworkedField]
    public EntityUid? CountdownSoundStream;

    [DataField] [AutoNetworkedField]
    public TimeSpan ExplosionTime;

    [DataField] [AutoNetworkedField]
    public EntityUid Wearer;
}
