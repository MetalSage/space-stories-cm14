using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Camera;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Crusher;

public sealed class STCrusherShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CMArmorSystem _armor = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly XenoShieldSystem _xenoShield = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly ProtoId<AlertPrototype> BarAlert = "STXenoEnergyCrusher";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCrusherShieldComponent, XenoDefensiveShieldActionEvent>(OnToggleAction);
        SubscribeLocalEvent<STCrusherShieldComponent, DamageModifyAfterResistEvent>(OnDamageModify, before: [typeof(XenoShieldSystem)]);
        SubscribeLocalEvent<STCrusherShieldComponent, RemovedShieldEvent>(OnShieldRemoved);
        SubscribeLocalEvent<STCrusherShieldComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<STCrusherShieldBreakBurstComponent, RefreshMovementSpeedModifiersEvent>(OnBurstRefreshSpeed);
        SubscribeLocalEvent<STCrusherShieldBreakBurstComponent, ComponentShutdown>(OnBurstShutdown);
    }

    public bool IsSlowImmune(EntityUid uid)
    {
        return HasComp<STCrusherShieldBreakBurstComponent>(uid);
    }

    private void OnBurstRefreshSpeed(Entity<STCrusherShieldBreakBurstComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime >= ent.Comp.EndsAt)
            return;

        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnBurstShutdown(Entity<STCrusherShieldBreakBurstComponent> ent, ref ComponentShutdown args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnToggleAction(Entity<STCrusherShieldComponent> xeno, ref XenoDefensiveShieldActionEvent args)
    {
        if (args.Handled)
            return;

        if (xeno.Comp.State == STCrusherShieldState.Broken)
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-defensive-shield-broken"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (_timing.CurTime < xeno.Comp.NextToggleAt)
            return;

        args.Handled = true;
        xeno.Comp.NextToggleAt = _timing.CurTime + xeno.Comp.ToggleDelay;

        if (xeno.Comp.State == STCrusherShieldState.Active)
        {
            DeactivateShield(xeno);
            return;
        }

        if (HasComp<OnFireComponent>(xeno.Owner))
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-defensive-shield-burning"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        ActivateShield(xeno);
    }

    private void ActivateShield(Entity<STCrusherShieldComponent> xeno)
    {
        xeno.Comp.State = STCrusherShieldState.Active;

        var restoredAmount = xeno.Comp.SavedPool < FixedPoint2.Zero ? xeno.Comp.MaxPool : xeno.Comp.SavedPool;
        Dirty(xeno);

        _xenoShield.ApplyShield(xeno.Owner, XenoShieldSystem.ShieldType.Crusher, restoredAmount, maxShield: xeno.Comp.MaxPool.Double());
        _aura.GiveAura(xeno.Owner, Color.White, null);

        if (_net.IsServer)
            _actions.SetToggled(xeno.Owner, true);
    }

    private void DeactivateShield(Entity<STCrusherShieldComponent> xeno)
    {
        xeno.Comp.State = STCrusherShieldState.Off;

        xeno.Comp.SavedPool = TryComp<XenoShieldComponent>(xeno.Owner, out var pool) && pool.Active
            ? pool.ShieldAmount
            : xeno.Comp.SavedPool;
        Dirty(xeno);

        _xenoShield.RemoveShield(xeno.Owner, XenoShieldSystem.ShieldType.Crusher);
        RemCompDeferred<AuraComponent>(xeno.Owner);

        if (_net.IsServer)
            _actions.SetToggled(xeno.Owner, false);
    }

    private void OnDamageModify(Entity<STCrusherShieldComponent> xeno, ref DamageModifyAfterResistEvent args)
    {
        if (args.Damage.DamageDict.TryGetValue(HeatDamageType, out var heat) && heat > FixedPoint2.Zero)
            return;

        switch (xeno.Comp.State)
        {
            case STCrusherShieldState.Active:
                xeno.Comp.LastDamageAt = _timing.CurTime;
                Dirty(xeno);

                foreach (var type in args.Damage.DamageDict)
                {
                    if (args.Damage.DamageDict[type.Key] <= 0)
                        continue;

                    args.Damage.DamageDict[type.Key] = FixedPoint2.Max(0, args.Damage.DamageDict[type.Key] - xeno.Comp.FlatReduction);
                }

                break;
            case STCrusherShieldState.Off:
                var resist = GetHpResistPercent(xeno);
                if (resist <= FixedPoint2.Zero)
                    return;

                foreach (var type in args.Damage.DamageDict)
                {
                    if (args.Damage.DamageDict[type.Key] <= 0)
                        continue;

                    args.Damage.DamageDict[type.Key] *= 1 - resist;
                }

                break;
            case STCrusherShieldState.Broken:
                break;
        }
    }

    private FixedPoint2 GetHpResistPercent(Entity<STCrusherShieldComponent> xeno)
    {
        if (!TryComp<DamageableComponent>(xeno.Owner, out var damageable) ||
            !TryComp<MobThresholdsComponent>(xeno.Owner, out var thresholds))
        {
            return xeno.Comp.MinResistPercent;
        }

        FixedPoint2? dead = null;
        foreach (var (damage, state) in thresholds.Thresholds)
        {
            if (state == Content.Shared.Mobs.MobState.Dead)
                dead = damage;
        }

        if (dead is not { } deadThreshold || deadThreshold <= FixedPoint2.Zero)
            return xeno.Comp.MinResistPercent;

        var damageAt40PercentHp = deadThreshold * xeno.Comp.MaxResistHpFraction;
        var fraction = FixedPoint2.Min(1, damageable.TotalDamage / damageAt40PercentHp);
        return xeno.Comp.MinResistPercent + (xeno.Comp.MaxResistPercent - xeno.Comp.MinResistPercent) * fraction.Float();
    }

    private void OnShieldRemoved(Entity<STCrusherShieldComponent> xeno, ref RemovedShieldEvent args)
    {
        if (args.Type != XenoShieldSystem.ShieldType.Crusher)
            return;

        if (xeno.Comp.State != STCrusherShieldState.Active)
            return;

        xeno.Comp.State = STCrusherShieldState.Broken;
        xeno.Comp.SavedPool = FixedPoint2.Zero;
        xeno.Comp.LockoutEndsAt = _timing.CurTime + xeno.Comp.BreakLockoutTime;
        Dirty(xeno);
        _aura.GiveAura(xeno.Owner, Color.Gray, xeno.Comp.BreakLockoutTime);
        _armor.UpdateArmorValue(xeno.Owner);

        if (_net.IsServer)
        {
            _actions.SetToggled(xeno.Owner, false);
            _audio.PlayPvs(xeno.Comp.BreakSound, xeno);
        }

        var origin = _transform.GetMapCoordinates(xeno.Owner);
        _cameraRecoil.KickCamera(xeno.Owner, new System.Numerics.Vector2(0, 2));

        var hitEntities = new List<EntityUid>();
        foreach (var enemy in _lookup.GetEntitiesInRange(xeno.Owner.ToCoordinates(), xeno.Comp.BreakShrapnelRange))
        {
            if (enemy == xeno.Owner)
                continue;

            if (!HasComp<MobStateComponent>(enemy))
                continue;

            if (!_xeno.CanAbilityAttackTarget(xeno.Owner, enemy))
                continue;

            hitEntities.Add(enemy);

            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = xeno.Comp.BreakShrapnelDamage;
            _damageable.TryChangeDamage(enemy, damage, origin: xeno.Owner, tool: xeno.Owner);

            _stun.TryParalyze(enemy, xeno.Comp.BreakEnemySlowTime, true);
            _slow.TrySlowdown(enemy, xeno.Comp.BreakEnemySlowTime, true);
        }

        if (hitEntities.Count > 0)
        {
            var filter = Filter.Pvs(xeno.Owner, entityManager: EntityManager);
            _colorFlash.RaiseEffect(Color.Red, hitEntities, filter);
        }

        var burst = EnsureComp<STCrusherShieldBreakBurstComponent>(xeno.Owner);
        burst.EndsAt = _timing.CurTime + xeno.Comp.BreakSelfSpeedBurstTime;
        Dirty(xeno.Owner, burst);
        _movementSpeed.RefreshMovementSpeedModifiers(xeno.Owner);
    }

    private void OnGetArmor(Entity<STCrusherShieldComponent> xeno, ref CMGetArmorEvent args)
    {
        if (xeno.Comp.State != STCrusherShieldState.Broken)
            return;

        args.XenoArmor -= xeno.Comp.BreakArmorPenalty;
    }

    private void UpdateBarAlert(EntityUid uid, FixedPoint2 value, FixedPoint2 max)
    {
        if (max <= FixedPoint2.Zero)
            return;

        var maxSeverity = _alerts.GetMaxSeverity(BarAlert);
        var level = ContentHelpers.RoundToLevels(value.Float(), max.Float(), maxSeverity + 1);
        var severity = (short) (maxSeverity - level);
        var message = $"{(int) value.Float()} / {(int) max.Float()}";
        _alerts.ShowAlert(uid, BarAlert, severity, dynamicMessage: message);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var shieldQuery = EntityQueryEnumerator<STCrusherShieldComponent>();
        while (shieldQuery.MoveNext(out var uid, out var shield))
        {
            if (shield.State == STCrusherShieldState.Broken && time >= shield.LockoutEndsAt)
            {
                shield.State = STCrusherShieldState.Off;
                Dirty(uid, shield);
                _armor.UpdateArmorValue(uid);
            }

            var hasPool = TryComp<XenoShieldComponent>(uid, out var syncPool);

            var offPoolValue = shield.SavedPool < FixedPoint2.Zero ? shield.MaxPool : shield.SavedPool;
            var barValue = shield.State switch
            {
                STCrusherShieldState.Active when hasPool => syncPool!.ShieldAmount,
                STCrusherShieldState.Broken => FixedPoint2.Zero,
                _ => offPoolValue,
            };
            UpdateBarAlert(uid, barValue, shield.MaxPool);

            if (shield.State == STCrusherShieldState.Active && hasPool && shield.MaxPool > FixedPoint2.Zero)
            {
                var fraction = Math.Clamp((syncPool!.ShieldAmount / shield.MaxPool).Float(), 0f, 1f);
                _aura.GiveAura(uid, Color.InterpolateBetween(Color.Red, Color.White, fraction), null);
            }

            if (shield.State == STCrusherShieldState.Off)
            {
                if (time >= shield.LastDamageAt + shield.RegenDelay &&
                    shield.SavedPool >= FixedPoint2.Zero &&
                    shield.SavedPool < shield.MaxPool)
                {
                    shield.SavedPool = FixedPoint2.Min(shield.MaxPool, shield.SavedPool + shield.RegenPerSecond * frameTime);
                    Dirty(uid, shield);
                }

                continue;
            }

            if (shield.State != STCrusherShieldState.Active)
                continue;

            if (time < shield.LastDamageAt + shield.RegenDelay)
                continue;

            if (!TryComp<XenoShieldComponent>(uid, out var pool) || !pool.Active)
                continue;

            if (pool.ShieldAmount >= shield.MaxPool)
                continue;

            _xenoShield.ApplyShield(uid, XenoShieldSystem.ShieldType.Crusher, shield.RegenPerSecond * frameTime, addShield: true, maxShield: shield.MaxPool.Double());
        }

        var burstQuery = EntityQueryEnumerator<STCrusherShieldBreakBurstComponent>();
        while (burstQuery.MoveNext(out var uid, out var burst))
        {
            if (time < burst.EndsAt)
                continue;

            RemCompDeferred<STCrusherShieldBreakBurstComponent>(uid);
        }
    }
}
