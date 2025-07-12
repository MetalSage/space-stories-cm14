using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public abstract partial class BaseVehicleSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehiclePilotSeatComponent : BaseVehicleSeatComponent
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;
}