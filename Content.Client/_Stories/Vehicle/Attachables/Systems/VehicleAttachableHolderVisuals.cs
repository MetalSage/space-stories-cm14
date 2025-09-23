using Content.Shared._Stories.Attachables;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Stories.Vehicle.Attachables;

public sealed class VehicleAttachableHolderVisuals : EntitySystem
{
    [Dependency] private readonly VehicleAttachableHolderSystem _attachableHolder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleAttachableHolderVisualsComponent, EntRemovedFromContainerMessage>(OnDetached);
        SubscribeLocalEvent<VehicleAttachableHolderVisualsComponent, VehicleAttachableHolderAttachablesAlteredEvent>(OnAttachablesAltered);

        SubscribeLocalEvent<VehicleAttachableVisualsComponent, AppearanceChangeEvent>(OnAttachableAppearanceChange);
    }

    private void OnDetached(Entity<VehicleAttachableHolderVisualsComponent> holder, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<VehicleAttachableVisualsComponent>(args.Entity) || !_attachableHolder.HasSlot(holder.Owner, args.Container.ID))
            return;

        var holderEv = new VehicleAttachableHolderAttachablesAlteredEvent(args.Entity, args.Container.ID, VehicleAttachableAlteredType.Detached);
        RaiseLocalEvent(holder, ref holderEv);
    }

    private void OnAttachablesAltered(Entity<VehicleAttachableHolderVisualsComponent> holder,
        ref VehicleAttachableHolderAttachablesAlteredEvent args)
    {
        if (!TryComp(args.Attachable, out VehicleAttachableVisualsComponent? attachableComponent))
            return;

        var attachable = new Entity<VehicleAttachableVisualsComponent>(args.Attachable, attachableComponent);

        switch (args.Alteration)
        {
            case VehicleAttachableAlteredType.Attached:
                SetAttachableOverlay(holder, attachable);
                break;

            case VehicleAttachableAlteredType.Detached:
                RemoveAttachableOverlay(holder, attachable);
                break;

            case VehicleAttachableAlteredType.AppearanceChanged:
                SetAttachableOverlay(holder, attachable);
                break;
        }
    }

    private void OnAttachableAppearanceChange(Entity<VehicleAttachableVisualsComponent> attachable, ref AppearanceChangeEvent args)
    {
        if (!attachable.Comp.RedrawOnAppearanceChange ||
            !_attachableHolder.TryGetHolder(attachable.Owner, out var holderUid) ||
            !_attachableHolder.TryGetSlotId(holderUid.Value, attachable.Owner, out var slotId))
        {
            return;
        }

        var holderEvent = new VehicleAttachableHolderAttachablesAlteredEvent(
            attachable.Owner, slotId,
            VehicleAttachableAlteredType.AppearanceChanged);

        RaiseLocalEvent(holderUid.Value, ref holderEvent);
    }

    private void RemoveAttachableOverlay(Entity<VehicleAttachableHolderVisualsComponent> holder, EntityUid attachable)
    {
        if (!TryComp(holder, out SpriteComponent? holderSprite))
            return;

        if (holder.Comp.ActiveLayers.TryGetValue(attachable, out var index))
        {
            holderSprite.RemoveLayer(index);
            holder.Comp.ActiveLayers.Remove(attachable);
        }
    }

    private void SetAttachableOverlay(Entity<VehicleAttachableHolderVisualsComponent> holder,
        Entity<VehicleAttachableVisualsComponent> attachable)
    {
        RefreshVisuals(holder, attachable);
    }

    public void RefreshVisuals(Entity<VehicleAttachableHolderVisualsComponent> holder, Entity<VehicleAttachableVisualsComponent> attachable)
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
