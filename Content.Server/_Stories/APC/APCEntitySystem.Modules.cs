using Robust.Shared.Prototypes;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public void SetupModule(Entity<APCEntityComponent> apc, EntityUid module)
        => SetupModules(apc, new[] { module });

    public void SetupModule(Entity<APCEntityComponent> apc, List<string?> prototypes)
    {
        var modules = new List<EntityUid>();
        var coordinates = apc.Owner.ToCoordinates();

        foreach (var protoId in prototypes.Where(p => p != null))
        {
            var offset = _proto.TryIndex<EntityPrototype>(protoId, out var proto) &&
                        proto.TryGetComponent<APCModuleComponent>(out var moduleComp)
                ? moduleComp.Offset
                : Vector2i.Zero;

            modules.Add(SpawnEntityAttachedTo(coordinates.Offset(offset), protoId!));
        }

        SetupModules(apc, modules);
    }

    private void SetupModules(Entity<APCEntityComponent> apc, IEnumerable<EntityUid> modules)
    {
        apc.Comp.Modules = modules.ToList();
        
        foreach (var module in apc.Comp.Modules)
            EnsureComp<APCModuleComponent>(module).APC = apc;
    }
}