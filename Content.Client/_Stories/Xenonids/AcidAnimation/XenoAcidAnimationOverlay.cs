using System.Linq;
using System.Numerics;
using Content.Client._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.AcidAnimation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Stories.Xenonids.AcidAnimation;

public sealed class XenoAcidAnimationOverlay : Overlay
{
    private const string SpitState = "spit";

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ContainerSystem _container;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly XenoAcidAnimationSystem _acidAnimation;

    private readonly EntityQuery<TransformComponent> _xformQuery;

    public override OverlaySpace Space => _overlay.HasOverlay<NightVisionOverlay>()
        ? OverlaySpace.WorldSpace
        : OverlaySpace.WorldSpaceBelowFOV;

    public XenoAcidAnimationOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _acidAnimation = _entity.System<XenoAcidAnimationSystem>();

        _xformQuery = _entity.GetEntityQuery<TransformComponent>();

        ZIndex = 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entity.EntityQueryEnumerator<XenoAcidAnimationComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (!_acidAnimation.ShouldAnimate(uid, comp, sprite, out var dir))
                continue;

            if (!_resources.TryGetResource<RSIResource>(comp.SpitRsi, out var rsiResource) ||
                !rsiResource.RSI.TryGetState(SpitState, out var state))
            {
                continue;
            }

            var texture = GetAnimatedFrame(state, dir, _acidAnimation.GetAnimationTime(uid, _timing.CurTime));
            var position = -(Vector2) texture.Size / EyeManager.PixelsPerMeter / 2f + _acidAnimation.GetOffset(comp);
            var bounds = GetDrawBounds((uid, sprite), position, texture);
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);
            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));
            handle.DrawTexture(texture, position);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private Box2 GetBaseBounds(Entity<SpriteComponent> ent)
    {
        Entity<SpriteComponent?> spriteEnt = (ent.Owner, ent.Comp);
        if (!_sprite.TryGetLayer(spriteEnt, XenoVisualLayers.Base, out var layer, false))
            return _sprite.GetLocalBounds(ent);

        return _sprite.GetLocalBounds(layer).Scale(ent.Comp.Scale);
    }

    private Box2 GetDrawBounds(Entity<SpriteComponent> ent, Vector2 position, Texture texture)
    {
        var spitSize = (Vector2) texture.Size / EyeManager.PixelsPerMeter;
        var spitBounds = Box2.FromDimensions(position, spitSize);
        return GetBaseBounds(ent).Union(spitBounds);
    }

    private static Texture GetAnimatedFrame(RSI.State state, RsiDirection dir, TimeSpan elapsed)
    {
        var frames = state.GetFrames(dir);
        if (!state.IsAnimated)
            return frames[0];

        var delays = state.GetDelays();
        var totalDelay = delays.Sum();
        if (totalDelay <= 0f)
            return frames[0];

        var time = elapsed.TotalSeconds % totalDelay;
        var delaySum = 0f;
        for (var i = 0; i < delays.Length; i++)
        {
            delaySum += delays[i];
            if (time <= delaySum)
                return frames[i];
        }

        return frames[^1];
    }
}
