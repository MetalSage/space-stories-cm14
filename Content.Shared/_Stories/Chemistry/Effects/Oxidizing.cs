using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Stories.Ordnance;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

[DataDefinition]
public sealed partial class Oxidizing : RMCChemicalEffect, IExplosionModifierEffect
{
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Causes severe cellular oxidation, dealing [color=red]{PotencyPerSecond}[/color] burn damage.\n" +
               $"Overdoses cause [color=red]{PotencyPerSecond * 2}[/color] burn damage.\n" +
               $"Critical overdoses cause [color=red]{PotencyPerSecond * 5}[/color] burn damage.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[HeatType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[HeatType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[HeatType] = potency * 5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    public void ModifyExplosionStats(ref float exPower, ref float exFalloff, ref float fireIntensity, ref float fireDuration, ref float fireRadius, float qty, float level)
    {
        fireIntensity += qty * (level * 0.2f);
        fireDuration -= qty * (level * 0.1f);
        fireRadius -= qty * (level * 0.01f);
    }
}
