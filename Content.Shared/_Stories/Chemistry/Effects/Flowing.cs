using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Stories.Ordnance;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

[DataDefinition]
public sealed partial class Flowing : RMCChemicalEffect, IExplosionModifierEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "The chemical is the opposite of viscous, and it tends to spill everywhere. This could probably be used to expand the radius of a chemical fire.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    public void ModifyExplosionStats(ref float exPower, ref float exFalloff, ref float fireIntensity, ref float fireDuration, ref float fireRadius, float qty, float level)
    {
        fireRadius += qty * (level * 0.05f);
        fireIntensity -= qty * (level * 0.05f);
        fireDuration -= qty * (level * 0.05f);
    }
}
