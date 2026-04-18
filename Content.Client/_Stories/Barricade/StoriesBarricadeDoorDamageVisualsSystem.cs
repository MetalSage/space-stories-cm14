using Content.Client.Doors;
using Content.Shared.Doors.Components;
using Content.Shared._Stories.Barricade;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Barricade;

public sealed class StoriesBarricadeDoorDamageVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange, after: [typeof(DoorSystem)]);
    }

    private void OnAppearanceChange(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<DoorState>(ent, DoorVisuals.State, out var state, args.Component))
            state = DoorState.Closed;

        if (!_appearance.TryGetData<int>(ent, StoriesBarricadeDoorDamageVisuals.Damage, out var damage, args.Component))
            damage = 0;

        UpdateVisual(ent.Owner, ent.Comp, args.Sprite, state, damage);
    }

    private static bool IsOpenState(DoorState state)
    {
        return state is DoorState.Open or DoorState.Opening or DoorState.Emagging;
    }

    private void UpdateVisual(EntityUid uid, StoriesBarricadeDoorDamageVisualsComponent visuals, SpriteComponent sprite, DoorState doorState, int damage)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), visuals.Layer, out var layer, false))
            return;

        if (damage <= 0 || visuals.States.Count == 0)
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        var spritePath = IsOpenState(doorState) ? visuals.OpenDamageSprite : visuals.ClosedDamageSprite;
        var state = visuals.States[Math.Clamp(damage - 1, 0, visuals.States.Count - 1)];
        _sprite.LayerSetSprite((uid, sprite), layer, new SpriteSpecifier.Rsi(new(spritePath), state));
        _sprite.LayerSetVisible((uid, sprite), layer, true);
    }
}
