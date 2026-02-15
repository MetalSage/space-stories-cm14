using Robust.Client.Graphics;

namespace Content.Client._Stories.Hunter.Vision;

public sealed class HunterVisionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlays.HasOverlay<HunterClanOverlay>())
            _overlays.AddOverlay(new HunterClanOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<HunterClanOverlay>();
    }
}
