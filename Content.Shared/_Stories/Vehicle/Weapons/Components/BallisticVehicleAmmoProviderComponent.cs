using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BallisticVehicleAmmoProviderComponent : AmmoProviderComponent
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField, AutoNetworkedField]
    public int Shots = 10;

    [DataField, AutoNetworkedField]
    public int InitialShots;

    [DataField, AutoNetworkedField]
    public bool AutoReload = false;

    [DataField, AutoNetworkedField]
    public TimeSpan ReloadCooldown = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public bool IsReloading = false;

    [DataField, AutoNetworkedField]
    public TimeSpan? ReloadEndTime;

    [DataField]
    public int Capacity = 10;

    [DataField]
    public string AmmoContainerId = "ammo-storage";
}
