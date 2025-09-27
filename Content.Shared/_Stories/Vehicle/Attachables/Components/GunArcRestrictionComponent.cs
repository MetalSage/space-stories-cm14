using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Systems;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunArcRestrictionComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle MaxAngleDeviation = Angle.FromDegrees(45);

    [DataField, AutoNetworkedField]
    public string? RestrictionMessage = "The target is not within your firing arc!";
}