using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCGunnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? APC;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}