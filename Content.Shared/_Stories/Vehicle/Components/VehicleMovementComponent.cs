using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleMovementComponent : Component
{
    [DataField("soundCollection", required: true), AutoNetworkedField]
    public SoundSpecifier SoundCollection = new SoundPathSpecifier("/Audio/_Stories/tank_driving.ogg");

    [DataField, AutoNetworkedField]
    public AudioParams AudioParams = AudioParams.Default.WithVolume(-5f);

    [DataField, AutoNetworkedField]
    public TimeSpan NextSoundTime;

    [DataField, AutoNetworkedField]
    public float SoundInterval = 3f;

    [DataField, AutoNetworkedField]
    public bool IsCurrentlyMoving;
}