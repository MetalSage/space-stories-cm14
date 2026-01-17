using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._Stories.Hunter.Medical.Components;
using Content.Shared._Stories.Hunter.Medical.Components.Steps;
using Content.Shared.Damage;

namespace Content.Shared._Stories.Hunter.Medical;

public sealed class SharedSTHunterSurgerySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STSurgeryDamagedConditionComponent, CMSurgeryValidEvent>(OnDamagedCondition);

        SubscribeLocalEvent<STSurgeryStepStabilizeComponent, CMSurgeryStepCompleteCheckEvent>(OnStabilizeCheck);
        SubscribeLocalEvent<STSurgeryStepMendComponent, CMSurgeryStepCompleteCheckEvent>(OnMendCheck);
        SubscribeLocalEvent<STSurgeryStepClampComponent, CMSurgeryStepCompleteCheckEvent>(OnClampCheck);

        SubscribeLocalEvent<STSurgeryStepMendComponent, CMSurgeryCanPerformStepEvent>(OnMendToolCheck);
    }

    private void OnDamagedCondition(Entity<STSurgeryDamagedConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!TryComp<DamageableComponent>(args.Body, out var damageable) || damageable.TotalDamage <= 0)
            args.Cancelled = true;
    }

    private void OnStabilizeCheck(Entity<STSurgeryStepStabilizeComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<STWoundsStabilizedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnMendCheck(Entity<STSurgeryStepMendComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<STWoundsMendedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnClampCheck(Entity<STSurgeryStepClampComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMendToolCheck(Entity<STSurgeryStepMendComponent> ent, ref CMSurgeryCanPerformStepEvent args)
    {
        var gunFound = false;
        foreach (var tool in args.Tools)
        {
            if (TryComp<HunterHealingGunComponent>(tool, out var gun))
            {
                if (!gun.Loaded)
                {
                    args.Invalid = StepInvalidReason.MissingTool;
                    args.Popup = Loc.GetString("st-healing-gun-needs-ammo");
                    return;
                }

                gunFound = true;
            }
        }

        if (!gunFound)
            args.Invalid = StepInvalidReason.MissingTool;
    }
}
