using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._Stories.Vehicle.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Attachables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(VehicleAttachableHolderSystem), typeof(SharedVehicleSystem), typeof(AttachableModifiersSystem))]
public sealed partial class VehicleAttachableComponent : Component
{
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
    public FixedPoint2 MaxHealth = FixedPoint2.New(500);

    [DataField, AutoNetworkedField]
    public ProtoId<HardpointTypePrototype> HardpointType = "HDPT_PRIMARY";

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> Skill;

    [DataField, AutoNetworkedField]
    public int SkillLevel = 2;

    [DataField, AutoNetworkedField]
    public float DamageMult = 0f;

    [DataField, AutoNetworkedField]
    public bool Destroyed;

    [DataField, AutoNetworkedField]
    public bool Ignored;
}
