using Content.Shared._RMC14.ARES.ExternalTerminals;
using Content.Shared._RMC14.Sentry;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.ARES.Lockdown;

public sealed class STARESCoreLockdownSystem : EntitySystem
{
    [Dependency] private readonly ARESCoreSystem _aresCore = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SentrySystem _sentry = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ARESExternalTerminalComponent>(ARESExternalTerminalUIKey.Key,
            subs => subs.Event<RMCARESExternalToggleLockdown>(OnToggleLockdown));

        SubscribeLocalEvent<STARESLockdownDoorComponent, STARESCoreLockdownChangedEvent>(OnDoorLockdownChanged);
        SubscribeLocalEvent<STARESLockdownTurretComponent, STARESCoreLockdownChangedEvent>(OnTurretLockdownChanged);
    }

    private void OnToggleLockdown(Entity<ARESExternalTerminalComponent> ent, ref RMCARESExternalToggleLockdown args)
    {
        if (!ent.Comp.LoggedIn || !ent.Comp.ShowCore)
            return;

        if (!_aresCore.TryGetARES(ent.Comp.Faction, out var ares) || ares is not { } core)
            return;

        var time = _timing.CurTime;
        if (time < core.Comp.NextLockdown)
        {
            _popup.PopupClient(Loc.GetString("st-ares-core-lockdown-cooldown"), ent.Owner, args.Actor, PopupType.SmallCaution);
            return;
        }

        core.Comp.NextLockdown = time + core.Comp.LockdownCooldown;
        core.Comp.LockdownActive = !core.Comp.LockdownActive;
        Dirty(core);

        var ev = new STARESCoreLockdownChangedEvent(core.Owner, core.Comp.LockdownActive);
        RaiseLocalEvent(ref ev);

        var msg = core.Comp.LockdownActive
            ? Loc.GetString("st-ares-core-lockdown-activated")
            : Loc.GetString("st-ares-core-lockdown-lifted");
        _popup.PopupClient(msg, ent.Owner, args.Actor);
    }

    private void OnDoorLockdownChanged(Entity<STARESLockdownDoorComponent> ent, ref STARESCoreLockdownChangedEvent args)
    {
        if (!SameMap(ent.Owner, args.Core))
            return;

        if (!TryComp<DoorComponent>(ent, out var door))
            return;

        if (args.Active)
            _door.TryClose(ent, door);
        else
            _door.TryOpen(ent, door);
    }

    private void OnTurretLockdownChanged(Entity<STARESLockdownTurretComponent> ent, ref STARESCoreLockdownChangedEvent args)
    {
        if (!SameMap(ent.Owner, args.Core))
            return;

        if (!TryComp<SentryComponent>(ent, out var sentry))
            return;

        _sentry.TrySetMode((ent.Owner, sentry), args.Active ? SentryMode.On : SentryMode.Off, remote: true);
    }

    private bool SameMap(EntityUid a, EntityUid b)
    {
        return _transform.GetMapId(a) == _transform.GetMapId(b);
    }
}
