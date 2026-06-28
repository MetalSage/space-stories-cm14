using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Interaction.Events;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.ReflectiveShield;

public sealed class ReflectiveShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReflectiveShieldComponent, ReflectiveShieldActionEvent>(OnReflectiveShieldAction);
        SubscribeLocalEvent<ReflectiveShieldComponent, ChangeDirectionAttemptEvent>(OnChangeDirectionAttempt);
        SubscribeLocalEvent<ReflectiveShieldComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ReflectiveShieldComponent, ToggleCombatActionEvent>(OnCombatModeToggle);
        SubscribeLocalEvent<ReflectiveShieldComponent, ComponentShutdown>(OnReflectiveShieldShutdown);
    }

    private void OnReflectiveShieldShutdown(Entity<ReflectiveShieldComponent> xeno, ref ComponentShutdown args)
    {
        if (!xeno.Comp.Active)
            return;

        RemComp<RMCReflectiveComponent>(xeno);
        RemComp<AuraComponent>(xeno);
        RemComp<MouseRotatorComponent>(xeno);
        RemComp<NoRotateOnMoveComponent>(xeno);

        xeno.Comp.Active = false;
        xeno.Comp.DeactivateAt = null;
        xeno.Comp.ActivatedAt = null;
        xeno.Comp.PendingCooldown = null;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ReflectiveShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active && comp.PendingCooldown != null)
            {
                var pending = comp.PendingCooldown.Value;
                comp.PendingCooldown = null;
                Dirty(uid, comp);
                foreach (var action in _rmcActions.GetActionsWithEvent<ReflectiveShieldActionEvent>(uid))
                {
                    _actions.SetCooldown(action.Owner, _timing.CurTime, _timing.CurTime + pending);
                }
                continue;
            }

            if (!comp.Active || comp.DeactivateAt == null)
                continue;

            if (_timing.CurTime < comp.DeactivateAt)
                continue;

            comp.DeactivateAt = null;
            Deactivate((uid, comp));
        }
    }

    private void OnChangeDirectionAttempt(Entity<ReflectiveShieldComponent> xeno, ref ChangeDirectionAttemptEvent args)
    {
        if (xeno.Comp.Active)
            args.Cancel();
    }

    private void OnAttackAttempt(Entity<ReflectiveShieldComponent> xeno, ref AttackAttemptEvent args)
    {
        if (xeno.Comp.Active)
            args.Cancel();
    }

    private void OnCombatModeToggle(Entity<ReflectiveShieldComponent> xeno, ref ToggleCombatActionEvent args)
    {
        if (!xeno.Comp.Active)
            return;

        EnsureComp<MouseRotatorComponent>(xeno);
        EnsureComp<NoRotateOnMoveComponent>(xeno);
    }

    private void OnReflectiveShieldAction(Entity<ReflectiveShieldComponent> xeno, ref ReflectiveShieldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EncasedPlates.EncasedPlatesComponent>(xeno, out var encased) || !encased.Active)
        {
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-reflective-shield-need-plates"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (xeno.Comp.Active)
        {
            args.Handled = true;
            Deactivate(xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        Activate(xeno);
    }

    private void Activate(Entity<ReflectiveShieldComponent> xeno)
    {
        xeno.Comp.Active = true;
        xeno.Comp.DeactivateAt = _timing.CurTime + xeno.Comp.Duration;
        xeno.Comp.ActivatedAt = _timing.CurTime;
        Dirty(xeno);

        var reflect = EnsureComp<RMCReflectiveComponent>(xeno);
        reflect.Angle = xeno.Comp.ReflectAngle;
        reflect.Chance = xeno.Comp.ReflectChance;
        reflect.ReflectionMultiplier = xeno.Comp.ReflectionMultiplier;
        Dirty(xeno.Owner, reflect);

        _rmcPulling.TryStopAllPullsFromAndOn(xeno);
        _appearance.SetData(xeno, XenoVisualLayers.ReflectiveShield, true);
        _aura.GiveAura(xeno, new Color(0f, 1f, 1f), null);
        _popup.PopupClient(Loc.GetString("st-xeno-bulwark-reflective-shield-activate"), xeno, xeno, PopupType.Medium);

        EnsureComp<MouseRotatorComponent>(xeno);
        EnsureComp<NoRotateOnMoveComponent>(xeno);

        foreach (var action in _rmcActions.GetActionsWithEvent<ReflectiveShieldActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), true);
        }
    }

    private void Deactivate(Entity<ReflectiveShieldComponent> xeno)
    {
        TimeSpan cooldown;
        if (xeno.Comp.ActivatedAt != null)
        {
            var elapsed = _timing.CurTime - xeno.Comp.ActivatedAt.Value;
            var seconds = Math.Max(
                xeno.Comp.MinCooldown.TotalSeconds,
                elapsed.TotalSeconds * xeno.Comp.CooldownPerSecond);
            cooldown = TimeSpan.FromSeconds(seconds);
        }
        else
        {
            cooldown = xeno.Comp.FullCooldown;
        }

        xeno.Comp.Active = false;
        xeno.Comp.DeactivateAt = null;
        xeno.Comp.ActivatedAt = null;
        xeno.Comp.PendingCooldown = cooldown;
        Dirty(xeno);

        RemComp<RMCReflectiveComponent>(xeno);
        _appearance.SetData(xeno, XenoVisualLayers.ReflectiveShield, false);
        RemComp<AuraComponent>(xeno);
        _popup.PopupClient(Loc.GetString("st-xeno-bulwark-reflective-shield-deactivate"), xeno, xeno, PopupType.Small);

        RemComp<MouseRotatorComponent>(xeno);
        RemComp<NoRotateOnMoveComponent>(xeno);

        foreach (var action in _rmcActions.GetActionsWithEvent<ReflectiveShieldActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), false);
        }
    }
}
