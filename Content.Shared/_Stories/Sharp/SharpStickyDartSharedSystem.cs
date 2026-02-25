using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.Sharp;

/// <summary>
/// Intentionally empty on client.
/// Sticky dart stop/delete/spawn is fully server-authoritative to avoid
/// predicted early cleanup and visual desync.
/// </summary>
public sealed class SharpStickyDartSharedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }
}
