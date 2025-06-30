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

public sealed class APCattachableVisualizerSystem : VisualizerSystem<APCAttachableDamageVisualsComponent>
{
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

        Resolve(uid, ref attachable, false);

        if (sprite is not { BaseRSI: { } rsi } ||
            !sprite.LayerMapTryGet(APCAttachableVisualLayers.Base, out var layer))
        {
            return;
        }

        if (!TryComp<MaxDamageComponent>(entity, out var maxDamageComp) || maxDamageComp.Max == FixedPoint2.Zero)
            return;

        var ratio = Math.Clamp((float)(damageable.TotalDamage / maxDamageComp.Max), 0f, 1f);
        var brightness = (byte)(255 * (1f - ratio * 0.8f)); 
        var color = new Color(brightness, brightness, brightness, 255);

        sprite.Color = color;

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
