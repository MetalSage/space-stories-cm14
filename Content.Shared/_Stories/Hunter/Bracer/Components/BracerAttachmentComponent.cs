using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class BracerAttachmentComponent : Component
{
    [DataField] [AutoNetworkedField]
    public EntityUid? AttachedWeapon;

    [DataField]
    public SoundSpecifier? DeploySound;

    [DataField]
    public SoundSpecifier? RetractSound;
}
