using Content.Shared._Stories.Vehicle.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleSystem))]
public sealed partial class VehicleStructureTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public VehicleClass MinimumClassToDestroy = VehicleClass.Light;

    [DataField, AutoNetworkedField]
    public float MomentumLossFactor = 0.5f;

    [DataField, AutoNetworkedField]
    public bool StopOnFail = false;

    [DataField, AutoNetworkedField]
    public int DamageToVehicleOnFail = 5;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? DestroySound = new SoundPathSpecifier("/Audio/_Stories/metal_crash.ogg", AudioParams.Default.WithVolume(20));
}
