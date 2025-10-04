using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Content.Shared.Movement.Components;
using Robust.Shared.GameStates;
using Content.Shared.Movement.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier SoundCollection = new SoundPathSpecifier("/Audio/_Stories/tank_driving.ogg");

    [DataField, AutoNetworkedField]
    public AudioParams AudioParams = AudioParams.Default.WithVolume(-5f);

    [DataField, AutoNetworkedField]
    public TimeSpan NextSoundTime;

    [DataField, ViewVariables]
    public TimeSpan NextDirectionChangeSoundTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public float SoundInterval = 3f;

    [DataField, AutoNetworkedField]
    public bool IsCurrentlyMoving;

    [ViewVariables, AutoNetworkedField]
    public MoveButtons LastMoveButtons = MoveButtons.None;

}
