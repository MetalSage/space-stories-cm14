using Content.Shared._Stories.APC;
using Content.Shared._Stories.Attachables;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Content.Client._Stories.APC.Attachables;
using Content.Shared._RMC14.Damage;
using Content.Shared.FixedPoint;

namespace Content.Client._Stories.APC;

public sealed class APCAttachableVisualizerSystem : VisualizerSystem<APCAttachableDamageVisualsComponent>
{
    [Dependency] private readonly APCAttachableHolderSystem _attachableHolder = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void OnAppearanceChange(EntityUid uid, APCAttachableDamageVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        UpdateSprite((uid, sprite, args.Component, null));
    }

    public void UpdateSprite(Entity<SpriteComponent?, AppearanceComponent?, DamageableComponent?, APCAttachableDamageVisualsComponent?> entity)
    {
        var (uid, sprite, appearance, damageable, attachable) = entity;

        if (!Resolve(uid, ref sprite, ref appearance, ref damageable, false))
            return;

        if (!Resolve(uid, ref attachable, false))
            return;

        if (sprite is not { BaseRSI: { } rsi } ||
            !sprite.LayerMapTryGet(APCAttachableVisualLayers.Base, out var layer))
        {
            return;
        }

        if (!TryComp<MaxDamageComponent>(entity, out var maxDamageComp) || maxDamageComp.Max == FixedPoint2.Zero)
            return;

        var ratio = Math.Clamp((float)(damageable.TotalDamage / maxDamageComp.Max), 0f, 1f);
        var brightness = (byte)(255 * (1f - ratio * attachable.DarknessLevel)); 
        var color = new Color(brightness, brightness, brightness, 255);

        SetAttachedColor(uid, color);
        sprite.Color = color;
    }

    private void SetAttachedColor(EntityUid attachable, Color color)
    {
        if (!_attachableHolder.TryGetHolder(attachable, out var holder))
            return;

        if (!TryComp<SpriteComponent>(holder, out var holderSprite) || 
            !TryComp<APCAttachableHolderVisualsComponent>(holder, out var holderVisuals))
        {
            return;
        }

        if (!holderVisuals.ActiveLayers.TryGetValue(attachable, out var layerIndex))
            return;

        holderSprite.LayerSetColor(layerIndex, color);
    }

    public override void Update(float frameTime)
    {
        var apcQuery = EntityQueryEnumerator<APCAttachableDamageVisualsComponent>();
        while (apcQuery.MoveNext(out var uid, out _))
        {
            UpdateSprite(uid);
        }
    }
}
