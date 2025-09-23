using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared._Stories.Vehicle.Systems;

namespace Content.Shared._Stories.Attachables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(VehicleAttachableHolderSystem), typeof(SharedVehicleSystem))]
public sealed partial class VehicleAttachableComponent : Component
{
    [DataField, AutoNetworkedField]
    public float AttachDoAfter = 1.5f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? AttachSound = new SoundPathSpecifier("/Audio/_RMC14/Attachable/attachment_add.ogg", AudioParams.Default.WithVolume(-6.5f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier? DetachSound = new SoundPathSpecifier("/Audio/_RMC14/Attachable/attachment_remove.ogg", AudioParams.Default.WithVolume(-5.5f));

    [DataField, AutoNetworkedField]
    public string? Description = "test";

    [DataField, AutoNetworkedField]
    public string? Stats = "test";

    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2.Zero;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Health = FixedPoint2.New(100);
}
