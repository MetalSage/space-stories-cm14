using Content.Shared._Stories.Ordnance.Assemblies;
using Robust.Client.GameObjects;

namespace Content.Client._Stories.Ordnance.Assemblies;

public sealed class OrdnanceAssemblyVisualizerSystem : VisualizerSystem<OrdnanceAssemblyHolderComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, OrdnanceAssemblyHolderComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null) return;

        if (AppearanceSystem.TryGetData<string>(uid, OrdnanceAssemblyVisuals.LeftId, out var leftId, args.Component))
        {
            var state = $"{leftId}_left";
            args.Sprite.LayerSetState(OrdnanceAssemblyLayers.Left, state);
            args.Sprite.LayerSetVisible(OrdnanceAssemblyLayers.Left, true);
        }
        else
        {
            args.Sprite.LayerSetVisible(OrdnanceAssemblyLayers.Left, false);
        }

        if (AppearanceSystem.TryGetData<string>(uid, OrdnanceAssemblyVisuals.RightId, out var rightId, args.Component))
        {
            var state = $"{rightId}_right";
            args.Sprite.LayerSetState(OrdnanceAssemblyLayers.Right, state);
            args.Sprite.LayerSetVisible(OrdnanceAssemblyLayers.Right, true);
        }
        else
        {
            args.Sprite.LayerSetVisible(OrdnanceAssemblyLayers.Right, false);
        }
    }
}
