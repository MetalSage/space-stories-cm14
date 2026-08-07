using System.Numerics;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Profiles;
using Content.Shared._Stories.Hunter.Vision;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Hunter.Vision;

public sealed class HunterClanOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private readonly InventorySystem _inventory;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly ResPath _rsiPath = new("/Textures/_Stories/Interface/Misc/hunter_marks.rsi");

    private readonly SpriteSystem _sprite;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly TransformSystem _transform;

    public HunterClanOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entityManager.System<SpriteSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _inventory = _entityManager.System<InventorySystem>();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localPlayer = _playerManager.LocalEntity;
        if (localPlayer == null)
            return;

        if (!_entityManager.HasComponent<HunterComponent>(localPlayer))
            return;

        if (!_inventory.TryGetSlotEntity(localPlayer.Value, "mask", out var mask) ||
            !_entityManager.HasComponent<HunterVisionMaskComponent>(mask))
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entityManager.AllEntityQueryEnumerator<HunterComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hunter, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var iconState = hunter.Status switch
            {
                HunterStatus.Leader => "leaderhud",
                HunterStatus.Council => "councilhud",
                _ => "predhud",
            };

            var iconRsi = new SpriteSpecifier.Rsi(_rsiPath, iconState);
            var texture = _sprite.GetFrame(iconRsi, _timing.CurTime);

            if (texture != null)
            {
                var scale = 1f / 32f;
                var w = texture.Width * scale;
                var h = texture.Height * scale;

                var drawPos = new Vector2(-w / 2f, -h / 2f);

                handle.DrawTextureRect(texture, new Box2(drawPos.X, drawPos.Y, drawPos.X + w, drawPos.Y + h));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
