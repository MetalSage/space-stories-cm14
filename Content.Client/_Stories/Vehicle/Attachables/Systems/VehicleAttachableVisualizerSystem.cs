using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Attachables;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Content.Client._Stories.Vehicle.Attachables;
using Content.Shared._RMC14.Damage;
using Content.Shared.FixedPoint;

namespace Content.Client._Stories.Vehicle;

public sealed class VehicleAttachableVisualizerSystem : VisualizerSystem<VehicleAttachableDamageVisualsComponent>
{
    [Dependency] private readonly VehicleAttachableHolderSystem _attachableHolder = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void OnAppearanceChange(EntityUid uid, VehicleAttachableDamageVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        UpdateSprite((uid, sprite, args.Component, null));
    }

    public void UpdateSprite(Entity<SpriteComponent?, AppearanceComponent?, DamageableComponent?, VehicleAttachableDamageVisualsComponent?> entity)
    {
        var (uid, sprite, appearance, damageable, attachableVisuals) = entity;

        if (!Resolve(uid, ref sprite, ref appearance, ref damageable, false))
            return;

        if (!Resolve(uid, ref attachableVisuals, false))
            return;

        if (sprite is not { BaseRSI: { } rsi } ||
            !sprite.LayerMapTryGet(VehicleAttachableVisualLayers.Base, out var layer))
        {
            return;
        }

        if (!TryComp<VehicleAttachableComponent>(entity, out var attachable) || attachable.MaxHealth <= FixedPoint2.Zero)
            return;

        Color color;

        if (attachable.Destroyed)
        {
            color = Color.White;
        }
        else
        {
            var ratio = Math.Clamp((float)(damageable.TotalDamage / attachable.MaxHealth), 0f, 1f);
            var brightness = (byte)(255 * (1f - ratio * attachableVisuals.DarknessLevel));
            color = new Color(brightness, brightness, brightness, 255);
        }

        SetAttachedColor(uid, color);
        sprite.Color = color;
    }

    private void SetAttachedColor(EntityUid attachable, Color color)
    {
        if (!_attachableHolder.TryGetHolder(attachable, out var holder))
            return;

        if (!TryComp<SpriteComponent>(holder, out var holderSprite) || 
            !TryComp<VehicleAttachableHolderVisualsComponent>(holder, out var holderVisuals))
        {
            return;
        }

        if (!holderVisuals.ActiveLayers.TryGetValue(attachable, out var layerIndex))
            return;

        holderSprite.LayerSetColor(layerIndex, color);
    }

    public override void Update(float frameTime)
    {
        var vehicleQuery = EntityQueryEnumerator<VehicleAttachableDamageVisualsComponent>();
        while (vehicleQuery.MoveNext(out var uid, out _))
        {
            UpdateSprite(uid);
        }
    }
}
