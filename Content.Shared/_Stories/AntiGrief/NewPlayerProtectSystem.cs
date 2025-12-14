using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Interaction.Events;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Alert;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Shared._Stories.AntiGrief.NewPlayerProtect;

public sealed class NewPlayerProtectSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ISharedPlaytimeManager _playtimeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private float _newPlayerProtectTime = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NewPlayerProtectComponent, AttackAttemptEvent>(OnAttemptAttackStructure);
        SubscribeLocalEvent<NewPlayerProtectComponent, ShotAttemptedEvent>(OnAttemptShootEvent);
        SubscribeLocalEvent<MarineComponent, PlayerAttachedEvent>(OnPlayerMarineAttached);
        SubscribeLocalEvent<NewPlayerProtectComponent, PlayerDetachedEvent>(OnNewPlayerDetached);

        Subs.CVar(_cfg, SCCVars.SCCVars.NewPlayersProtectTime, v => _newPlayerProtectTime = v, true);
    }

    private void OnAttemptShootEvent(Entity<NewPlayerProtectComponent> ent, ref ShotAttemptedEvent args)
    {
        var gun = args.Used;
        if (HasComp<NewPlayerRestrictedGunComponent>(gun))
            args.Cancel();

        if (!TryComp<BallisticAmmoProviderComponent>(gun, out var ammoProvider) ||
            ammoProvider.Entities.Count == 0)
            return;

        foreach (var ammo in ammoProvider.Entities)
        {
            if (_tag.HasTag(ammo, ent.Comp.GrenadeTag))
                args.Cancel();
        }

        _popup.PopupClient(Loc.GetString("stories-cant-use", ("time", _newPlayerProtectTime)), ent.Owner);
    }

    private void OnAttemptAttackStructure(Entity<NewPlayerProtectComponent> ent, ref AttackAttemptEvent args)
    {
        if (HasComp<XenoStructureMapTrackedComponent>(args.Target) ||
            HasComp<HiveConstructionNodeComponent>(args.Target) &&
            HasComp<XenoComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("stories-cant-destroy", ("time", _newPlayerProtectTime)), ent.Owner);
            args.Cancel();
            return;
        }

        if (args.Target != ent &&
            HasComp<MarineComponent>(args.Target) &&
            HasComp<MarineComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("stories-cant-attack-allies", ("time", _newPlayerProtectTime)), ent.Owner);
            args.Cancel();
        }
    }

    private void OnPlayerMarineAttached(Entity<MarineComponent> marine, ref PlayerAttachedEvent args)
    {
        var playtime = _playtimeManager.GetPlayTimes(args.Player);

        if (!playtime.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var time) ||
            time < TimeSpan.FromHours(_newPlayerProtectTime))
        {
            var newPlayerComp = EnsureComp<NewPlayerProtectComponent>(marine.Owner);
            newPlayerComp.Hours = _newPlayerProtectTime;
            _alerts.ShowAlert(marine.Owner, newPlayerComp.AlertProto);
        }
    }

    private void OnNewPlayerDetached(Entity<NewPlayerProtectComponent> ent, ref PlayerDetachedEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _alerts.ClearAlert(ent.Owner, ent.Comp.AlertProto);
        RemCompDeferred<NewPlayerProtectComponent>(ent);
    }
}
