using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Stories.Hunter.Bracer;

public sealed class ClientStabilizingCrystalSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StabilizingCrystalComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<StabilizingCrystalComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (
            !_appearance.TryGetData(
                ent.Owner,
                StabilizingCrystalVisuals.State,
                out StabilizingCrystalVisualState state,
                args.Component
            )
        )
            state = StabilizingCrystalVisualState.Normal;

        var iconState = state == StabilizingCrystalVisualState.Used ? ent.Comp.UsedIconState : "crystal";

        _sprite.LayerSetRsiState((ent.Owner, args.Sprite), 0, iconState);
    }
}
