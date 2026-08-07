using Robust.Client.Graphics;

namespace Content.Client._Stories.Xenonids.XenoBoxerHud;

public sealed class XenoBoxerTrackerOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        if (!_overlay.HasOverlay<XenoBoxerTrackerOverlay>())
            _overlay.AddOverlay(new XenoBoxerTrackerOverlay());
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<XenoBoxerTrackerOverlay>();
    }
}
