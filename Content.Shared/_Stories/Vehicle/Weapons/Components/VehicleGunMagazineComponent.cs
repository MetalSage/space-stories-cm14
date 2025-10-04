using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleGunMagazineComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string MagazineType = string.Empty;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId ProjectilePrototype;

    [DataField, AutoNetworkedField]
    public int Shots = 10;

    [DataField, AutoNetworkedField]
    public int Capacity = 10;
}

[Serializable, NetSerializable]
public enum VehicleAmmoVisuals
{
    Layer,
}

[Serializable, NetSerializable]
public enum VehicleAmmoState
{
    Fill,
    Empty,
}
