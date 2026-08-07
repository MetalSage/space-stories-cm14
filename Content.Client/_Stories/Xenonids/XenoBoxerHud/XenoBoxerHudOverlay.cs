using System.Numerics;
using Content.Client._RMC14.NightVision;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Client._Stories.Xenonids.XenoBoxerHud;

public sealed class XenoBoxerTrackerOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ContainerSystem _container;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<EntityActiveInvisibleComponent> _invisQuery;

    private readonly ResPath _rsiPath = new("/Textures/_Stories/Interface/boxer_hud.rsi");
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => _overlay.HasOverlay<NightVisionOverlay>()
        ? OverlaySpace.WorldSpace
        : OverlaySpace.WorldSpaceBelowFOV;

    public XenoBoxerTrackerOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();

        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _invisQuery = _entity.GetEntityQuery<EntityActiveInvisibleComponent>();

        _shader = _prototype.Index<ShaderPrototype>("unshaded").Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entity.HasComponent<XenoComponent>(_players.LocalEntity))
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        if (_entity.HasComponent<XenoBoxerKnockoutRecentlyComponent>(_players.LocalEntity))
            DrawTracker(in args, scaleMatrix, rotationMatrix);

        DrawKnockoutLabel(in args, scaleMatrix, rotationMatrix);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTracker(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var trackers = _entity.EntityQueryEnumerator<XenoBoxerKnockoutRecentlyComponent>();
        while (trackers.MoveNext(out var uid, out var comp))
        {
            foreach (var (target, tracker) in comp.Trackers)
            {
                if (!_entity.TryGetComponent<SpriteComponent>(target, out var sprite))
                    continue;

                if (!_entity.TryGetComponent<TransformComponent>(target, out var xform))
                    continue;

                if (_invisQuery.HasComp(target))
                    continue;

                if (xform.MapID != args.MapId)
                    continue;

                if (_container.IsEntityOrParentInContainer(target, xform: xform))
                    continue;

                var bounds = sprite.Bounds;
                var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

                if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                    continue;

                var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
                var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
                var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
                handle.SetTransform(matrix);

                var spriteFrame = (int)Math.Clamp(tracker.Count, 1f, 15f);
                var icon = new Rsi(_rsiPath, $"tracker_{spriteFrame}");
                var texture = _sprite.GetFrame(icon, _timing.CurTime);

                var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height + 0.2f;
                var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width + 0.2f;

                var position = new Vector2(xOffset, yOffset);
                handle.DrawTexture(texture, position);
            }
        }
    }

    private void DrawKnockoutLabel(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var icon = new Rsi(_rsiPath, "ko_label");

        var query = _entity.EntityQueryEnumerator<KnockoutLabelComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var target, out var _, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(target, xform: xform))
                continue;

            if (_invisQuery.HasComp(target))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }
}
