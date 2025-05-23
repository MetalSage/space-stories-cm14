using Robust.Shared.Prototypes;
using Content.Shared._Stories.APC;
using System.Linq;
using Content.Shared.Coordinates;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using System.Numerics;
using Content.Shared._Stories.APC;
using Content.Shared._Stories.APC.Systems;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public void InitializeModules()
    {
    }

    public void SetupStartingModules(Entity<APCEntityComponent> apc, List<EntProtoId?> prototypes)
    {
        var coordinates = new EntityCoordinates(apc.Owner, Vector2.Zero);
        var xform = Transform(apc.Owner);
        var modules = new List<EntityUid>();

        foreach (var protoId in prototypes)
        {
            if (protoId == null)
                continue;

            var module = Spawn(protoId, coordinates);
            if (!_container.Insert(module, apc.Comp.ModulesContainer, containerXform: xform))
                continue;

            modules.Add(module);
        }

        SetupModulesInternal(apc, modules);
        Dirty(apc);
    }

    public void SetupModule(Entity<APCEntityComponent> apc, EntityUid module)
        => SetupModules(apc, new List<EntityUid?> { module });

    public void SetupModules(Entity<APCEntityComponent> apc, List<EntityUid?> modules)
    {
        var coordinates = new EntityCoordinates(apc.Owner, Vector2.Zero);
        var xform = Transform(apc.Owner);
        var insertedModules = new List<EntityUid>();

        foreach (var module in modules)
        {
            if (module == null)
                continue;

            if (!TryComp<MetaDataComponent>(module.Value, out var meta) || meta.EntityPrototype == null)
                continue;

            var protoId = meta.EntityPrototype.ID;

            if (apc.Comp.ModulesContainer.ContainedEntities.Any(existing =>
                TryComp<MetaDataComponent>(existing, out var existingMeta) &&
                existingMeta.EntityPrototype != null &&
                existingMeta.EntityPrototype.ID == protoId && TryComp<APCModuleComponent>(existing, out var existingModule) && 
                existingModule.Offset == Vector2.Zero))
            {
                _popup.PopupEntity($"Модуль {ToPrettyString(module.Value)} уже установлен в {ToPrettyString(apc)}", apc);
                continue;
            }

            if (!_container.Insert(module.Value, apc.Comp.ModulesContainer, containerXform: xform))
                continue;

            insertedModules.Add(module.Value);
        }

        SetupModulesInternal(apc, insertedModules);
    }

    private void SetupModulesInternal(Entity<APCEntityComponent> apc, IEnumerable<EntityUid> modules)
    {
        foreach (var module in modules)
        {
            if (module == null)
                continue;

            if (!TryComp<APCModuleComponent>(module, out var moduleComp))
                continue;

            if (moduleComp.VirtualModule == null)
                continue;

            moduleComp.APC = apc.Owner;
            moduleComp.VirtualModuleEnt = SpawnAttachedTo(moduleComp.VirtualModule, apc.Owner.ToCoordinates().Offset(moduleComp.Offset));
            if (moduleComp.VirtualModuleEnt != null)
                apc.Comp.VirtualModules.Add(moduleComp.VirtualModuleEnt.Value);

            var ev = new APCModuleAttachedEvent(GetNetEntity(apc.Owner), GetNetEntity(module));
            RaiseLocalEvent(apc.Owner, ref ev);
        }
    }
}
