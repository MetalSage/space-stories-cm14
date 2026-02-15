using Content.Server.Body.Systems;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._Stories.Hunter.Medical;
using Content.Shared._Stories.Hunter.Medical.Components;
using Content.Shared._Stories.Hunter.Medical.Components.Steps;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.Network;

namespace Content.Server._Stories.Hunter.Medical;

public sealed class HunterSurgerySystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HunterMedicalSystem _gunSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STSurgeryStepStabilizeComponent, CMSurgeryStepEvent>(OnStabilizeStep);
        SubscribeLocalEvent<STSurgeryStepMendComponent, CMSurgeryStepEvent>(OnMendStep);
        SubscribeLocalEvent<STSurgeryStepClampComponent, CMSurgeryStepEvent>(OnClampStep);
    }

    private void OnStabilizeStep(Entity<STSurgeryStepStabilizeComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (_net.IsClient)
            return;

        Heal(args.Body, ent.Comp.HealAmount);
        ApplySlow(args.Body, ent.Comp.SlowDuration);

        EnsureComp<STWoundsStabilizedComponent>(args.Part);
    }

    private void OnMendStep(Entity<STSurgeryStepMendComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (_net.IsClient)
            return;

        Heal(args.Body, ent.Comp.HealAmount);
        ApplySlow(args.Body, ent.Comp.SlowDuration);

        foreach (var organ in _body.GetBodyOrgans(args.Body))
        {
            if (TryComp<DamageableComponent>(organ.Id, out var damageable))
                _damageable.SetAllDamage(organ.Id, damageable, 0);
        }

        foreach (var tool in args.Tools)
        {
            if (TryComp<HunterHealingGunComponent>(tool, out var gun) && gun.Loaded)
            {
                _gunSystem.SetGunLoaded((tool, gun), false);
                break;
            }
        }

        EnsureComp<STWoundsMendedComponent>(args.Part);
    }

    private void OnClampStep(Entity<STSurgeryStepClampComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (_net.IsClient)
            return;

        Heal(args.Body, ent.Comp.HealAmount);

        _statusEffect.TryRemoveStatusEffect(args.Body, "Slowed");

        RemComp<CMIncisionOpenComponent>(args.Part);
        RemComp<CMBleedersClampedComponent>(args.Part);
        RemComp<CMSkinRetractedComponent>(args.Part);

        RemComp<STWoundsStabilizedComponent>(args.Part);
        RemComp<STWoundsMendedComponent>(args.Part);
    }

    private void Heal(EntityUid target, float amount)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return;

        var totalDamage = damageable.TotalDamage;
        if (totalDamage <= 0)
            return;

        var healSpec = new DamageSpecifier();
        var healAmountFixed = FixedPoint2.New(amount);

        if (healAmountFixed >= totalDamage)
        {
            foreach (var (type, value) in damageable.Damage.DamageDict)
            {
                healSpec.DamageDict[type] = -value;
            }
        }
        else
        {
            var ratio = healAmountFixed / totalDamage;
            foreach (var (type, value) in damageable.Damage.DamageDict)
            {
                healSpec.DamageDict[type] = -(value * ratio);
            }
        }

        _damageable.TryChangeDamage(target, healSpec, true);
    }

    private void ApplySlow(EntityUid target, TimeSpan duration)
    {
        _statusEffect.TryAddStatusEffect(target, "Slowed", duration, true);
    }
}
