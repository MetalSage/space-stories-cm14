using Content.Shared._Stories.Vehicle.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedVehicleSystem))]
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
    public Angle LastRotation = Angle.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastBlockedTime;

    [DataField]
    public float BlockedCooldown = 0.05f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? MovementSound = new SoundPathSpecifier("/Audio/_Stories/tank_driving.ogg");

    [DataField, AutoNetworkedField]
    public int SoundEvery = 25;

    [DataField, AutoNetworkedField]
    public float SoundSteps;

    [DataField, AutoNetworkedField]
    public EntityUid? AudioStream;
}
