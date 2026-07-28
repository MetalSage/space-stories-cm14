using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._Stories.Xenonids.Evolution;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Stories.Xenonids.Evolution;

[TestFixture]
public sealed class StoriesXenoSpawnOnEvolveTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: STTestXenoSpawnOnEvolveOld
  components:
  - type: XenoEvolution
  - type: StoriesXenoSpawnOnEvolve
    spawn: STTestXenoSpawnOnEvolveResult

- type: entity
  id: STTestXenoSpawnOnEvolveNew

- type: entity
  id: STTestXenoSpawnOnEvolveResult
""";

    [Test]
    public async Task SpawnsOnlyOncePerOldXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var mapSystem = entManager.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            var oldXeno = entManager.SpawnEntity("STTestXenoSpawnOnEvolveOld", coordinates);
            var newXeno = entManager.SpawnEntity("STTestXenoSpawnOnEvolveNew", coordinates);
            var oldEvolution = entManager.GetComponent<XenoEvolutionComponent>(oldXeno);

            var ev = new NewXenoEvolvedEvent((oldXeno, oldEvolution), newXeno, true);
            entManager.EventBus.RaiseLocalEvent(newXeno, ref ev, true);
            entManager.EventBus.RaiseLocalEvent(newXeno, ref ev, true);

            Assert.That(CountSpawned(entManager), Is.EqualTo(1));
            Assert.That(entManager.HasComponent<StoriesXenoSpawnOnEvolveComponent>(oldXeno), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistinctOldXenosCanEachSpawnOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var mapSystem = entManager.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            var newXeno = entManager.SpawnEntity("STTestXenoSpawnOnEvolveNew", coordinates);

            RaiseEvolution(entManager, coordinates, newXeno);
            RaiseEvolution(entManager, coordinates, newXeno);

            Assert.That(CountSpawned(entManager), Is.EqualTo(2));
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseEvolution(
        IEntityManager entManager,
        MapCoordinates coordinates,
        EntityUid newXeno)
    {
        var oldXeno = entManager.SpawnEntity("STTestXenoSpawnOnEvolveOld", coordinates);
        var oldEvolution = entManager.GetComponent<XenoEvolutionComponent>(oldXeno);
        var ev = new NewXenoEvolvedEvent((oldXeno, oldEvolution), newXeno, true);
        entManager.EventBus.RaiseLocalEvent(newXeno, ref ev, true);
    }

    private static int CountSpawned(IEntityManager entManager)
    {
        return entManager.EntityQuery<MetaDataComponent>()
            .Count(meta => !meta.Deleted &&
                meta.EntityPrototype?.ID == "STTestXenoSpawnOnEvolveResult");
    }
}
