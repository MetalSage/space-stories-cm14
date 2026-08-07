using Content.Shared._RMC14.Atmos;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server._Stories.Atmos;

public sealed class FireColorServerSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCFireColorComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<RMCFireColorComponent> ent, ref ComponentStartup args)
    {
        UpdateColor(ent);
    }

    public void UpdateColor(Entity<RMCFireColorComponent> ent)
    {
        if (TryComp<PointLightComponent>(ent.Owner, out var light) && light.Color != ent.Comp.Color)
        {
            _light.SetColor(ent.Owner, ent.Comp.Color, light);
        }

        if (TryComp<RMCIgniteOnCollideComponent>(ent.Owner, out var ignite) && ignite.BurnColor != ent.Comp.Color)
        {
            ignite.BurnColor = ent.Comp.Color;
            Dirty(ent.Owner, ignite);
        }
    }
}
