using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.FixedPoint;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Repairable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedVehicleRepairableSystem))]
public sealed partial class VehicleRepairableComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillEngineer";

    [DataField, AutoNetworkedField]
    public int SkillRequired;

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ToolQualityPrototype>, float> ToolThresholds = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ToolQualityPrototype>, SoundSpecifier>? ToolSoundThresholds = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ToolQualityPrototype>, string>? ToolMessages = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 FuelUsed = FixedPoint2.New(1);
}
