using System.Linq;
using Content.Shared._Stories.Attachables;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Stories.Vehicle.Attachables;

public sealed class VehicleAttachableHolderVisuals : EntitySystem
{
    [Dependency] private readonly VehicleAttachableHolderSystem _attachableHolder = default!;

    private readonly HashSet<EntityUid> _destroyedAttachables = new();

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

        if (!holder.Comp.ActiveLayers.TryGetValue(attachable, out var removedIndex))
            return;

        holderSprite.RemoveLayer(removedIndex);
        holder.Comp.ActiveLayers.Remove(attachable);

        var layersToUpdate = new List<(EntityUid key, int newIndex)>();
        foreach (var kvp in holder.Comp.ActiveLayers)
        {
            if (kvp.Value > removedIndex)
                layersToUpdate.Add((kvp.Key, kvp.Value - 1));
        }

        foreach (var (key, newIndex) in layersToUpdate)
        {
            holder.Comp.ActiveLayers[key] = newIndex;
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

        var state = attachable.Comp.State;

        if (_destroyedAttachables.Contains(attachable.Owner))
            state = attachable.Comp.DestroyedState;

        var layerData = new PrototypeLayerData()
        {
            RsiPath = rsi,
            State = state,
            Offset = attachable.Comp.Offset,
            Visible = true,
        };

        var newIndex = holderSprite.AddLayer(layerData);
        holder.Comp.ActiveLayers[attachable.Owner] = newIndex;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleAttachableHolderVisualsComponent>();
        while (query.MoveNext(out var holderUid, out var holderComp))
        {
            var layersNeedingUpdate = new List<(EntityUid attachableUid, VehicleAttachableVisualsComponent attachable)>();

            foreach (var kvp in holderComp.ActiveLayers)
            {
                var attachableUid = kvp.Key;

                if (!TryComp<VehicleAttachableVisualsComponent>(attachableUid, out var attachable))
                    continue;

                if (!TryComp<VehicleAttachableComponent>(attachableUid, out var attachableComp))
                    continue;

                var isDestroyed = attachableComp.Destroyed;
                var wasDestroyed = _destroyedAttachables.Contains(attachableUid);

                if (isDestroyed != wasDestroyed)
                {
                    if (isDestroyed)
                        _destroyedAttachables.Add(attachableUid);
                    else
                        _destroyedAttachables.Remove(attachableUid);

                    layersNeedingUpdate.Add((attachableUid, attachable));
                }
            }

            foreach (var (attachableUid, attachable) in layersNeedingUpdate)
            {
                RefreshVisuals((holderUid, holderComp), (attachableUid, attachable));
            }
        }
    }
}
