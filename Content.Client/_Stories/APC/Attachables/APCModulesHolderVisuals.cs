/*
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using Content.Shared._Stories.APC;

namespace Content.Client._Stories.APC.Modules;

public sealed class APCModulesHolderVisuals : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCModulesHolderVisualsComponent, EntRemovedFromContainerMessage>(OnDetached);
        SubscribeLocalEvent<APCModulesHolderVisualsComponent, APCAttachableAlteredEvent>(OnAttachablesAltered);

        // SubscribeLocalEvent<APCModulesVisualsComponent, AppearanceChangeEvent>(OnAttachableAppearanceChange);
    }

    private void OnDetached(Entity<APCModulesHolderVisualsComponent> holder, ref EntRemovedFromContainerMessage args)
    {

        if (!HasComp<APCModulesVisualsComponent>(args.Entity))
            return;

        var holderEv = new APCAttachableAlteredEvent(args.Entity, APCModulesAlteredType.Detached);
        RaiseLocalEvent(holder, ref holderEv);
    }

    private void OnAttachablesAltered(Entity<APCModulesHolderVisualsComponent> holder,
        ref APCAttachableAlteredEvent args)
    {

        if (!TryComp(args.Module, out APCModulesVisualsComponent? attachableComponent))
            return;

        var attachable = new Entity<APCModulesVisualsComponent>(args.Module, attachableComponent);
        switch (args.Alteration)
        {
            case APCModulesAlteredType.Attached:
                SetAttachableOverlay(holder, attachable);
                break;

            case APCModulesAlteredType.Detached:
                RemoveAttachableOverlay(holder, attachable);
                break;

            case APCModulesAlteredType.AppearanceChanged:
                SetAttachableOverlay(holder, attachable);
                break;
        }
    }

    private void RemoveAttachableOverlay(Entity<APCModulesHolderVisualsComponent> holder, EntityUid attachable)
    {
        if (!TryComp(holder, out SpriteComponent? holderSprite))
            return;

        if (holder.Comp.ActiveLayers.TryGetValue(attachable, out var index))
        {
            holderSprite.RemoveLayer(index);
            holder.Comp.ActiveLayers.Remove(attachable);
        }
    }

    private void SetAttachableOverlay(Entity<APCModulesHolderVisualsComponent> holder,
        Entity<APCModulesVisualsComponent> attachable)
    {
        RefreshVisuals(holder, attachable);
    }

    public void RefreshVisuals(Entity<APCModulesHolderVisualsComponent> holder, Entity<APCModulesVisualsComponent> attachable)
    {
        if (!TryComp(holder, out SpriteComponent? holderSprite))
            return;

        if (!TryComp(attachable, out SpriteComponent? attachableSprite))
            return;

        var actualRsi = attachable.Comp.Rsi ?? attachableSprite.LayerGetActualRSI(attachable.Comp.Layer)?.Path;
        var rsi = actualRsi?.ToString();

        if (rsi == null)
            return;

        var state = attachable.Comp.State;

        Logger.Info($"Creating new layer RSI={rsi}, State={state}, Offset={attachable.Comp.Offset}");

        var layerData = new PrototypeLayerData()
        {
            RsiPath = rsi,
            State = state,
            Offset = attachable.Comp.Offset,
            Visible = true,
        };

        var newIndex = holderSprite.AddLayer(layerData);
        holder.Comp.ActiveLayers[attachable] = newIndex;

        Logger.Info($"New layer {newIndex} added for attachable {attachable.Owner}");
    }
}
*/