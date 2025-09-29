using Content.Client._Stories.Vehicle.Attachables;
using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

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

        UpdateDamageOverlay(uid, sprite, holder);
    }

    private void UpdateDamageOverlay(EntityUid uid, SpriteComponent sprite, VehicleAttachableHolderVisualsComponent holder)
    {
        if (!TryComp(uid, out VehicleComponent? vehicleComp))
            return;

        if (!TryComp(uid, out DamageableComponent? damageComp))
            return;

        var maxHealth = (float) vehicleComp.MaxHealth;
        var totalDamage = (float) damageComp.TotalDamage;
        var currentHealth = maxHealth - totalDamage;

        var shouldShowDamage = totalDamage > 0 && !string.IsNullOrEmpty(holder.DamagedState);

        const string damageLayerKey = "damage_overlay";
        var damageLayerIndex = -1;

        if (sprite.LayerMapTryGet(damageLayerKey, out var existingLayer))
        {
            damageLayerIndex = existingLayer;
        }
        else if (shouldShowDamage)
        {
            damageLayerIndex = sprite.AddLayer(new SpriteSpecifier.Rsi(holder.Rsi, holder.DamagedState));
            sprite.LayerMapSet(damageLayerKey, damageLayerIndex);
        }

        if (damageLayerIndex == -1)
            return;

        if (shouldShowDamage)
        {
            var healthRatio = (float)currentHealth / maxHealth;
            var damageRatio = 1.0f - healthRatio;
            var alpha = damageRatio; // BYOND: damage_overlay.alpha = 255 * (1 - (health / initial(health)))

            sprite.LayerSetVisible(damageLayerIndex, true);
            sprite.LayerSetColor(damageLayerIndex, Color.White.WithAlpha(alpha));
        }
        else
        {
            sprite.LayerSetVisible(damageLayerIndex, false);
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
