using Content.Shared._Stories.Hunter.Marking;
using Robust.Client.Graphics;

namespace Content.Client._Stories.Hunter.Marking;

public sealed class HunterMarkingSystem : SharedHunterMarkingSystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlays.HasOverlay<HunterMarkingOverlay>())
            _overlays.AddOverlay(new HunterMarkingOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<HunterMarkingOverlay>();
    }
}
