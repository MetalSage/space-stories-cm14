using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent]
public sealed partial class APCMapComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCEntityGridComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? APC;
}
