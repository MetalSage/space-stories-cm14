using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.BoostOnHit;

public sealed class RMCXenoBoostOnHitSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCXenoBoostOnHitComponent, DamageModifyAfterResistEvent>(OnTakeDamage);
        SubscribeLocalEvent<RMCXenoBoostOnHitComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RMCXenoBoostOnHitComponent>();
        while (query.MoveNext(out var uid, out var boostOnHitComponent))
        {
            if (boostOnHitComponent.Active && boostOnHitComponent.DurationTime <= _timing.CurTime)
                TryUnboost((uid, boostOnHitComponent));

            if (boostOnHitComponent.Cooldown && boostOnHitComponent.CooldownTime <= _timing.CurTime)
                TryRecharge((uid, boostOnHitComponent));
        }
    }

    private void OnTakeDamage(Entity<RMCXenoBoostOnHitComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (args.Damage.GetTotal() <= 0)
            return;

        TryBoost((ent.Owner, ent.Comp));
    }

    private void OnRefreshSpeed(Entity<RMCXenoBoostOnHitComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Active)
            return;

        args.ModifySpeed(ent.Comp.Amount);
    }

    public void TryBoost(Entity<RMCXenoBoostOnHitComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.Active || ent.Comp.Cooldown)
            return;

        ent.Comp.Active = true;
        ent.Comp.DurationTime = _timing.CurTime + ent.Comp.Duration;
        Dirty(ent);

        _aura.GiveAura(ent, ent.Comp.AuraColor, ent.Comp.Duration);
        _popup.PopupPredicted(Loc.GetString(ent.Comp.BoostMessage), ent, null, PopupType.MediumCaution);

        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent);
    }

    // ReSharper disable once IdentifierTypo
    public void TryUnboost(Entity<RMCXenoBoostOnHitComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!ent.Comp.Active)
            return;

        ent.Comp.Active = false;
        ent.Comp.Cooldown = true;
        ent.Comp.CooldownTime = _timing.CurTime + ent.Comp.Duration + ent.Comp.CooldownDuration;
        Dirty(ent);

        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent);
    }

    public void TryRecharge(Entity<RMCXenoBoostOnHitComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!ent.Comp.Cooldown)
            return;

        ent.Comp.Cooldown = false;
        Dirty(ent);

        _popup.PopupPredicted(Loc.GetString(ent.Comp.RechargeMessage), ent, null, PopupType.Medium);
    }
}
