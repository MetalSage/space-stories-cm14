using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Stories.Ordnance;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

[DataDefinition]
public sealed partial class Fueling : RMCChemicalEffect, IExplosionModifierEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Makes the body more flammable, increasing fire stacks by [color=orange]{PotencyPerSecond}[/color].\n" +
               $"Overdoses severely increase flammability by [color=orange]{PotencyPerSecond * 2}[/color].\n" +
               $"Critical overdoses violently engulf the body in flames.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var rmcFlammable = args.EntityManager.System<SharedRMCFlammableSystem>();
        if (args.EntityManager.TryGetComponent<FlammableComponent>(args.TargetEntity, out var flammable))
        {
            rmcFlammable.AdjustStacks((args.TargetEntity, flammable), (int)potency);
        }
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var rmcFlammable = args.EntityManager.System<SharedRMCFlammableSystem>();
        if (args.EntityManager.TryGetComponent<FlammableComponent>(args.TargetEntity, out var flammable))
        {
            rmcFlammable.AdjustStacks((args.TargetEntity, flammable), (int)(potency * 2f));
        }
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var rmcFlammable = args.EntityManager.System<SharedRMCFlammableSystem>();
        if (args.EntityManager.TryGetComponent<FlammableComponent>(args.TargetEntity, out var flammable))
        {
            rmcFlammable.Ignite((args.TargetEntity, flammable), 25, 20, 10, true);
        }
    }

    public void ModifyExplosionStats(ref float exPower, ref float exFalloff, ref float fireIntensity, ref float fireDuration, ref float fireRadius, float qty, float level)
    {
        fireIntensity -= qty * (level * 0.1f);
        fireDuration += qty * (level * 0.2f);
        fireRadius += qty * (level * 0.01f);
    }
}
