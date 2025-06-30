using Content.Shared._Stories.APC;
using Content.Shared._Stories.Attachables;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Content.Client._Stories.APC.Attachables;

namespace Content.Client._Stories.APC;

public sealed class APCEntityVisualizerSystem : VisualizerSystem<APCEntityComponent>
{
    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void OnAppearanceChange(EntityUid uid, APCEntityComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        UpdateSprite((uid, sprite, args.Component, null));
    }

    public void UpdateSprite(Entity<SpriteComponent?, AppearanceComponent?, InputMoverComponent?, APCAttachableHolderVisualsComponent?> entity)
    {
        var (uid, sprite, appearance, input, holder) = entity;

        if (!Resolve(uid, ref sprite, ref appearance, false))
            return;

        Resolve(uid, ref input, false);
        Resolve(uid, ref holder, false);

        if (sprite is not { BaseRSI: { } rsi } ||
            !sprite.LayerMapTryGet(APCEntityVisualLayers.Base, out var layer))
        {
            return;
        }

        var isMoving = input?.HeldMoveButtons > MoveButtons.None &&
                    input.HeldMoveButtons != MoveButtons.Walk;

        if (holder == null)
            return;

        foreach (var (attachable, layerIndex) in holder.ActiveLayers)
        {
            if (!TryComp(attachable, out APCAttachableVisualsComponent? visualsComp) ||
                !TryComp(attachable, out APCAttachableComponent? attachableComp))
            {
                continue;
            }


            sprite.LayerSetAutoAnimated(layerIndex, isMoving);
        }
    }

    public override void Update(float frameTime)
    {
        var apcQuery = EntityQueryEnumerator<APCEntityComponent>();
        while (apcQuery.MoveNext(out var uid, out _))
        {
            UpdateSprite(uid);
        }
    }
}
