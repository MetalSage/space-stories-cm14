using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Player;

namespace Content.Shared._Stories.AntiGrief.Cadet;

public sealed class CadetSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ISharedPlaytimeManager _playtimeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CadetComponent, AttackAttemptEvent>(OnAttemptAttackStructure);
        SubscribeLocalEvent<CadetComponent, ShotAttemptedEvent>(OnAttemptShootEvent);
        SubscribeLocalEvent<CadetComponent, XenoSecreteStructureAttemptEvent>(OnAttemptXenoSecreteStructure);

        SubscribeLocalEvent<MarineComponent, PlayerAttachedEvent>(OnPlayerMarineAttached);
        SubscribeLocalEvent<CadetComponent, PlayerDetachedEvent>(OnPlayerCadetDetached);
    }

    private void OnAttemptShootEvent(Entity<CadetComponent> cadet, ref ShotAttemptedEvent args)
    {
        var gun = args.Used;

        if (HasComp<CadetRestrictedGunComponent>(gun))
            args.Cancel();

        if (!TryComp<BallisticAmmoProviderComponent>(gun, out var ammoProvider) || 
            ammoProvider.Entities.Count == 0)
        {
            return;
        }

        foreach (var ent in ammoProvider.Entities)
        {
            if (_tag.HasTag(ent, cadet.Comp.GrenadeTag))
                args.Cancel();
        }

        _popup.PopupClient(Loc.GetString("stories-cadet-cant-use"), cadet.Owner);
    }

    private void OnAttemptXenoSecreteStructure(Entity<CadetComponent> cadet, ref XenoSecreteStructureAttemptEvent args)
    {
        _popup.PopupClient(Loc.GetString("stories-cadet-cant-build"), cadet.Owner);
        args.Cancelled = true;
    }

    private void OnAttemptAttackStructure(Entity<CadetComponent> cadet, ref AttackAttemptEvent args)
    {
        if (HasComp<XenoConstructComponent>(args.Target) || 
            HasComp<HiveConstructionNodeComponent>(args.Target) && 
            HasComp<XenoComponent>(cadet))
        {
            _popup.PopupClient(Loc.GetString("stories-cadet-cant-destroy"), cadet.Owner);
            args.Cancel();
        }

        if (args.Target != cadet &&
            HasComp<MarineComponent>(args.Target) &&
            HasComp<MarineComponent>(cadet))
        {
            _popup.PopupClient(Loc.GetString("stories-cadet-cant-attack-allies"), cadet.Owner);
            args.Cancel();
        }
    }

    private void OnPlayerMarineAttached(Entity<MarineComponent> marine, ref PlayerAttachedEvent args)
    {
        var playtime = _playtimeManager.GetPlayTimes(args.Player);

        if (!playtime.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out TimeSpan time) ||
            time < TimeSpan.FromHours(10))
        {
            EnsureComp<CadetComponent>(marine.Owner);
        }
    }

    private void OnPlayerCadetDetached(Entity<CadetComponent> cadet, ref PlayerDetachedEvent args)
    {
        if (TerminatingOrDeleted(cadet))
            return;

        RemCompDeferred<CadetComponent>(cadet);
    }
}
