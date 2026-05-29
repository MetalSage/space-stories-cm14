using Content.Shared._Stories.Ordnance;
using Robust.Client.GameObjects;

namespace Content.Client._Stories.Ordnance;

public sealed class OrdnanceCasingVisualizerSystem : VisualizerSystem<OrdnanceCasingComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, OrdnanceCasingComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<string>(uid, OrdnanceCasingVisuals.StateId, out var stateId, args.Component))
        {
            args.Sprite.LayerSetState(0, stateId);
        }
    }
}
