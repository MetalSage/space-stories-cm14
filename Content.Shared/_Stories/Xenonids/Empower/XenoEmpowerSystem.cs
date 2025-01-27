using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Shields;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Coordinates;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Content.Shared.Weapons.Melee;

namespace Content.Shared._RMC14.Xenonids.Empower;


public sealed partial class XenoEmpowerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly XenoShieldSystem _shield = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    private EntityQuery<MeleeWeaponComponent> _melee;
    private readonly HashSet<Entity<MarineComponent>> _marines = new();

    public override void Initialize()
    {
        base.Initialize();

        _melee = GetEntityQuery<MeleeWeaponComponent>();

        SubscribeLocalEvent<XenoEmpowerComponent, RemovedShieldEvent>(OnShieldRemove);
        SubscribeLocalEvent<XenoEmpowerComponent, XenoDefensiveShieldActionEvent>(OnXenoFirstEmpowerAction);
        SubscribeLocalEvent<XenoEmpowerComponent, XenoEmpowerActionEvent>(OnXenoEmpowerAction);
    }

    private void OnXenoFirstEmpowerAction(Entity<XenoEmpowerComponent> xeno, ref XenoDefensiveShieldActionEvent args)
    {
        if (args.Handled)
            return;
        if (!xeno.Comp.FirstActive)
        {
            xeno.Comp.FirstActiveOffAt = _timing.CurTime + xeno.Comp.FirstActiveDuration;
            xeno.Comp.FirstActive = true;
        }
        else if(xeno.Comp.FirstActive)
        {
            xeno.Comp.FirstActive = false;
            args.Handled = true;
            var ev = new XenoEmpowerActionEvent();
            RaiseLocalEvent(xeno, ev);
            return;
        }

        if (!_xenoPlasma.TryRemovePlasma(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        _shield.ApplyShield(xeno, XenoShieldSystem.ShieldType.Ravager, xeno.Comp.AmountBase);
        ApplyEffects(xeno);
        Dirty(xeno);
    }

    private void OnXenoEmpowerAction(Entity<XenoEmpowerComponent> xeno, ref XenoEmpowerActionEvent args)
    {
        if (args.Handled)
            return;

        SetEmpowerDelays(xeno);

        if (!TryComp(xeno, out TransformComponent? xform))
            return;

        _marines.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, xeno.Comp.Range, _marines);
        FixedPoint2 shieldAmount = 0;
        var empowerTargets = 0;
        foreach (var receiver in _marines)
        {
            if (empowerTargets >= xeno.Comp.MaxTargets)
                break;

            if (_mobState.IsDead(receiver))
                continue;

            empowerTargets++;
            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.EffectOnMarine, receiver.Owner.ToCoordinates());
            shieldAmount += xeno.Comp.AmountPerHuman;
        }

        if (empowerTargets >= 3)
        {
            xeno.Comp.EmpowerActive = true;
            if (_melee.TryGetComponent(xeno, out var melee))
            {
                FixedPoint2 DamageMod = (45 + empowerTargets * 2) / 3;
                melee.Damage.DamageDict["Blunt"] = DamageMod;
                melee.Damage.DamageDict["Slash"] = DamageMod;
                melee.Damage.DamageDict["Piercing"] = DamageMod;
            }
        }

        _shield.ApplyShield(xeno, XenoShieldSystem.ShieldType.Ravager, shieldAmount);
        ApplyEffects(xeno);
        Dirty(xeno);

        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-activate", ("user", xeno)), xeno, Filter.PvsExcept(xeno), true, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-activate-self", ("user", xeno)), xeno, xeno, PopupType.Medium);
            SpawnAttachedTo(xeno.Comp.Effect, xeno.Owner.ToCoordinates());
        }
    }
    private void SetEmpowerDelays(Entity<XenoEmpowerComponent> xeno)
    {
        EntityUid? empower = null;

        foreach (var (id, action) in _actions.GetActions(xeno))
        {
            if (action.BaseEvent is XenoDefensiveShieldActionEvent)
            {
                empower = id;
                break;
            }
        }

        if (empower == null)
            return;

        var empowerCooldownTime = TimeSpan.FromSeconds(18);

        _actions.SetUseDelay(empower, empowerCooldownTime);
        _actions.SetCooldown(empower, empowerCooldownTime);
        Dirty(xeno);
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
        if (!_net.IsClient && args.Type == XenoShieldSystem.ShieldType.Ravager)
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-end"), ent, ent, PopupType.MediumCaution);
    }

    public override void Update(float frameTime)
    {

        var time = _timing.CurTime;

        var ravagerQuery = EntityQueryEnumerator<XenoEmpowerComponent, XenoShieldComponent>();
        while (ravagerQuery.MoveNext(out var uid, out var xeno, out var shield))
        {

            if ((time > xeno.FirstActiveOffAt) && xeno.FirstActive)
            {
                xeno.FirstActive = false;
                var ev = new XenoEmpowerActionEvent();
                RaiseLocalEvent(uid, ev);
                continue;
            }

            if ((xeno.EmpowerOffAt <= time) && xeno.EmpowerActive)
            {
                xeno.EmpowerActive = false;
                if (_melee.TryGetComponent(uid, out var melee))
                {
                    melee.Damage.DamageDict["Blunt"] = 15;
                    melee.Damage.DamageDict["Slash"] = 15;
                    melee.Damage.DamageDict["Piercing"] = 15;
                }
            }

            if (shield.Active && shield.Shield == XenoShieldSystem.ShieldType.Ravager && xeno.ShieldOffAt <= time)
                _shield.RemoveShield(uid, XenoShieldSystem.ShieldType.Ravager);
        }
    }
}
