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
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange, after: [typeof(DoorSystem)]);
        SubscribeLocalEvent<StoriesBarricadeDoorDamageVisualsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnStartup(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref ComponentStartup args)
    {
        Validate(ent);
    }

    private void OnAppearanceChange(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<DoorState>(ent, DoorVisuals.State, out var state, args.Component))
            state = DoorState.Closed;

        UpdateVisual(ent.Owner, ent.Comp, args.Sprite, state);
    }

    private void OnDamageChanged(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var state = GetDoorState(ent.Owner);
        UpdateVisual(ent.Owner, ent.Comp, sprite, state);
    }

    private static bool IsOpenState(DoorState state)
    {
        return state is DoorState.Open or DoorState.Opening or DoorState.Emagging;
    }

    private void UpdateVisual(EntityUid uid, StoriesBarricadeDoorDamageVisualsComponent visuals, SpriteComponent sprite, DoorState doorState)
    {
        if (!visuals.Valid)
            return;

        if (!TryComp(uid, out DamageableComponent? damageable) ||
            !_sprite.LayerMapTryGet((uid, sprite), visuals.Layer, out var layer, false))
            return;

        var prefix = IsOpenState(doorState) ? visuals.OpenPrefix : visuals.ClosedPrefix;
        var state = $"{prefix}_{GetDamageLevel(damageable.TotalDamage, visuals.Thresholds)}";

        _sprite.LayerSetRsiState((uid, sprite), layer, state);
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

    private DoorState GetDoorState(EntityUid uid)
    {
        if (TryComp(uid, out AppearanceComponent? appearance) &&
            _appearance.TryGetData<DoorState>(uid, DoorVisuals.State, out var state, appearance))
        {
            return state;
        }

        return TryComp(uid, out DoorComponent? door) ? door.State : DoorState.Closed;
    }

    private void Validate(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent)
    {
        var visuals = ent.Comp;

        if (visuals.Thresholds.Count == 0)
        {
            Log.Error($"No damage thresholds configured for {ToPrettyString(ent.Owner)}.");
            visuals.Valid = false;
            return;
        }

        visuals.Thresholds.Sort();

        if (!TryComp(ent, out SpriteComponent? sprite) || sprite.BaseRSI == null)
            return;

        var stateCount = visuals.Thresholds.Count + 1;
        for (var level = 0; level < stateCount; level++)
        {
            ValidateState(ent, sprite, visuals.ClosedPrefix, level);
            ValidateState(ent, sprite, visuals.OpenPrefix, level);
        }
    }

    private void ValidateState(Entity<StoriesBarricadeDoorDamageVisualsComponent> ent, SpriteComponent sprite, string prefix, int level)
    {
        if (!ent.Comp.Valid)
            return;

        var state = $"{prefix}_{level}";
        if (sprite.BaseRSI!.TryGetState(state, out _))
            return;

        Log.Error($"Missing RSI state '{state}' for {ToPrettyString(ent.Owner)}.");
        ent.Comp.Valid = false;
    }
}
