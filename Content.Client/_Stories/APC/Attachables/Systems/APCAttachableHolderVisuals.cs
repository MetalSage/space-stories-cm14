using Content.Shared._Stories.Attachables;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Stories.APC.Attachables;

public sealed class APCAttachableHolderVisuals : EntitySystem
{
    [Dependency] private readonly APCAttachableHolderSystem _attachableHolder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCAttachableHolderVisualsComponent, EntRemovedFromContainerMessage>(OnDetached);
        SubscribeLocalEvent<APCAttachableHolderVisualsComponent, APCAttachableHolderAttachablesAlteredEvent>(OnAttachablesAltered);

        SubscribeLocalEvent<APCAttachableVisualsComponent, AppearanceChangeEvent>(OnAttachableAppearanceChange);
    }

    private void OnDetached(Entity<APCAttachableHolderVisualsComponent> holder, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<APCAttachableVisualsComponent>(args.Entity) || !_attachableHolder.HasSlot(holder.Owner, args.Container.ID))
            return;

        var holderEv = new APCAttachableHolderAttachablesAlteredEvent(args.Entity, args.Container.ID, APCAttachableAlteredType.Detached);
        RaiseLocalEvent(holder, ref holderEv);
    }

    private void OnAttachablesAltered(Entity<APCAttachableHolderVisualsComponent> holder,
        ref APCAttachableHolderAttachablesAlteredEvent args)
    {
        if (!TryComp(args.Attachable, out APCAttachableVisualsComponent? attachableComponent))
            return;

        var attachable = new Entity<APCAttachableVisualsComponent>(args.Attachable, attachableComponent);

        switch (args.Alteration)
        {
            case APCAttachableAlteredType.Attached:
                SetAttachableOverlay(holder, attachable);
                break;

            case APCAttachableAlteredType.Detached:
                RemoveAttachableOverlay(holder, attachable);
                break;

            case APCAttachableAlteredType.AppearanceChanged:
                SetAttachableOverlay(holder, attachable);
                break;
        }
    }

    private void OnAttachableAppearanceChange(Entity<APCAttachableVisualsComponent> attachable, ref AppearanceChangeEvent args)
    {
        if (!attachable.Comp.RedrawOnAppearanceChange ||
            !_attachableHolder.TryGetHolder(attachable.Owner, out var holderUid) ||
            !_attachableHolder.TryGetSlotId(holderUid.Value, attachable.Owner, out var slotId))
        {
            return;
        }

        var holderEvent = new APCAttachableHolderAttachablesAlteredEvent(
            attachable.Owner, slotId,
            APCAttachableAlteredType.AppearanceChanged);

        RaiseLocalEvent(holderUid.Value, ref holderEvent);
    }

    private void RemoveAttachableOverlay(Entity<APCAttachableHolderVisualsComponent> holder, EntityUid attachable)
    {
        if (!TryComp(holder, out SpriteComponent? holderSprite))
            return;

        if (holder.Comp.ActiveLayers.TryGetValue(attachable, out var index))
        {
            holderSprite.RemoveLayer(index);
            holder.Comp.ActiveLayers.Remove(attachable);
        }
    }

    private void SetAttachableOverlay(Entity<APCAttachableHolderVisualsComponent> holder,
        Entity<APCAttachableVisualsComponent> attachable)
    {
        RefreshVisuals(holder, attachable);
    }

    public void RefreshVisuals(Entity<APCAttachableHolderVisualsComponent> holder, Entity<APCAttachableVisualsComponent> attachable)
    {
        RemoveAttachableOverlay(holder, attachable.Owner);
        if (!TryComp(holder, out SpriteComponent? holderSprite))
            return;

        if (!TryComp(attachable, out SpriteComponent? attachableSprite))
            return;

        var actualRsi = attachable.Comp.Rsi ?? attachableSprite.LayerGetActualRSI(attachable.Comp.Layer)?.Path;

        if (actualRsi?.ToString() is not { } rsi)
            return;

        var layerData = new PrototypeLayerData()
        {
            RsiPath = rsi,
            State = attachable.Comp.State,
            Offset = attachable.Comp.Offset,
            Visible = true,
        };

        var newIndex = holderSprite.AddLayer(layerData);
        holder.Comp.ActiveLayers[attachable] = newIndex;
    }
}
