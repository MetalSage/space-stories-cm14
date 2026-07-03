using Content.Server.Destructible;
using Content.Shared._Stories.CombatMech;
using Content.Shared.FixedPoint;

namespace Content.Server._Stories.CombatMech;

public sealed class CombatMechDestructibleSystem : EntitySystem
{
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly CombatMechSystem _combatMech = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CombatMechComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CombatMechComponent> ent, ref ComponentStartup args)
    {
        if (!_destructible.TryGetDestroyedAt(ent.Owner, out var destroyedAt) ||
            destroyedAt.Value <= 0 ||
            destroyedAt.Value == FixedPoint2.MaxValue)
        {
            return;
        }

        _combatMech.SetMaxHealth(ent, destroyedAt.Value.Float());
    }
}
