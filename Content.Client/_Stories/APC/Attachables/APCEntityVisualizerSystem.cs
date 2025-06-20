/*
using Robust.Client.GameObjects;
using Content.Shared._Stories.APC;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;

namespace Content.Client._Stories.APC.Modules;

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

    public void UpdateSprite(Entity<SpriteComponent?, AppearanceComponent?, InputMoverComponent?, APCModulesHolderVisualsComponent?> entity)
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
            
        foreach (var (moduleUid, layerIndex) in holder.ActiveLayers)
        {
            if (!TryComp(moduleUid, out APCModulesVisualsComponent? visComp) ||
                !TryComp(moduleUid, out APCModuleComponent? moduleComp))
            {
                continue;
            }

//            if (moduleComp.ModuleType != APCModuleType.Movement)
//                continue;

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
*/
