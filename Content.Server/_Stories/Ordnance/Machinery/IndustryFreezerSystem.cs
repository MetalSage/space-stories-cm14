using Content.Shared._Stories.Ordnance.Machinery;
using Content.Server.Storage.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using System;

namespace Content.Server._Stories.Ordnance.Machinery;

public sealed class IndustryFreezerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IndustryFreezerComponent, EntityStorageComponent>();
        var curTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var freezer, out var storage))
        {
            if (storage.Open || curTime < freezer.NextProcessTime)
                continue;

            freezer.NextProcessTime = curTime + freezer.ProcessInterval;
            var processedCount = 0;

            foreach (var entity in storage.Contents.ContainedEntities)
            {
                if (processedCount >= freezer.MaxContainersPerInterval)
                    break;

                if (!_solution.TryGetFitsInDispenser(entity, out var solutionUid, out var solution))
                    continue;

                var formAmount = solution.GetTotalPrototypeQuantity(freezer.InputReagent1);
                var waterAmount = solution.GetTotalPrototypeQuantity(freezer.InputReagent2);

                if (formAmount >= FixedPoint2.New(3) && waterAmount >= FixedPoint2.New(3))
                {
                    if (solutionUid.HasValue)
                    {
                        var actualConvert = FixedPoint2.New(3);
                        _solution.RemoveReagent(solutionUid.Value, freezer.InputReagent1, actualConvert);
                        _solution.RemoveReagent(solutionUid.Value, freezer.InputReagent2, actualConvert);
                        _solution.TryAddReagent(solutionUid.Value, freezer.OutputReagent, actualConvert, out _);

                        processedCount++;

                        var formLeft = solution.GetTotalPrototypeQuantity(freezer.InputReagent1);
                        var waterLeft = solution.GetTotalPrototypeQuantity(freezer.InputReagent2);

                        if (formLeft < FixedPoint2.New(3) || waterLeft < FixedPoint2.New(3))
                        {
                            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/ding.ogg"), uid);
                        }
                    }
                }
            }
        }
    }
}
