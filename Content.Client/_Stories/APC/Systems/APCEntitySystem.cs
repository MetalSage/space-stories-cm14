using Content.Shared.ADT.btr;
using Content.Shared.ADT.btr.Systems;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.ADT.btr;

/// <inheritdoc/>
public sealed partial class APCEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCEntityComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(EntityUid uid, APCEntityComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.Sprite.TryGetLayer((int)APCVisualLayers.Base, out var layer))
            return;

        var state = component.BaseState;
        var drawDepth = DrawDepth.Mobs;
        if (component.DestroyedState != null && _appearance.TryGetData<bool>(uid, APCVisuals.Destroyed, out var destroyed, args.Component) && destroyed)
        {
            state = component.DestroyedState;
            drawDepth = DrawDepth.SmallMobs;
        }

        layer.SetState(state);
        args.Sprite.DrawDepth = (int)drawDepth;
    }
}
