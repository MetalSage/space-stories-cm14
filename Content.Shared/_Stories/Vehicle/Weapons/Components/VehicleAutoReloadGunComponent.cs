using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleAutoReloadGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ReloadTime = 10f;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan? ReloadEndTime;

    [ViewVariables, AutoNetworkedField]
    public bool IsReloading;
}
