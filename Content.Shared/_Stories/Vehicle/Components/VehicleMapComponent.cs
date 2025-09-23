using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleMapComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleGridComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Vehicle;
}
