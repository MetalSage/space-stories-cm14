using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class BracerAttachedWeaponComponent : Component
{
    [DataField] [AutoNetworkedField]
    public NetEntity? Bracer;
}
