using Content.Shared._Stories.Hunter;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client._Stories.Hunter;

public sealed class HunterCapeVisualizerSystem : VisualizerSystem<HunterCapeVisualsComponent>
{
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterCapeVisualsComponent, GetEquipmentVisualsEvent>(
            OnGetEquipmentVisuals,
            after: new[] { typeof(ClothingSystem) }
        );
    }

    protected override void OnAppearanceChange(
        EntityUid uid,
        HunterCapeVisualsComponent component,
        ref AppearanceChangeEvent args
    )
    {
        if (args.Sprite != null)
        {
            if (AppearanceSystem.TryGetData<Color>(uid, HunterCapeVisuals.Color, out var color, args.Component))
                args.Sprite.Color = color;
            else
                args.Sprite.Color = Color.White;
        }

        _itemSystem.VisualsChanged(uid);
    }

    private void OnGetEquipmentVisuals(Entity<HunterCapeVisualsComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!AppearanceSystem.TryGetData<Color>(ent, HunterCapeVisuals.Color, out var color))
            return;

        foreach (var layerTuple in args.Layers)
        {
            layerTuple.Item2.Color = color;
        }
    }
}
