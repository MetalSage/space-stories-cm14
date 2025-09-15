using Content.Shared._RMC14.Attachable.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Attachable.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(AttachableAimedShotSystem))]
public sealed partial class AttachableAimedShotComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan AimedShotCooldown = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public int Range = 15;

    [DataField, AutoNetworkedField]
    public float AimDuration = 4.25f;

    [DataField, AutoNetworkedField]
    public double AimDistanceDifficulty = 0.1;
}
