using Content.Shared._RMC14.Attachable.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Attachable.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(AttachableAimedShotSystem))]
public sealed partial class AttachableAimedShotHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Range = 15;
}
