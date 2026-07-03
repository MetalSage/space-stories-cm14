using Content.Shared._Stories.CombatMech;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Stories.CombatMech;

// Mirrors the body-overlay sprite colour to the mech body every frame so RMC's damage-flash
// tween stays in sync. There is no public hook into the tween itself.
public sealed class CombatMechDamageFlashSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CombatMechComponent, SpriteComponent>();
        while (query.MoveNext(out _, out var mech, out var mechSprite))
        {
            if (mech.BodyOverlayEntity is not { } overlay || Deleted(overlay))
                continue;

            if (_spriteQuery.TryComp(overlay, out var overlaySprite) &&
                overlaySprite.Color != mechSprite.Color)
            {
                _sprite.SetColor((overlay, overlaySprite), mechSprite.Color);
            }
        }
    }
}
