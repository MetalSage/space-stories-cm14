using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Attachables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DirectionalFireComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle MaxFireAngle = Angle.FromDegrees(45);
}
