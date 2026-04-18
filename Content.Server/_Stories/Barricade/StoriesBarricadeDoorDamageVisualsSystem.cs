using Content.Shared._Stories.Barricade;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server._Stories.Barricade;

public sealed class StoriesBarricadeDoorDamageVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref DamageChangedEvent args)
    {
        _appearance.SetData(ent, StoriesBarricadeDoorDamageVisuals.Damage, GetDamageLevel(args.Damageable.TotalDamage, ent.Comp.Thresholds));
    }

    private static int GetDamageLevel(FixedPoint2 damage, List<FixedPoint2> thresholds)
    {
        var level = 0;

        foreach (var threshold in thresholds)
        {
            if (damage < threshold)
                break;

            level++;
        }

        return level;
    }
}
