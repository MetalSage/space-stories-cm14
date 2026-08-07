using Content.Shared.EntityEffects;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using JetBrains.Annotations;
using System;

namespace Content.Shared._Stories.Ordnance.Chemistry.ReactionEffects;

[UsedImplicitly]
[DataDefinition]
public sealed partial class SensitiveReactionExplosionEffect : EntityEffect
{
    [DataField(required: true)]
    public FixedPoint2 Threshold;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "RMC";

    [DataField]
    public float MaxIntensity = 100f;

    [DataField]
    public float IntensityPerUnit = 1f;

    [DataField]
    public float IntensitySlope = 1f;

    [DataField]
    public float MaxTotalIntensity = 100f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Explodes if created volume is greater than {Threshold}u.";
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
            return;

        if (reagentArgs.Quantity == FixedPoint2.Zero || reagentArgs.Quantity <= Threshold)
            return;

        var intensity = Math.Min(MaxIntensity, (float)reagentArgs.Quantity * IntensityPerUnit);

        var ev = new SensitiveReactionExplosionEvent(args.TargetEntity, (string)ExplosionType, intensity, IntensitySlope, MaxTotalIntensity);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

[ByRefEvent]
public record struct SensitiveReactionExplosionEvent(EntityUid Target, string ExplosionType, float Intensity, float Slope, float MaxTotalIntensity);
