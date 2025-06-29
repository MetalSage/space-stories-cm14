using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCGunMagazineComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Prototype;
}
