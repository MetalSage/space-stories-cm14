using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Synth;

public sealed class STSelfRechargingSolutionSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STSelfRechargingSolutionComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<STSelfRechargingSolutionComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextRecharge = _timing.CurTime + ent.Comp.RechargeEvery;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<STSelfRechargingSolutionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextRecharge)
                continue;

            comp.NextRecharge = _timing.CurTime + comp.RechargeEvery;

            if (!_solution.TryGetSolution(uid, comp.SolutionId, out var solution, out _))
                continue;

            var amount = FixedPoint2.Min(comp.RechargeAmount, solution.Value.Comp.Solution.AvailableVolume);
            if (amount <= FixedPoint2.Zero)
                continue;

            _solution.TryAddReagent(solution.Value, comp.Reagent, amount, out _);
        }
    }
}
