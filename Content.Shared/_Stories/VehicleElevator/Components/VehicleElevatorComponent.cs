using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Shared._Stories.VehicleElevator.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedVehicleElevatorSystem))]
public sealed partial class VehicleElevatorComponent : Component
{
    [DataField]
    public float Radius = 2;

    [DataField, AutoNetworkedField]
    public RequisitionsElevatorMode Mode;

    [DataField]
    public RequisitionsElevatorMode? NextMode;

    [DataField, AutoNetworkedField]
    public bool Busy;

    [DataField, AutoNetworkedField]
    public EntProtoId? CurrentOrder;

    [DataField, AutoNetworkedField]
    public string LoweredState = "supply_elevator_lowered";

    [DataField, AutoNetworkedField]
    public string LoweringState = "supply_elevator_lowering";

    [DataField, AutoNetworkedField]
    public string RaisedState = "supply_elevator_raised";

    [DataField, AutoNetworkedField]
    public string RaisingState = "supply_elevator_raising";

    public EntityUid? Audio;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ToggledAt;

    [DataField]
    public TimeSpan ToggleDelay = TimeSpan.FromSeconds(17);

    [DataField]
    public TimeSpan RaiseSoundDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan RaiseDelay = TimeSpan.FromSeconds(12.5);

    [DataField]
    public TimeSpan LowerDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan LowerSoundDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier? LoweringSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_lowering.ogg");

    [DataField]
    public SoundSpecifier? RaisingSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_raising.ogg");

    public object? LoweringAnimation;

    public object? RaisingAnimation;
}
