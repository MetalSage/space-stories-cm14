using Robust.Shared.Prototypes;
using Content.Shared._Stories.APC;
using System.Linq;
using Content.Shared.Coordinates;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public void InitializeModules()
    {
    }

    public void SetupModule(Entity<APCEntityComponent> apc, EntityUid module)
        => SetupModules(apc, new[] { module });

    public void SetupModule(Entity<APCEntityComponent> apc, List<string?> prototypes)
    {
        var modules = new List<EntityUid>();
        var coordinates = apc.Owner.ToCoordinates();

        foreach (var protoId in prototypes)
        {

            if (protoId == null)
                continue;

            var offset = _proto.TryIndex<EntityPrototype>(protoId, out var proto) &&
                        proto.TryGetComponent<APCModuleComponent>(out var moduleComp)
                ? moduleComp.Offset
                : Vector2i.Zero;

            modules.Add(SpawnAttachedTo(protoId!, coordinates.Offset(offset)));
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