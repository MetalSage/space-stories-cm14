using Content.Shared._Stories.Synth;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Synth.WorkingJoe;

public sealed class STWorkingJoeAppearanceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _appearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedSynthGenerationSystem _synthGeneration = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        STWorkingJoeAppearancePrototype? matched = null;
        foreach (var proto in _prototype.EnumeratePrototypes<STWorkingJoeAppearancePrototype>())
        {
            if (proto.Jobs.Contains(args.JobId))
            {
                matched = proto;
                break;
            }
        }

        if (matched == null)
            return;

        var mob = args.Mob;
        if (!TryComp(mob, out HumanoidAppearanceComponent? humanoid))
            return;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Hair);
        humanoid.MarkingSet.RemoveCategory(MarkingCategories.FacialHair);
        humanoid.EyeColor = matched.EyeColor;
        humanoid.SkinColor = matched.SkinColor;
        Dirty(mob, humanoid);

        var prefix = Loc.GetString(matched.NamePrefix);
        var designation = $"{prefix} #{_random.Next(0, 100)}{_random.Next(0, 100)}";
        _metaData.SetEntityName(mob, designation);

        if (TryComp<SynthComponent>(mob, out var synthComp))
            _synthGeneration.SynthStartup((mob, synthComp));

        foreach (var action in _actions.GetActions(mob))
        {
            if (action.Owner.IsValid() && MetaData(action.Owner).EntityPrototype?.ID == "ActionChooseGen")
            {
                _actions.RemoveAction(mob, action.Owner);
                break;
            }
        }
    }
}
