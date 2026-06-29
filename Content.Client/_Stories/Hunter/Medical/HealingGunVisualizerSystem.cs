using Content.Shared._Stories.Hunter.Medical;
using Content.Shared._Stories.Hunter.Medical.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Stories.Hunter.Medical;

public sealed class HealingGunVisualizerSystem : VisualizerSystem<HunterHealingGunComponent>
{
    protected override void OnAppearanceChange(
        EntityUid uid,
        HunterHealingGunComponent component,
        ref AppearanceChangeEvent args
    )
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, STHealingGunVisuals.Loaded, out var loaded, args.Component))
            return;

        var state = loaded ? component.LoadedState : component.EmptyState;
        args.Sprite.LayerSetState(0, state);
    }
}
