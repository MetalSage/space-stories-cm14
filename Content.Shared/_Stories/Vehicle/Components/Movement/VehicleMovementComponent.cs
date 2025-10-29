using Content.Shared._Stories.Vehicle.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedVehicleSystem), typeof(SharedMoverController))]
public sealed partial class VehicleMovementComponent : Component
{
    [DataField, AutoNetworkedField]
    public int CurrentMomentum;

    [DataField, AutoNetworkedField]
    public int MaxMomentum = 2;

    [DataField, AutoNetworkedField]
    public int MinimumStepsForMomentum = 2;

    [DataField]
    public float StepIncrement = 1.0f;

    [DataField, AutoNetworkedField]
    public float DistanceMoved;

    [DataField, AutoNetworkedField]
    public int Steps;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastMovementTime;

    [DataField]
    public TimeSpan MomentumDecayDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float MomentumTurnLossFactor = 0.5f;

    [DataField]
    public float MaxMomentumSpeedBonus = 0.75f;

    [DataField]
    public float SpeedPerMomentum = 0.15f;

    [DataField, AutoNetworkedField]
    public bool Blocked;

    [DataField, AutoNetworkedField]
    public Direction LastMoveDirection;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? MovementSound = new SoundPathSpecifier("/Audio/_Stories/tank_driving.ogg", AudioParams.Default.WithVolume(-4));

    [DataField, AutoNetworkedField]
    public EntityUid? AudioStream;
}
