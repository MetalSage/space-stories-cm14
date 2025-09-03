using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Whitelist;

namespace Content.Shared._Stories.Extensions;

public sealed partial class StoriesExtensions : EntitySystem
{
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly EntityWhitelistSystem _entityWhitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AssignSquadOnSpawnComponent, MapInitEvent>(AssignSquadOnInit);
        SubscribeLocalEvent<AssignHiveOnSpawnComponent, MapInitEvent>(AssignHiveOnInit);
    }

    private void AssignSquadOnInit(Entity<AssignSquadOnSpawnComponent> ent, ref MapInitEvent args)
    {
        var query = EntityQueryEnumerator<SquadTeamComponent>();
        while (query.MoveNext(out var squad, out var comp))
        {
            if (!_entityWhitelist.IsWhitelistPass(ent.Comp.Whitelist, squad))
                continue;

            _squad.AssignSquad(ent, (squad, comp), null);
        }
    }

    private void AssignHiveOnInit(Entity<AssignHiveOnSpawnComponent> ent, ref MapInitEvent args)
    {
        var query = EntityQueryEnumerator<HiveComponent>();
        while (query.MoveNext(out var hive, out var comp))
        {
            if (!_entityWhitelist.IsWhitelistPass(ent.Comp.Whitelist, hive))
                continue;

            _hive.SetHive(ent.Owner, hive);
        }
    }
}
