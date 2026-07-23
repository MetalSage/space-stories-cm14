using Content.Shared._RMC14.ARES.ExternalTerminals;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared._Stories.Synth;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.ARES.CoreGas;

public sealed class STARESCoreGasSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> GasDamage = "Poison";
    private const float ZoneRadius = 0.6f;

    [Dependency] private readonly ARESCoreSystem _aresCore = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    private EntityQuery<SynthComponent> _synthQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<XenoWeedsComponent> _weedsQuery;

    private readonly HashSet<EntityUid> _hit = new();

    public override void Initialize()
    {
        base.Initialize();

        _synthQuery = GetEntityQuery<SynthComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _weedsQuery = GetEntityQuery<XenoWeedsComponent>();

        Subs.BuiEvents<ARESExternalTerminalComponent>(ARESExternalTerminalUIKey.Key,
            subs => subs.Event<RMCARESExternalReleaseGas>(OnReleaseGas));
    }

    private void OnReleaseGas(Entity<ARESExternalTerminalComponent> ent, ref RMCARESExternalReleaseGas args)
    {
        if (!ent.Comp.LoggedIn || !ent.Comp.ShowCore)
            return;

        if (!_aresCore.TryGetARES(ent.Comp.Faction, out var ares) || ares is not { } core)
            return;

        var time = _timing.CurTime;
        if (time < core.Comp.NextGasRelease)
        {
            _popup.PopupClient(Loc.GetString("st-ares-core-gas-cooldown"), ent.Owner, args.Actor, PopupType.SmallCaution);
            return;
        }

        core.Comp.NextGasRelease = time + core.Comp.GasReleaseCooldown;
        Dirty(core);

        if (_net.IsServer)
            ReleaseGas(core);

        _popup.PopupClient(Loc.GetString("st-ares-core-gas-released"), ent.Owner, args.Actor);
    }

    private void ReleaseGas(Entity<ARESCoreComponent> core)
    {
        var coreMap = _transform.GetMapId(core.Owner);

        var query = EntityQueryEnumerator<STARESCoreGasZoneComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var zoneXform))
        {
            if (zoneXform.MapID != coreMap)
                continue;

            _hit.Clear();
            _lookup.GetEntitiesInRange(zoneXform.Coordinates, ZoneRadius, _hit);

            foreach (var target in _hit)
            {
                if (_synthQuery.HasComp(target))
                    continue;

                if (_mobStateQuery.HasComp(target))
                {
                    var damage = new DamageSpecifier(_prototype.Index(GasDamage), 1000);
                    _damageable.TryChangeDamage(target, damage, true, origin: core.Owner);
                    continue;
                }

                if (_weedsQuery.HasComp(target))
                    QueueDel(target);
            }
        }
    }
}
