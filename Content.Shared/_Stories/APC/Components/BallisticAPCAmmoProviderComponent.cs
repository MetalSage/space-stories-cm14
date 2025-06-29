using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BallisticAPCAmmoProviderComponent : AmmoProviderComponent
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField, AutoNetworkedField]
    public int Shots = 10;

    [DataField]
    public int Capacity = 10;

    [DataField]
    public string AmmoContainerId = "ammo-storage";

    [DataField]
    public string AmmoType = string.Empty;
}