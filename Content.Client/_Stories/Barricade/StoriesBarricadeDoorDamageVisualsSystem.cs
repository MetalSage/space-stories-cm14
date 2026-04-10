using Content.Client.Doors;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared._Stories.Barricade;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Stories.Barricade;

public sealed class StoriesBarricadeDoorDamageVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange, after: [typeof(DoorSystem)]);
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnAppearanceChange(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateVisual(ent.Owner, ent.Comp, args.Sprite);
    }

    private void OnDamageChanged(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
        {
            return;
        }

        UpdateVisual(ent.Owner, ent.Comp, sprite);
    }

    private static bool IsOpenState(DoorState state)
    {
        return state is DoorState.Open or DoorState.Opening or DoorState.Emagging;
    }

    private void UpdateVisual(EntityUid uid, StoriesBarricadeDoorDamageVisualsComponent visuals, SpriteComponent sprite)
    {
        if (!TryComp(uid, out DamageableComponent? damageable) ||
            !TryComp(uid, out DoorComponent? door) ||
            !sprite.LayerMapTryGet(DoorVisualLayers.Base, out var layer))
        {
            return;
        }

        var prefix = IsOpenState(door.State) ? visuals.OpenPrefix : visuals.ClosedPrefix;
        var state = $"{prefix}_{GetDamageLevel(damageable.TotalDamage, visuals.Thresholds)}";

        sprite.LayerSetState(layer, state);
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
