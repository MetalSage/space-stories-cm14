using Content.Shared._Stories.Sponsors.WeaponSkins;
using Content.Shared._Stories.Sponsors.WeaponSkins.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;

namespace Content.Client._Stories.Sponsors.WeaponSkins.Systems;

public sealed class WeaponSkinVisualizerSystem : VisualizerSystem<WeaponSkinComponent>
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    protected override void OnAppearanceChange(EntityUid uid, WeaponSkinComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, WeaponSkinVisuals.Skin, out var skinId, args.Component))
        {
            skinId = component.DefaultSkin;
            if (string.IsNullOrEmpty(skinId))
                return;
        }

        if (!component.Skins.TryGetValue(skinId, out var skinRsiPath))
            return;

        if (!_resourceCache.TryGetResource<RSIResource>(skinRsiPath, out var skinRsi))
            return;

        args.Sprite.BaseRSI = skinRsi.RSI;
        if (component.Layers != null)
        {
            foreach (var (layerKey, skinStates) in component.Layers)
            {
                if (skinStates.TryGetValue(skinId, out var layerState) &&
                    args.Sprite.LayerMapTryGet(layerKey, out var layerIndex))
                {
                    args.Sprite.LayerSetState(layerIndex, layerState);
                }
            }
        }
        _item.VisualsChanged(uid);
    }
}
