using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Shields;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Coordinates;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.Empower;


public sealed partial class XenoEmpowerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly XenoShieldSystem _shield = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    private readonly HashSet<Entity<MarineComponent>> _marines = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEmpowerComponent, RemovedShieldEvent>(OnShieldRemove);
        SubscribeLocalEvent<XenoEmpowerComponent, XenoDefensiveShieldActionEvent>(OnXenoEmpowerAction);
    }

    private void OnXenoEmpowerAction(Entity<XenoEmpowerComponent> xeno, ref XenoDefensiveShieldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xenoPlasma.TryRemovePlasma(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        if (!TryComp(xeno, out TransformComponent? xform))
            return;

        _marines.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, xeno.Comp.Range, _marines);
        var shieldAmount = xeno.Comp.AmountBase;
        var empowerTargets = 0;
        foreach (var receiver in _marines)
        {
            if (empowerTargets == xeno.Comp.MaxTargets)
                break;

            if (_mobState.IsDead(receiver))
                continue;

            empowerTargets++;
            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.EffectOnMarine, receiver.Owner.ToCoordinates());
            shieldAmount += xeno.Comp.AmountPerHuman;
        }
        if (empowerTargets >= 3)
            xeno.Comp.EmpowerActive = true;

        _shield.ApplyShield(xeno, XenoShieldSystem.ShieldType.Ravager, shieldAmount);
        ApplyEffects(xeno);

        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-activate", ("user", xeno)), xeno, Filter.PvsExcept(xeno), true, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-activate-self", ("user", xeno)), xeno, xeno, PopupType.Medium);
            SpawnAttachedTo(xeno.Comp.Effect, xeno.Owner.ToCoordinates());
        }
    }


    public void ApplyEffects(Entity<XenoEmpowerComponent> ent)
    {
        if (!TryComp<CMArmorComponent>(ent, out var armor))
            return;

        ent.Comp.ShieldOffAt = _timing.CurTime + ent.Comp.ShieldDuration;
        ent.Comp.EmpowerOffAt = _timing.CurTime + ent.Comp.EmpowerDuration;
    }

    public void OnShieldRemove(Entity<XenoEmpowerComponent> ent, ref RemovedShieldEvent args)
    {
        if (!_net.IsClient || args.Type == XenoShieldSystem.ShieldType.Ravager)
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-end"), ent, ent, PopupType.MediumCaution);
    }

    public override void Update(float frameTime)
    {

        var time = _timing.CurTime;

        var ravagerQuery = EntityQueryEnumerator<XenoEmpowerComponent, XenoShieldComponent>();
        while (ravagerQuery.MoveNext(out var uid, out var crushShield, out var shield))
        {
            if (crushShield.EmpowerOffAt <= time)
                crushShield.EmpowerActive = false;

            if (shield.Active && shield.Shield == XenoShieldSystem.ShieldType.Ravager && crushShield.ShieldOffAt <= time)
                _shield.RemoveShield(uid, XenoShieldSystem.ShieldType.Ravager);
        }
    }
}
