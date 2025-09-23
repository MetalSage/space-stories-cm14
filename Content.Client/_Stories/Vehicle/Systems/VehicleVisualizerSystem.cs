using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Attachables;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Content.Client._Stories.Vehicle.Attachables;

namespace Content.Client._Stories.Vehicle;

public sealed class VehicleVisualizerSystem : VisualizerSystem<VehicleComponent>
{
    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void OnAppearanceChange(EntityUid uid, VehicleComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        UpdateSprite((uid, sprite, args.Component, null));
    }

    public void UpdateSprite(Entity<SpriteComponent?, AppearanceComponent?, InputMoverComponent?, VehicleAttachableHolderVisualsComponent?> entity)
    {
        var (uid, sprite, appearance, input, holder) = entity;

        if (!Resolve(uid, ref sprite, ref appearance, false))
            return;

        Resolve(uid, ref input, false);
        Resolve(uid, ref holder, false);

        if (sprite is not { BaseRSI: { } rsi } ||
            !sprite.LayerMapTryGet(VehicleVisualLayers.Base, out var layer))
        {
            return;
        }

        var isMoving = input?.HeldMoveButtons > MoveButtons.None &&
                    input.HeldMoveButtons != MoveButtons.Walk;

        if (holder == null)
            return;

        foreach (var (attachable, layerIndex) in holder.ActiveLayers)
        {
            if (!TryComp(attachable, out VehicleAttachableVisualsComponent? visualsComp) ||
                !TryComp(attachable, out VehicleAttachableComponent? attachableComp))
            {
                continue;
            }


            sprite.LayerSetAutoAnimated(layerIndex, isMoving);
        }
    }

    public override void Update(float frameTime)
    {
        var vehicleQuery = EntityQueryEnumerator<VehicleComponent>();
        while (vehicleQuery.MoveNext(out var uid, out _))
        {
            UpdateSprite(uid);
        }
    }
}
