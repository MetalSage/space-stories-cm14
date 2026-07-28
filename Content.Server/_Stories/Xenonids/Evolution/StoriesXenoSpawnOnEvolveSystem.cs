using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._Stories.Xenonids.Evolution;

namespace Content.Server._Stories.Xenonids.Evolution;

public sealed class StoriesXenoSpawnOnEvolveSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NewXenoEvolvedEvent>(OnXenoEvolved);
    }

    private void OnXenoEvolved(ref NewXenoEvolvedEvent args)
    {
        if (!TryComp<StoriesXenoSpawnOnEvolveComponent>(args.OldXeno, out var component))
            return;

        var spawn = component.Spawn;
        RemComp<StoriesXenoSpawnOnEvolveComponent>(args.OldXeno);

        var spawned = Spawn(spawn, _transform.GetMoverCoordinates(args.OldXeno));
        _transform.AttachToGridOrMap(spawned);
    }
}
