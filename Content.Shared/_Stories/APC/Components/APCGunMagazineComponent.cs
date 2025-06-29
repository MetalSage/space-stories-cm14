using Robust.Shared.GameStates;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent]
public sealed partial class APCGunMagazineComponent : Component
{
    [DataField]
    public string MagazineType;
}