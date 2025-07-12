using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCGunnerSeatComponent : BaseVehicleSeatComponent
{
    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    [DataField, AutoNetworkedField]
    public EntProtoId? Action = "STAPCHardpointMenuAction";
}
