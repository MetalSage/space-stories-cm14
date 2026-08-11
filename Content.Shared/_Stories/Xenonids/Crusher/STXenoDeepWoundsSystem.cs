using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Crusher;

public sealed class STXenoDeepWoundsSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedWoundsSystem _wounds = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private const int DeepMaxStage = 3;
    private const int WeepingMaxStage = 2;
    private const int TailStabMinDeepStage = 2;

    private static readonly TimeSpan StageDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EscalateInterval = TimeSpan.FromSeconds(10);
    private static readonly float[] StageBloodloss = { 5f, 5f, 7f };

    private static readonly TimeSpan[] DeepSlowDurations =
    {
        TimeSpan.FromSeconds(1.5),
        TimeSpan.FromSeconds(3.25),
        TimeSpan.FromSeconds(5),
    };

    private static readonly TimeSpan[] WeepingSlowDurations =
    {
        TimeSpan.FromSeconds(1.5),
        TimeSpan.FromSeconds(5),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STXenoDeepWoundsComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<WoundableComponent, STCrusherSplashHitEvent>(OnSplashHit);
        SubscribeLocalEvent<STDeepWoundStagesComponent, STWoundTreatStageAttemptEvent>(OnTreatStageAttempt);
    }

    private void OnTreatStageAttempt(Entity<STDeepWoundStagesComponent> ent, ref STWoundTreatStageAttemptEvent args)
    {
        if (args.Deep)
        {
            if (ent.Comp.DeepStage <= 0)
            {
                args.FullyTreated = true;
                return;
            }

            ent.Comp.DeepStage--;
            args.FullyTreated = ent.Comp.DeepStage <= 0;
        }
        else
        {
            if (ent.Comp.WeepingStage <= 0)
            {
                args.FullyTreated = true;
                return;
            }

            ent.Comp.WeepingStage--;
            args.FullyTreated = ent.Comp.WeepingStage <= 0;
        }

        Dirty(ent);
    }

    private void OnMeleeHit(Entity<STXenoDeepWoundsComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var isTailStab = RemComp<STXenoTailStabHitComponent>(xeno.Owner);

        foreach (var target in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno.Owner, target))
                continue;

            if (isTailStab)
                EscalateDeepToStage(target, TailStabMinDeepStage);
            else
                EscalateDeep(target);

            break;
        }
    }

    private void OnSplashHit(Entity<WoundableComponent> target, ref STCrusherSplashHitEvent args)
    {
        var stages = EnsureComp<STDeepWoundStagesComponent>(target.Owner);
        if (stages.WeepingStage != 0)
            return;

        ApplyWeepingStage(target.Owner, stages, 1);
    }

    private void EscalateDeep(EntityUid target)
    {
        var stages = EnsureComp<STDeepWoundStagesComponent>(target);
        if (stages.DeepStage >= DeepMaxStage)
            return;

        ApplyDeepStage(target, stages, stages.DeepStage + 1);
    }

    private void EscalateDeepToStage(EntityUid target, int minStage)
    {
        var stages = EnsureComp<STDeepWoundStagesComponent>(target);
        if (stages.DeepStage >= minStage)
        {
            EscalateDeep(target);
            return;
        }

        ApplyDeepStage(target, stages, minStage);
    }

    private void ApplyDeepStage(EntityUid target, STDeepWoundStagesComponent stages, int stage)
    {
        stages.DeepStage = stage;
        stages.DeepNextEscalateAt = _timing.CurTime + EscalateInterval;
        Dirty(target, stages);

        var amount = StageBloodloss[stage - 1];
        _wounds.AddWound(target, FixedPoint2.New(amount), WoundType.Brute, StageDuration, deep: true, directBloodloss: amount, untreatable: true);
        _slow.TrySlowdown(target, GetSlowDuration(true, stage), ignoreDurationModifier: true);
    }

    private void ApplyWeepingStage(EntityUid target, STDeepWoundStagesComponent stages, int stage)
    {
        stages.WeepingStage = stage;
        stages.WeepingNextEscalateAt = _timing.CurTime + EscalateInterval;
        Dirty(target, stages);

        var amount = StageBloodloss[stage - 1];
        _wounds.AddWound(target, FixedPoint2.New(amount), WoundType.Brute, StageDuration, deep: false, directBloodloss: amount, untreatable: true);
        _slow.TrySlowdown(target, GetSlowDuration(false, stage), ignoreDurationModifier: true);
    }

    private static TimeSpan GetSlowDuration(bool deep, int stage)
    {
        var durations = deep ? DeepSlowDurations : WeepingSlowDurations;
        var index = Math.Clamp(stage - 1, 0, durations.Length - 1);
        return durations[index];
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<STDeepWoundStagesComponent>();
        while (query.MoveNext(out var uid, out var stages))
        {
            if (stages.DeepStage > 0 && time >= stages.DeepNextEscalateAt)
            {
                if (!_wounds.HasActiveDeepWound(uid, true))
                {
                    stages.DeepStage = 0;
                    Dirty(uid, stages);
                }
                else if (stages.DeepStage < DeepMaxStage)
                {
                    ApplyDeepStage(uid, stages, stages.DeepStage + 1);
                }
            }

            if (stages.WeepingStage > 0 && time >= stages.WeepingNextEscalateAt)
            {
                if (!_wounds.HasActiveDeepWound(uid, false))
                {
                    stages.WeepingStage = 0;
                    Dirty(uid, stages);
                }
                else if (stages.WeepingStage < WeepingMaxStage)
                {
                    ApplyWeepingStage(uid, stages, stages.WeepingStage + 1);
                }
            }
        }
    }
}
