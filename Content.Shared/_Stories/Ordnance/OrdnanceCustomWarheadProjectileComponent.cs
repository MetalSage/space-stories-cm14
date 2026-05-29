using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Ordnance;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrdnanceCustomWarheadProjectileComponent : Component
{
    [DataField] public EntityUid LauncherUid;
    [DataField] public EntityUid WarheadUid;
}
