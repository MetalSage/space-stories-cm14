using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent]
public sealed partial class APCMapComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCEntityGridComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? APC;
}
