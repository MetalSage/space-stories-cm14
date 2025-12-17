using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunArcRestrictionComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle MaxAngleDeviation = Angle.FromDegrees(45);

    [DataField, AutoNetworkedField]
    public Angle ArcDirection = Angle.Zero;

    [DataField]
    public string? RestrictionMessage = "st-vehicle-gun-arc-restriction";
}
