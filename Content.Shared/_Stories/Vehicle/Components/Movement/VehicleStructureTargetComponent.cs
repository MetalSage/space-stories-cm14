using Content.Shared._Stories.Vehicle.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleSystem))]
public sealed partial class VehicleStructureTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public VehicleClass MinimumClassToDestroy = VehicleClass.Medium;

    [DataField, AutoNetworkedField]
    public float MomentumLossFactor = 0.5f;

    [DataField, AutoNetworkedField]
    public bool StopOnFail = false;

    [DataField, AutoNetworkedField]
    public int DamageToVehicleOnFail = 5;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? DestroySound = new SoundPathSpecifier("/Audio/Effects/metal_crash.ogg");
}
