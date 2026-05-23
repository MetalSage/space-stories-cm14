using Content.Shared._RMC14.Slow;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Stories.Ordnance;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

[DataDefinition]
public sealed partial class Viscous : RMCChemicalEffect, IExplosionModifierEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Thickens the blood, severely slowing movement for [color=yellow]{PotencyPerSecond}[/color] seconds.\n" +
               $"Overdoses cause extreme sluggishness for [color=red]{PotencyPerSecond * 2}[/color] seconds.\n" +
               $"Critical overdoses completely stop movement for [color=red]{PotencyPerSecond * 3}[/color] seconds.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var slow = args.EntityManager.System<RMCSlowSystem>();
        slow.TrySlowdown(args.TargetEntity, TimeSpan.FromSeconds((double)potency));
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var slow = args.EntityManager.System<RMCSlowSystem>();
        slow.TrySuperSlowdown(args.TargetEntity, TimeSpan.FromSeconds((double)(potency * 2f)));
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var slow = args.EntityManager.System<RMCSlowSystem>();
        slow.TrySuperSlowdown(args.TargetEntity, TimeSpan.FromSeconds((double)(potency * 3f)));
    }

    public void ModifyExplosionStats(ref float exPower, ref float exFalloff, ref float fireIntensity, ref float fireDuration, ref float fireRadius, float qty, float level)
    {
        fireRadius -= qty * (level * 0.025f);
    }
}
