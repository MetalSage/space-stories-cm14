using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Stories.Ordnance;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

[DataDefinition]
public sealed partial class Explosive : RMCChemicalEffect, IExplosionModifierEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "The chemical is highly explosive. Do not ignite. Careful when handling, sensitivity is based off the OD threshold, which can lead to spontaneous detonation.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    public void ModifyExplosionStats(ref float exPower, ref float exFalloff, ref float fireIntensity, ref float fireDuration, ref float fireRadius, float qty, float level)
    {
        exPower += qty * level;
        exFalloff -= qty * (level * 0.1f);
    }
}
