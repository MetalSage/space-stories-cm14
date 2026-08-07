using System.Numerics;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Vision;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Hunter.Marking;

public sealed class HunterMarkingOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private readonly InventorySystem _inventory;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    private readonly ShaderInstance _shader;
    private readonly SpriteSystem _sprite;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly TransformSystem _transform;

    public HunterMarkingOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _inventory = _entity.System<InventorySystem>();
        _shader = _prototype.Index<ShaderPrototype>("unshaded").Instance();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewer = _playerManager.LocalPlayer?.ControlledEntity;
        if (viewer == null)
            return;

        if (
            !_inventory.TryGetSlotEntity(viewer.Value, "mask", out var maskEntity)
            || !_entity.HasComponent<HunterVisionMaskComponent>(maskEntity)
        )
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        var query = _entity.AllEntityQueryEnumerator<HunterMarkedComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marked, out var sprite, out var xform))
        {
            DrawMarksForEntity((uid, sprite, xform), marked, in args, scaleMatrix, rotationMatrix);
        }

        handle.UseShader(null);
    }

    private void DrawMarksForEntity(
        Entity<SpriteComponent, TransformComponent> ent,
        HunterMarkedComponent marked,
        in OverlayDrawArgs args,
        Matrix3x2 scaleMatrix,
        Matrix3x2 rotationMatrix
    )
    {
        var marksToDraw = new[]
        {
            (HunterMarkType.Prey, marked.PreyIcon),
            (HunterMarkType.Honored, marked.HonoredIcon),
            (HunterMarkType.Dishonored, marked.DishonoredIcon),
            (HunterMarkType.GearCarrier, marked.GearCarrierIcon),
            (HunterMarkType.Thralled, marked.ThralledIcon),
            (HunterMarkType.Blooded, marked.BloodedIcon),
        };

        foreach (var (markType, icon) in marksToDraw)
        {
            if ((marked.Marks & markType) != 0)
                DrawIcon(ent, in args, icon, scaleMatrix, rotationMatrix);
        }
    }

    private void DrawIcon(
        Entity<SpriteComponent, TransformComponent> ent,
        in OverlayDrawArgs args,
        SpriteSpecifier.Rsi icon,
        Matrix3x2 scaleMatrix,
        Matrix3x2 rotationMatrix
    )
    {
        var (_, sprite, xform) = ent;
        if (xform.MapID != args.MapId)
            return;

        var bounds = sprite.Bounds;
        var worldPos = _transform.GetWorldPosition(xform);

        if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
            return;

        var handle = args.WorldHandle;
        var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
        var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
        var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
        handle.SetTransform(matrix);

        var texture = _sprite.GetFrame(icon, _timing.CurTime);
        if (texture == null)
            return;

        var yOffset = -((float)texture.Height / EyeManager.PixelsPerMeter) / 2f;
        var xOffset = -((float)texture.Width / EyeManager.PixelsPerMeter) / 2f;

        handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
    }
}
