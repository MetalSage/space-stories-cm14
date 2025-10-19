using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Attachables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementAttachableComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WalkSpeed = 0f;

    [DataField, AutoNetworkedField]
    public float SprintSpeed = 0f;
}
