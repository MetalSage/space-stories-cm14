using System;
using Content.Client.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Stories.Atmos;

public sealed class FireColorClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCFireColorComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<RMCFireColorComponent, ComponentStartup>(OnStartup);
    }

    private void OnState(Entity<RMCFireColorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        try
        {
            UpdateColor(ent);
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
        }
    }

    private void OnStartup(Entity<RMCFireColorComponent> ent, ref ComponentStartup args)
    {
        UpdateColor(ent);
    }

    private void UpdateColor(Entity<RMCFireColorComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        if (HasComp<TileFireComponent>(ent.Owner))
        {
            if (sprite.Color != ent.Comp.Color)
                sprite.Color = ent.Comp.Color;
        }
        else
        {
            if (sprite.LayerMapTryGet(FireVisualLayers.Fire, out var fireLayer))
            {
                sprite.LayerSetColor(fireLayer, ent.Comp.Color);
            }
        }
    }
}
