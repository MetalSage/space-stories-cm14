using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.EquipSkillRequirement;

[RegisterComponent]
[Access(typeof(EquipSkillRequirementSystem))]
public sealed partial class EquipSkillRequirementComponent : Component
{
    [DataField(required: true)]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();

    [DataField]
    public string Popup = string.Empty;
}
