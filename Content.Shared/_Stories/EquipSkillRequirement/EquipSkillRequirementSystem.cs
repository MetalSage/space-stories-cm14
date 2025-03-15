using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Stories.EquipSkillRequirement;

public sealed class EquipSkillRequirementSystem : EntitySystem
{
    [Dependency] private readonly SkillsSystem _rmcSkills = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EquipSkillRequirementComponent, BeingEquippedAttemptEvent>(OnEquip);
    }

    private void OnEquip(Entity<EquipSkillRequirementComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (_rmcSkills.HasAllSkills(args.EquipTarget, ent.Comp.Skills))
            return;

        if (!string.IsNullOrEmpty(ent.Comp.Popup))
            args.Reason = Loc.GetString(ent.Comp.Popup);

        args.Cancel();
    }
}
