using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth.Generation;

public sealed class STSynthGenerationVariantSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedSynthGenerationSystem _synthGeneration = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is not { } jobId ||
            !_prototype.TryIndex<JobPrototype>(jobId, out var job) ||
            job.Variants == null)
        {
            return;
        }

        if (!args.Profile.VariantPreferences.TryGetValue(jobId, out var variant) || variant == null)
            return;

        if (!_prototype.TryIndex<EntityPrototype>(variant, out var variantProto) ||
            !variantProto.HasComponent<SynthGenerationComponent>())
        {
            return;
        }

        if (!TryComp<SynthGenerationComponent>(args.Mob, out var genComp))
            return;

        genComp.Generation = variant;
        Dirty(args.Mob, genComp);

        if (TryComp<SynthComponent>(args.Mob, out var synthComp))
            _synthGeneration.SynthStartup((args.Mob, synthComp));

        if (genComp.SelectGenerationActionEntity is { } action)
            _actions.RemoveAction(args.Mob, action);
    }
}
