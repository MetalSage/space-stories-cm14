using System.Linq;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Burrow;
using Content.Shared._RMC14.Xenonids.Crest;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Fortify;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Invisibility;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared._Stories.Xenonids.Evolution;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random; // Stories-EvoQueue
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Evolution;

public sealed class XenoEvolutionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!; // Stories-EvoQueue
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly HiveBoonSystem _xenoBoon = default!;
    [Dependency] private readonly SharedXenoHiveSystem _xenoHive = default!;
    [Dependency] private readonly SharedXenoWeedsSystem _xenoWeeds = default!;

    private TimeSpan _evolutionPointsRequireOvipositorAfter;
    private TimeSpan _evolutionAccumulatePointsBefore;
    private TimeSpan _evolveSameCasteCooldown;
    private TimeSpan _earlyEvoBoostBefore;

    // Stories-EvoQueue-Start
    private bool _evolutionQueueEnabled;
    private TimeSpan _evolutionQueueGrace;
    private TimeSpan _nextQueueUpdate;
    // Stories-EvoQueue-End

    private readonly HashSet<EntityUid> _climbable = new();
    private readonly HashSet<EntityUid> _doors = new();
    private readonly HashSet<EntityUid> _intersecting = new();

    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        _mobStateQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<MarinesLandedChangedEvent>(OnMarinesLandedChanged);
        SubscribeLocalEvent<HiveComponent, XenoHiveQueenChangedEvent>(OnHiveQueenChanged);

        SubscribeLocalEvent<XenoDevolveComponent, XenoOpenDevolveActionEvent>(OnXenoOpenDevolveAction);

        SubscribeLocalEvent<XenoEvolutionComponent, MapInitEvent>(OnXenoEvolveMapInit);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoOpenEvolutionsActionEvent>(OnXenoEvolveAction);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoEvolutionDoAfterEvent>(OnXenoEvolveDoAfter);
        SubscribeLocalEvent<XenoEvolutionComponent, NewXenoEvolvedEvent>(OnXenoEvolutionNewEvolved);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoDevolvedEvent>(OnXenoEvolutionDevolved);

        SubscribeLocalEvent<XenoNewlyEvolvedComponent, PreventCollideEvent>(OnNewlyEvolvedPreventCollide);

        SubscribeLocalEvent<XenoEvolutionGranterComponent, NewXenoEvolvedEvent>(OnGranterEvolved);

        SubscribeLocalEvent<XenoOvipositorChangedEvent>(OnOvipositorChanged);
        SubscribeLocalEvent<XenoComponent, XenoTransmuteActionEvent>(OnXenoTransmuteAction);
        SubscribeLocalEvent<XenoComponent, XenoTransmuteChosenEvent>(OnXenoTransmuteChosen);

        Subs.BuiEvents<XenoEvolutionComponent>(XenoEvolutionUIKey.Key,
            subs =>
            {
                subs.Event<XenoEvolveBuiMsg>(OnXenoEvolveBui);
                subs.Event<XenoStrainBuiMsg>(OnXenoStrainBui);
                subs.Event<XenoEvolutionQueueCancelBuiMsg>(OnXenoEvolutionQueueCancelBui); // Stories-EvoQueue
            });

        Subs.BuiEvents<XenoDevolveComponent>(XenoDevolveUIKey.Key,
            subs =>
            {
                subs.Event<XenoDevolveBuiMsg>(OnXenoDevolveBui);
            });

        Subs.CVar(_config, RMCCVars.RMCEvolutionPointsRequireOvipositorMinutes, v => _evolutionPointsRequireOvipositorAfter = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCEvolutionPointsAccumulateBeforeMinutes, v => _evolutionAccumulatePointsBefore = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCXenoEvolveSameCasteCooldownSeconds, v => _evolveSameCasteCooldown = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCXenoEarlyEvoPointBoostBeforeMinutes, v => _earlyEvoBoostBefore = TimeSpan.FromMinutes(v), true);
        // Stories-EvoQueue-Start
        Subs.CVar(_config, RMCCVars.RMCXenoEvolutionQueueEnabled, v => _evolutionQueueEnabled = v, true);
        Subs.CVar(_config, RMCCVars.RMCXenoEvolutionQueueGraceSeconds, v => _evolutionQueueGrace = TimeSpan.FromSeconds(v), true);
        // Stories-EvoQueue-End
    }

    private void OnXenoOpenDevolveAction(Entity<XenoDevolveComponent> xeno, ref XenoOpenDevolveActionEvent args)
    {
        if (args.Handled)
            return;

        if (!DamagedCheckPopup(xeno))
            return;

        args.Handled = true;
        _ui.OpenUi(xeno.Owner, XenoDevolveUIKey.Key, xeno);
    }

    private void OnXenoEvolveMapInit(Entity<XenoEvolutionComponent> ent, ref MapInitEvent args)
    {
        _action.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);

        if (_net.IsClient)
            return;

        ent.Comp.MarinesLanded = MarinesHaveLanded();

        // Stories-EvoQueue
        EnsureComp<XenoEvolutionQueueComponent>(ent).TierEnteredAt = _gameTicker.RoundDuration();
    }

    private void OnXenoEvolveAction(Entity<XenoEvolutionComponent> xeno, ref XenoOpenEvolutionsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.OpenUi(xeno.Owner, XenoEvolutionUIKey.Key, xeno);
        _ui.SetUiState(xeno.Owner, XenoEvolutionUIKey.Key, BuildEvolveState(xeno.Owner)); // Stories-EvoQueue
    }

    private void OnXenoEvolveBui(Entity<XenoEvolutionComponent> xeno, ref XenoEvolveBuiMsg args)
    {
        var actor = args.Actor;
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, actor);

        if (_net.IsClient)
            return;

        // Stories-EvoQueue
        if (!HasValidQueueOffer(xeno, args.Choice))
        {
            _popup.PopupEntity(Loc.GetString("stories-xeno-queue-no-offer"), xeno, xeno, PopupType.MediumCaution);
            return;
        }

        if (!CanEvolvePopup(xeno, args.Choice))
        {
            Log.Warning($"{ToPrettyString(actor)} sent an invalid evolution choice: {args.Choice}.");
            return;
        }

        if (!DamagedCheckPopup(xeno, false))
            return;

        var time = _timing.CurTime;
        if (_prototypes.TryIndex(args.Choice, out var choice) &&
            choice.HasComponent<XenoEvolutionGranterComponent>(_compFactory) &&
            _xenoHive.GetHive(xeno.Owner) is { } hive &&
            hive.Comp.LastQueenDeath is { } lastQueenDeath &&
            time < lastQueenDeath + hive.Comp.NewQueenCooldown)
        {
            var left = lastQueenDeath + hive.Comp.NewQueenCooldown - time;
            var msg = Loc.GetString("rmc-xeno-evolution-cant-evolve-recent-queen-death-minutes",
                ("minutes", left.Minutes),
                ("seconds", left.Seconds));
            if (left.Minutes == 0)
            {
                msg = Loc.GetString("rmc-xeno-evolution-cant-evolve-recent-queen-death-seconds",
                    ("seconds", left.Seconds));
            }

            _popup.PopupEntity(msg, xeno, xeno, PopupType.MediumCaution);
            return;
        }

        var ev = new XenoEvolutionDoAfterEvent(args.Choice);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.EvolutionDelay, ev, xeno)
        {
            BreakOnRest = false,
        };

        if (xeno.Comp.EvolutionDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("cm-xeno-evolution-start"), xeno, xeno);

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            // Stories-EvoQueue
            if (_evolutionQueueEnabled &&
                TryComp(xeno, out XenoEvolutionQueueComponent? offerQueue) &&
                offerQueue.OfferedUntil != null)
            {
                offerQueue.EvolvingUntil = _gameTicker.RoundDuration() + xeno.Comp.EvolutionDelay + TimeSpan.FromSeconds(1);
                offerQueue.OfferedUntil = null;
                Dirty(xeno.Owner, offerQueue);
            }

            _jitter.DoJitter(xeno, xeno.Comp.EvolutionDelay, true, 80, 8, true);

            var popupOthers = Loc.GetString("rmc-xeno-evolution-start-others", ("xeno", xeno));
            _popup.PopupEntity(popupOthers, xeno, Filter.PvsExcept(xeno), true, PopupType.Medium);

            var popupSelf = Loc.GetString("rmc-xeno-evolution-start-self");
            _popup.PopupEntity(popupSelf, xeno, xeno, PopupType.Medium);
        }
    }

    private void OnXenoStrainBui(Entity<XenoEvolutionComponent> xeno, ref XenoStrainBuiMsg args)
    {
        var actor = args.Actor;
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, actor);

        if (_net.IsClient)
            return;

        if (!xeno.Comp.Strains.Contains(args.Choice))
        {
            Log.Warning($"{ToPrettyString(actor)} sent an invalid strain choice: {args.Choice}.");
            return;
        }

        if (!ContainedCheckPopup(xeno))
            return;

        if (!DamagedCheckPopup(xeno, false))
            return;

        var newXeno = TransferXeno(xeno, args.Choice);
        var ev = new NewXenoEvolvedEvent(xeno, newXeno, false);
        RaiseLocalEvent(newXeno, ref ev, true);

        _adminLog.Add(LogType.RMCEvolve, $"Xenonid {ToPrettyString(xeno)} chose strain {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);
    }

    // Stories-EvoQueue-Start
    private void OnXenoEvolutionQueueCancelBui(Entity<XenoEvolutionComponent> xeno, ref XenoEvolutionQueueCancelBuiMsg args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(xeno, out XenoEvolutionQueueComponent? queue) || queue.OfferedUntil == null)
            return;

        DeclineOffer((xeno.Owner, queue), Loc.GetString("stories-xeno-queue-declined"));
        _ui.SetUiState(xeno.Owner, XenoEvolutionUIKey.Key, BuildEvolveState(xeno.Owner));

        if (_evolutionQueueEnabled)
            UpdateEvolutionQueue(_gameTicker.RoundDuration());
    }

    // Drops the offer, resets priority (back of queue) and sits the xeno out so the slot passes on.
    private void DeclineOffer(Entity<XenoEvolutionQueueComponent> xeno, string? popup)
    {
        var now = _gameTicker.RoundDuration();
        xeno.Comp.OfferedUntil = null;
        xeno.Comp.OfferedTier = 0;
        xeno.Comp.TierEnteredAt = now;
        xeno.Comp.PassedUntil = now + _evolutionQueueGrace;
        Dirty(xeno);

        if (popup != null)
            _popup.PopupEntity(popup, xeno, xeno, PopupType.MediumCaution);
    }

    private bool HasValidQueueOffer(Entity<XenoEvolutionComponent> xeno, EntProtoId choice)
    {
        if (!_evolutionQueueEnabled)
            return true;

        if (_xenoHive.GetHive(xeno.Owner) is not { } hive ||
            !_xenoHive.IsQueuedCaste(hive, choice))
        {
            return true;
        }

        if (!_prototypes.TryIndex(choice, out var proto) ||
            !proto.TryGetComponent(out XenoComponent? xenoComp, _compFactory))
        {
            return false;
        }

        var roundDuration = _gameTicker.RoundDuration();
        if (xenoComp.UnlockAt > roundDuration)
            return false;

        return TryComp(xeno, out XenoEvolutionQueueComponent? queue) &&
               queue.OfferedTier == xenoComp.Tier &&
               queue.OfferedUntil is { } until &&
               until > roundDuration;
    }

    private void ReleaseEvolvingReservation(EntityUid xeno)
    {
        if (!TryComp(xeno, out XenoEvolutionQueueComponent? queue) || queue.EvolvingUntil == null)
            return;

        queue.EvolvingUntil = null;
        queue.OfferedTier = 0;
        Dirty(xeno, queue);
    }
    // Stories-EvoQueue-End

    private void OnXenoDevolveBui(Entity<XenoDevolveComponent> xeno, ref XenoDevolveBuiMsg args)
    {
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, xeno);
        TryDevolve(xeno, args.Choice);
    }

    private void OnXenoEvolveDoAfter(Entity<XenoEvolutionComponent> xeno, ref XenoEvolutionDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Handled ||
            args.Cancelled ||
            !_mind.TryGetMind(xeno, out _, out _) ||
            !CanEvolvePopup(xeno, args.Choice))
        {
            // Stories-EvoQueue
            ReleaseEvolvingReservation(xeno.Owner);
            return;
        }

        args.Handled = true;

        var newXeno = TransferXeno(xeno, args.Choice);
        var ev = new NewXenoEvolvedEvent(xeno, newXeno, true);
        RaiseLocalEvent(newXeno, ref ev, true);

        _adminLog.Add(LogType.RMCEvolve, $"Xenonid {ToPrettyString(xeno)} evolved into {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-end"), newXeno, newXeno);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);
    }

    private void OnXenoEvolutionNewEvolved(Entity<XenoEvolutionComponent> xeno, ref NewXenoEvolvedEvent args)
    {
        TransferPoints((args.OldXeno, args.OldXeno), xeno, args.SubtractPoints);
        _jitter.DoJitter(xeno, xeno.Comp.EvolutionJitterDuration, true, 80, 8, true);
    }

    private void OnXenoEvolutionDevolved(Entity<XenoEvolutionComponent> xeno, ref XenoDevolvedEvent args)
    {
        TransferPoints(args.OldXeno, (xeno, xeno), false);
    }

    private void TransferPoints(Entity<XenoEvolutionComponent?> old, Entity<XenoEvolutionComponent> xeno, bool subtract)
    {
        if (!Resolve(old, ref old.Comp, false))
            return;

        xeno.Comp.Points = subtract ? FixedPoint2.Max(0, old.Comp.Points - old.Comp.Max) : old.Comp.Points;

        Dirty(xeno);
    }

    private void OnNewlyEvolvedPreventCollide(Entity<XenoNewlyEvolvedComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.StopCollide.Contains(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnGranterEvolved(Entity<XenoEvolutionGranterComponent> ent, ref NewXenoEvolvedEvent args)
    {
        _xenoAnnounce.AnnounceSameHive(ent.Owner, Loc.GetString("rmc-new-queen"));
    }

    private void OnOvipositorChanged(ref XenoOvipositorChangedEvent ev)
    {
        if (_net.IsClient)
            return;

        var xenos = EntityQueryEnumerator<ActorComponent, XenoEvolutionComponent>();
        while (xenos.MoveNext(out var uid, out _, out _))
        {
            var buiState = new XenoEvolveBuiState(LackingOvipositor(uid));
            _ui.SetUiState(uid, XenoEvolutionUIKey.Key, buiState);
        }
    }

    private void OnMarinesLandedChanged(ref MarinesLandedChangedEvent ev)
    {
        if (_net.IsClient)
            return;

        var xenos = EntityQueryEnumerator<XenoEvolutionComponent>();
        while (xenos.MoveNext(out var uid, out var comp))
        {
            if (comp.MarinesLanded != ev.Landed)
            {
                comp.MarinesLanded = ev.Landed;
                Dirty(uid, comp);
            }

            if (HasComp<ActorComponent>(uid))
            {
                var buiState = new XenoEvolveBuiState(LackingOvipositor(uid));
                _ui.SetUiState(uid, XenoEvolutionUIKey.Key, buiState);
            }
        }
    }

    private void OnHiveQueenChanged(Entity<HiveComponent> ent, ref XenoHiveQueenChangedEvent ev)
    {
        if (_net.IsClient)
            return;

        var xenos = EntityQueryEnumerator<ActorComponent, XenoEvolutionComponent, HiveMemberComponent>();
        while (xenos.MoveNext(out var uid, out _, out _, out var member))
        {
            if (member.Hive == ent.Owner)
            {
                var buiState = new XenoEvolveBuiState(LackingOvipositor(uid));
                _ui.SetUiState(uid, XenoEvolutionUIKey.Key, buiState);
            }
        }
    }

    private void OnXenoTransmuteAction(Entity<XenoComponent> xeno, ref XenoTransmuteActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanTransmutePopup(xeno))
            return;

        var current = Prototype(xeno.Owner)?.ID;
        var choices = new List<DialogOption>();
        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.ID == current ||
                !IsBaseTransmuteCaste(prototype) ||
                !prototype.TryGetComponent(out XenoComponent? xenoComp, _compFactory) ||
                xenoComp.Tier != xeno.Comp.Tier)
            {
                continue;
            }

            choices.Add(new DialogOption(prototype.Name, new XenoTransmuteChosenEvent(prototype.ID)));
        }

        choices.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.InvariantCultureIgnoreCase));
        _dialog.OpenOptions(xeno.Owner,
            Loc.GetString("rmc-xeno-transmute-title"),
            choices,
            Loc.GetString("rmc-xeno-transmute-prompt"));
    }

    private void OnXenoTransmuteChosen(Entity<XenoComponent> xeno, ref XenoTransmuteChosenEvent args)
    {
        Transmute(xeno, args.Choice);
    }

    private bool ContainedCheckPopup(EntityUid xeno, bool doPopup = true)
    {
        if (!_container.IsEntityInContainer(xeno))
            return true;

        if (doPopup)
            _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-failed-bad-location"), xeno, xeno, PopupType.MediumCaution);

        return false;
    }

    private bool DamagedCheckPopup(EntityUid xeno, bool predicted = true, bool doPopup = true)
    {
        if (!TryComp(xeno, out DamageableComponent? damageable) ||
            damageable.TotalDamage <= 1)
            return true;

        // Stories-EvoQueue-Start
        if (doPopup)
        {
            if (predicted)
                _popup.PopupClient(Loc.GetString("rmc-xeno-evolution-cant-evolve-damaged"), xeno, xeno, PopupType.MediumCaution);
            else
                _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-cant-evolve-damaged"), xeno, xeno, PopupType.MediumCaution);
        }
        // Stories-EvoQueue-End

        return false;
    }

    private bool CanTransmutePopup(Entity<XenoComponent> xeno, bool doPopup = true)
    {
        if (xeno.Comp.Tier is <= 0 or > 3)
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("rmc-xeno-transmute-failed-tier"), xeno, xeno, PopupType.MediumCaution);

            return false;
        }

        if (_mobState.IsDead(xeno.Owner))
            return false;

        if (!ContainedCheckPopup(xeno.Owner, doPopup))
            return false;

        if (!DamagedCheckPopup(xeno.Owner, false, doPopup))
            return false;

        if (TryComp(xeno.Owner, out TransformComponent? xform) &&
            xform.MapID == MapId.Nullspace)
        {
            return false;
        }

        if (IsInTransmuteBlockingStance(xeno.Owner))
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("rmc-xeno-transmute-failed-stance"), xeno, xeno, PopupType.MediumCaution);

            return false;
        }

        return true;
    }

    private bool IsInTransmuteBlockingStance(EntityUid xeno)
    {
        return TryComp(xeno, out XenoFortifyComponent? fortify) && fortify.Fortified ||
               TryComp(xeno, out XenoCrestComponent? crest) && crest.Lowered ||
               TryComp(xeno, out XenoBurrowComponent? burrow) && (burrow.Active || burrow.Tunneling) ||
               HasComp<XenoActiveInvisibleComponent>(xeno);
    }

    public EntityUid? Transmute(Entity<XenoComponent> xeno, EntProtoId to)
    {
        if (_net.IsClient ||
            !CanTransmutePopup(xeno) ||
            !TryComp(xeno.Owner, out XenoEvolutionComponent? evolution))
        {
            return null;
        }

        if (!_prototypes.TryIndex(to, out var prototype) ||
            !IsBaseTransmuteCaste(prototype) ||
            !prototype.TryGetComponent(out XenoComponent? newXenoComp, _compFactory) ||
            newXenoComp.Tier != xeno.Comp.Tier ||
            prototype.ID == Prototype(xeno.Owner)?.ID)
        {
            return null;
        }

        var newXeno = TransferXeno(xeno.Owner, to);
        var ev = new NewXenoEvolvedEvent((xeno.Owner, evolution), newXeno, false);
        RaiseLocalEvent(newXeno, ref ev, true);

        _adminLog.Add(LogType.RMCEvolve, $"Xenonid {ToPrettyString(xeno)} transmuted into {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        _popup.PopupEntity(Loc.GetString("rmc-xeno-transmute-end"), newXeno, newXeno);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);

        return newXeno;
    }

    private bool IsBaseTransmuteCaste(EntityPrototype prototype)
    {
        if (prototype.Abstract ||
            !prototype.TryGetComponent(out XenoBaseCasteComponent? baseCaste, _compFactory) ||
            !baseCaste.Enabled ||
            prototype.HasComponent<XenoStrainComponent>(_compFactory) ||
            prototype.HasComponent<XenoHiddenComponent>(_compFactory))
        {
            return false;
        }

        return true;
    }

    private bool CanEvolvePopup(Entity<XenoEvolutionComponent> xeno, EntProtoId newXeno, bool doPopup = true, bool ignoreSlotLimit = false) // Stories-EvoQueue
    {
        var isEarlyEvo = xeno.Comp.EarlyEvolvesTo.Contains(newXeno);
        if (!xeno.Comp.EvolvesTo.Contains(newXeno) && !xeno.Comp.EvolvesToWithoutPoints.Contains(newXeno) && !isEarlyEvo)
            return false;

        if (isEarlyEvo && xeno.Comp.MarinesLanded)
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-failed-marines-dropped"), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        if (!_prototypes.TryIndex(newXeno, out var prototype))
            return true;

        if (!ContainedCheckPopup(xeno, doPopup))
            return false;

        if (prototype.HasComponent<XenoEvolutionGranterComponent>(_compFactory) && HiveHasLivingQueen(xeno.Owner))
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-failed-queen-exists"), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        // TODO RMC14 revive jelly when added should not bring back dead queens
        if (prototype.TryGetComponent(out XenoEvolutionCappedComponent? capped, _compFactory) &&
            HasLiving<XenoEvolutionCappedComponent>(capped.Max, e => e.Comp.Id == capped.Id))
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-failed-already-have", ("prototype", prototype.Name)), xeno, xeno, PopupType.MediumCaution);

            return false;
        }

        if (!xeno.Comp.CanEvolveWithoutGranter && !HasLiving<XenoEvolutionGranterComponent>(1))
        {
            if (doPopup)
            {
                _popup.PopupEntity(
                    Loc.GetString("cm-xeno-evolution-failed-hive-shaken"),
                    xeno,
                    xeno,
                    PopupType.MediumCaution
                );
            }

            return false;
        }


        if (TryComp<RestrictEvolveOffWeedsComponent>(xeno.Owner, out var comp))
        {
            var coordinates = _transform.GetMoverCoordinates(xeno).SnapToGrid(EntityManager, _map);
            if (_transform.GetGrid(coordinates) is not { } gridUid ||
                !TryComp(gridUid, out MapGridComponent? grid))
            {
                return false;
            }

            if (!_xenoWeeds.IsOnWeeds((gridUid, grid), coordinates) && comp.RestrictTime > _gameTicker.RoundDuration())
            {
                // Stories-EvoQueue-Start
                if (doPopup)
                {
                    _popup.PopupEntity(
                        Loc.GetString("rmc-xeno-evolution-failed-early-weeds"),
                        xeno,
                        xeno,
                        PopupType.MediumCaution
                    );
                }
                // Stories-EvoQueue-End

                return false;
            }
        }

        prototype.TryGetComponent(out XenoComponent? newXenoComp, _compFactory);
        if (newXenoComp != null &&
            newXenoComp.UnlockAt > _gameTicker.RoundDuration())
        {
            if (doPopup)
            {
                _popup.PopupEntity(
                    Loc.GetString("cm-xeno-evolution-failed-cannot-support"),
                    xeno,
                    xeno,
                    PopupType.MediumCaution
                );
            }

            return false;
        }

        // Stories-EvoQueue-Start
        var hasQueueOffer = false;
        if (_evolutionQueueEnabled &&
            !ignoreSlotLimit &&
            newXenoComp != null &&
            _xenoHive.GetHive(xeno.Owner) is { } queueHive &&
            _xenoHive.IsQueuedCaste(queueHive, newXeno))
        {
            var queueNow = _gameTicker.RoundDuration();
            hasQueueOffer = TryComp(xeno, out XenoEvolutionQueueComponent? queue) &&
                            queue.OfferedTier == newXenoComp.Tier &&
                            ((queue.OfferedUntil is { } offeredUntil && offeredUntil > queueNow) ||
                             (queue.EvolvingUntil is { } evolvingUntil && evolvingUntil > queueNow));

            if (!hasQueueOffer)
            {
                if (doPopup)
                    _popup.PopupEntity(Loc.GetString("stories-xeno-queue-wait"), xeno, xeno, PopupType.MediumCaution);

                return false;
            }
        }
        // Stories-EvoQueue-End

        if (newXenoComp != null &&
            !newXenoComp.BypassTierCount &&
            !hasQueueOffer && // Stories-EvoQueue
            !ignoreSlotLimit && // Stories-EvoQueue
            _xenoHive.GetHive(xeno.Owner) is { } oldHive &&
            _xenoHive.TryGetTierLimit((oldHive, oldHive.Comp), newXenoComp.Tier, out var limit))
        {
            _xenoHive.GetTierOccupancy(oldHive, newXenoComp.Tier, out var total, out var existing, out var slotCount);

            if (total != 0 && existing / (float)total >= limit && (!slotCount.ContainsKey(newXeno) || slotCount[newXeno] <= 0))
            {
                if (doPopup)
                {
                    _popup.PopupEntity(
                        Loc.GetString("cm-xeno-evolution-failed-hive-full", ("tier", newXenoComp.Tier)),
                        xeno,
                        xeno,
                        PopupType.MediumCaution
                    );
                }

                return false;
            }
        }

        if (TryComp(xeno, out XenoRecentlyDevolvedComponent? recently) &&
            recently.Recent.TryGetValue(newXeno, out var at) &&
            at + _evolveSameCasteCooldown > _timing.CurTime)
        {
            var timeRemaining = at + _evolveSameCasteCooldown - _timing.CurTime;
            var msg = Loc.GetString("rmc-xeno-evolution-cant-evolve-caste-cooldown",
                ("minutes", timeRemaining.Minutes),
                ("seconds", timeRemaining.Seconds));

            if (doPopup)
                _popup.PopupEntity(msg, xeno, xeno, PopupType.MediumCaution);

            return false;
        }

        return true;
    }

    private bool CanEvolveAny(Entity<XenoEvolutionComponent> xeno)
    {
        if (xeno.Comp.Points >= xeno.Comp.Max &&
            (xeno.Comp.EvolvesTo.Count > 0 || (!xeno.Comp.MarinesLanded && xeno.Comp.EarlyEvolvesTo.Count > 0)))
            return true;

        foreach (var evolution in xeno.Comp.EvolvesToWithoutPoints)
        {
            if (CanEvolvePopup(xeno, evolution, false))
                return true;
        }

        return false;
    }

    // TODO RMC14 make this a property of the hive component
    // TODO RMC14 per-hive
    public int GetLiving<T>(Predicate<Entity<T>>? predicate = null) where T : IComponent
    {
        var total = 0;
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            if (predicate != null && !predicate((uid, comp)))
                continue;

            total++;
        }

        return total;
    }

    // TODO RMC14 make this a property of the hive component
    // TODO RMC14 per-hive
    public bool HasLiving<T>(int count, Predicate<Entity<T>>? predicate = null) where T : IComponent
    {
        if (count <= 0)
            return true;

        var total = 0;
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            if (predicate != null && !predicate((uid, comp)))
                continue;

            total++;

            if (total >= count)
                return true;
        }

        return false;
    }

    private bool HasLivingInHive<T>(EntityUid hiveMember, int count, Predicate<Entity<T>>? predicate = null) where T : IComponent
    {
        if (count <= 0)
            return true;

        var total = 0;
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_xenoHive.FromSameHive(uid, hiveMember))
                continue;

            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            if (predicate != null && !predicate((uid, comp)))
                continue;

            total++;

            if (total >= count)
                return true;
        }

        return false;
    }

    public FixedPoint2 AddPointsCapped(Entity<XenoEvolutionComponent?> evolution, FixedPoint2 points)
    {
        if (!Resolve(evolution, ref evolution.Comp, false))
            return FixedPoint2.Zero;

        var oldPoints = evolution.Comp.Points;
        evolution.Comp.Points += FixedPoint2.Min(evolution.Comp.Max, points);
        Dirty(evolution);

        return evolution.Comp.Points - oldPoints;
    }

    public void SetPoints(Entity<XenoEvolutionComponent> evolution, FixedPoint2 points)
    {
        evolution.Comp.Points = points;
        Dirty(evolution);
    }

    public bool NeedsOvipositor()
    {
        return _gameTicker.RoundDuration() > _evolutionPointsRequireOvipositorAfter;
    }

    public bool HasOvipositor()
    {
        return HasLiving<XenoEvolutionGranterComponent>(1, e => HasComp<XenoAttachedOvipositorComponent>(e));
    }

    public bool HasOvipositor(EntityUid hiveMember)
    {
        return HasLivingInHive<XenoEvolutionGranterComponent>(hiveMember,
            1,
            e => HasComp<XenoAttachedOvipositorComponent>(e));
    }

    public bool LackingOvipositor()
    {
        return NeedsOvipositor() && !HasOvipositor();
    }

    public bool LackingOvipositor(EntityUid hiveMember)
    {
        return NeedsOvipositor() && !HasOvipositor(hiveMember);
    }

    private bool MarinesHaveLanded()
    {
        var query = EntityQueryEnumerator<ActiveGameRuleComponent, CMDistressSignalRuleComponent>();
        while (query.MoveNext(out _, out var distress))
        {
            return distress.MarinesLanded;
        }

        return false;
    }

    private bool HiveHasLivingQueen(EntityUid xeno)
    {
        if (_xenoHive.GetHive(xeno) is not { } hive)
            return false;

        var queen = hive.Comp.CurrentQueen;
        if (queen == null || TerminatingOrDeleted(queen.Value))
            return false;

        return !_mobState.IsDead(queen.Value);
    }

    private EntityUid TransferXeno(EntityUid xeno, EntProtoId proto)
    {
        var coordinates = _transform.GetMoverCoordinates(xeno);
        var newXeno = Spawn(proto, coordinates);
        _xenoHive.SetSameHive(xeno, newXeno);

        RemComp<CanBeLarvaQueuedComponent>(xeno);

        if (_mind.TryGetMind(xeno, out var mindId, out _))
        {
            _mind.TransferTo(mindId, newXeno);
            _mind.UnVisit(mindId);
        }

        foreach (var held in _hands.EnumerateHeld(xeno))
        {
            _hands.TryDrop(xeno, held);
        }

        // TODO RMC14 this is a hack because climbing on a newly created entity does not work properly for the client
        var comp = EnsureComp<XenoNewlyEvolvedComponent>(newXeno);

        _doors.Clear();
        _entityLookup.GetEntitiesIntersecting(xeno, _doors);
        foreach (var id in _doors)
        {
            if (HasComp<DoorComponent>(id) || HasComp<AirlockComponent>(id))
                comp.StopCollide.Add(id);
        }

        var newRecently = EnsureComp<XenoRecentlyDevolvedComponent>(newXeno);
        if (TryComp(xeno, out XenoRecentlyDevolvedComponent? oldRecently))
        {
            foreach (var (id, time) in oldRecently.Recent)
            {
                newRecently.Recent[id] = time;
            }
        }

        if (Prototype(xeno)?.ID is { } oldId)
            newRecently.Recent[oldId] = _timing.CurTime;

        return newXeno;
    }

    private void TryDevolve(Entity<XenoDevolveComponent> xeno, EntProtoId to, bool damagedCheck = true)
    {
        if (damagedCheck && !DamagedCheckPopup(xeno))
            return;

        if (Devolve(xeno, to) is { } newXeno && _net.IsServer)
            _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-devolve", ("xeno", newXeno)), newXeno, newXeno, PopupType.LargeCaution);
    }

    public EntityUid? Devolve(Entity<XenoDevolveComponent> xeno, EntProtoId to)
    {
        if (_net.IsClient ||
            !xeno.Comp.DevolvesTo.Contains(to))
        {
            return null;
        }

        var newXeno = TransferXeno(xeno, to);
        var ev = new XenoDevolvedEvent(xeno, newXeno);
        RaiseLocalEvent(newXeno, ref ev, true);

        _adminLog.Add(LogType.RMCDevolve, $"Xenonid {ToPrettyString(xeno)} devolved into {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);

        return newXeno;
    }

    public override void Update(float frameTime)
    {
        var newly = EntityQueryEnumerator<XenoNewlyEvolvedComponent>();
        while (newly.MoveNext(out var uid, out var comp))
        {
            if (comp.TriedClimb)
            {
                _intersecting.Clear();
                _entityLookup.GetEntitiesIntersecting(uid, _intersecting);
                for (var i = comp.StopCollide.Count - 1; i >= 0; i--)
                {
                    var colliding = comp.StopCollide[i];
                    if (!_intersecting.Contains(colliding))
                        comp.StopCollide.RemoveAt(i);
                }

                if (comp.StopCollide.Count == 0)
                    RemCompDeferred<XenoNewlyEvolvedComponent>(uid);

                continue;
            }

            comp.TriedClimb = true;
            if (TryComp(uid, out ClimbingComponent? climbing))
            {
                _climbable.Clear();
                _entityLookup.GetEntitiesIntersecting(uid, _climbable);

                foreach (var intersecting in _climbable)
                {
                    if (HasComp<ClimbableComponent>(intersecting))
                    {
                        _climb.ForciblySetClimbing(uid, intersecting);
                        Dirty(uid, climbing);
                        break;
                    }
                }
            }
        }

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var roundDuration = _gameTicker.RoundDuration();

        // Stories-EvoQueue-Start
        if (_evolutionQueueEnabled && time >= _nextQueueUpdate)
        {
            _nextQueueUpdate = time + TimeSpan.FromSeconds(1);
            UpdateEvolutionQueue(roundDuration);
        }
        // Stories-EvoQueue-End

        var needsOvipositor = NeedsOvipositor();
        if (needsOvipositor)
        {
            var granters = EntityQueryEnumerator<XenoEvolutionGranterComponent>();
            while (granters.MoveNext(out var uid, out var granter))
            {
                if (granter.GotOvipositorPopup)
                    continue;

                granter.GotOvipositorPopup = true;
                Dirty(uid, granter);

                _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-ovipositor-needed"),
                    uid,
                    uid,
                    PopupType.LargeCaution
                );

                _xenoHive.AnnounceNeedsOvipositorToSameHive(uid);
            }
        }

        var evoBonus = FixedPoint2.Zero;
        var bonuses = EntityQueryEnumerator<EvolutionBonusComponent>();
        while (bonuses.MoveNext(out var comp))
        {
            evoBonus += comp.Amount;
        }

        FixedPoint2? evoOverride = null;
        var overrides = EntityQueryEnumerator<EvolutionOverrideComponent>();
        while (overrides.MoveNext(out var comp))
        {
            evoOverride = comp.Amount;
        }

        var evolution = EntityQueryEnumerator<XenoEvolutionComponent>();
        while (evolution.MoveNext(out var uid, out var comp))
        {
            if (comp.Max == FixedPoint2.Zero)
                continue;

            if (time < comp.LastPointsAt + TimeSpan.FromSeconds(1))
                continue;

            comp.LastPointsAt = time;
            Dirty(uid, comp);

            if (!comp.GotPopup && CanEvolveAny((uid, comp)))
            {
                comp.GotPopup = true;
                Dirty(uid, comp);

                _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-ready"), uid, uid, PopupType.Large);
                _audio.PlayEntity(comp.EvolutionReadySound, uid, uid);
                continue;
            }
            var points = (_earlyEvoBoostBefore > _gameTicker.RoundDuration()) ? comp.EarlyPointsPerSecond : comp.PointsPerSecond;
            var gain = evoOverride ?? points + evoBonus;
            var hasEvolutionBoon = _xenoBoon.TryGetActiveBoon<HiveBoonEvolutionComponent>(uid, out var evolutionBoon);

            if (comp.Points < comp.Max || roundDuration < _evolutionAccumulatePointsBefore)
            {
                var hasGranter = needsOvipositor
                    ? HasOvipositor(uid)
                    : HasLivingInHive<XenoEvolutionGranterComponent>(uid, 1);

                var bypassesGranter = hasEvolutionBoon && evolutionBoon.Comp.BypassOvipositor;
                if (comp.RequiresGranter && !hasGranter && !bypassesGranter)
                    continue;

                var gainToApply = hasEvolutionBoon
                    ? GetFrozenEvolutionBoonGain((uid, comp), evolutionBoon) * evolutionBoon.Comp.Multiplier
                    : gain;
                SetPoints((uid, comp), comp.Points + gainToApply);
            }
            else if (comp.Points > comp.Max)
            {
                SetPoints((uid, comp), FixedPoint2.Max(comp.Points - gain, comp.Max));
            }
        }
    }

    private FixedPoint2 GetFrozenEvolutionBoonGain(Entity<XenoEvolutionComponent> xeno, Entity<HiveBoonEvolutionComponent> boon)
    {
        var points = boon.Comp.FrozenEarlyEvolutionBoost
            ? xeno.Comp.EarlyPointsPerSecond
            : xeno.Comp.PointsPerSecond;

        return boon.Comp.HasFrozenOverride
            ? boon.Comp.FrozenOverride
            : points + boon.Comp.FrozenBonus;
    }

    // Stories-EvoQueue-Start
    private XenoEvolveBuiState BuildEvolveState(EntityUid xeno)
    {
        return new XenoEvolveBuiState(LackingOvipositor(xeno));
    }

    private bool TryGetCasteTier(EntProtoId caste, out int tier)
    {
        tier = 0;
        if (!_prototypes.TryIndex(caste, out var proto) ||
            !proto.TryGetComponent(out XenoComponent? xenoComp, _compFactory))
        {
            return false;
        }

        tier = xenoComp.Tier;
        return true;
    }

    private bool CanTakeQueuedSlot(Entity<XenoEvolutionComponent> xeno, Entity<HiveComponent> hive, int tier)
    {
        foreach (var caste in xeno.Comp.EvolvesTo)
        {
            if (_xenoHive.IsQueuedCaste(hive, caste) &&
                TryGetCasteTier(caste, out var t) && t == tier &&
                CanEvolvePopup(xeno, caste, false, true))
            {
                return true;
            }
        }

        return false;
    }

    private List<Entity<XenoEvolutionQueueComponent>> GetQueueCandidates(Entity<HiveComponent> hive, int tier, TimeSpan roundDuration)
    {
        var result = new List<Entity<XenoEvolutionQueueComponent>>();

        var query = EntityQueryEnumerator<XenoEvolutionQueueComponent, XenoEvolutionComponent, ActorComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out var queue, out var evo, out _, out var member))
        {
            if (member.Hive != hive.Owner ||
                queue.OfferedUntil != null ||
                queue.EvolvingUntil != null || // Stories-EvoQueue
                (queue.PassedUntil is { } passed && passed > roundDuration) ||
                _mobState.IsDead(uid) ||
                evo.Points < evo.Max ||
                !CanTakeQueuedSlot((uid, evo), hive, tier))
            {
                continue;
            }

            result.Add((uid, queue));
        }

        return result;
    }

    private void UpdateEvolutionQueue(TimeSpan roundDuration)
    {
        var refresh = new HashSet<EntityUid>();

        var pending = new Dictionary<(EntityUid Hive, int Tier), int>();
        var offers = EntityQueryEnumerator<XenoEvolutionQueueComponent>();
        while (offers.MoveNext(out var uid, out var queue))
        {
            if (queue.EvolvingUntil is { } evolving)
            {
                if (evolving > roundDuration &&
                    !_mobState.IsDead(uid) &&
                    HasComp<ActorComponent>(uid) &&
                    _xenoHive.GetHive(uid) is { } evolvingHive)
                {
                    var evolvingKey = (evolvingHive.Owner, queue.OfferedTier);
                    pending[evolvingKey] = pending.GetValueOrDefault(evolvingKey) + 1;
                    continue;
                }

                queue.EvolvingUntil = null;
                Dirty(uid, queue);
            }

            if (queue.OfferedUntil is not { } until)
                continue;

            if (_xenoHive.GetHive(uid) is not { } offerHive)
            {
                queue.OfferedUntil = null;
                Dirty(uid, queue);
                continue;
            }

            if (_mobState.IsDead(uid) || !HasComp<ActorComponent>(uid))
            {
                DeclineOffer((uid, queue), null);
                refresh.Add(offerHive.Owner);
                continue;
            }

            if (until <= roundDuration)
            {
                DeclineOffer((uid, queue), Loc.GetString("stories-xeno-queue-expired"));
                refresh.Add(offerHive.Owner);
                continue;
            }

            var key = (offerHive.Owner, queue.OfferedTier);
            pending[key] = pending.GetValueOrDefault(key) + 1;
        }

        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var hiveId, out var hiveComp))
        {
            var hive = new Entity<HiveComponent>(hiveId, hiveComp);
            foreach (var tier in hiveComp.TierLimits.Keys)
            {
                var need = _xenoHive.GetOpenTierSlots(hive, tier) - pending.GetValueOrDefault((hiveId, tier));
                if (need <= 0)
                    continue;

                var candidates = GetQueueCandidates(hive, tier, roundDuration);
                if (candidates.Count == 0)
                    continue;

                _random.Shuffle(candidates);
                var ordered = candidates.OrderBy(c => c.Comp.TierEnteredAt);

                foreach (var candidate in ordered)
                {
                    if (need <= 0)
                        break;

                    candidate.Comp.OfferedUntil = roundDuration + _evolutionQueueGrace;
                    candidate.Comp.OfferedTier = tier;
                    Dirty(candidate);
                    _popup.PopupEntity(
                        Loc.GetString("stories-xeno-queue-offered", ("seconds", (int)_evolutionQueueGrace.TotalSeconds)),
                        candidate, candidate, PopupType.Large);
                    refresh.Add(hiveId);
                    need--;
                }
            }
        }

        foreach (var hiveId in refresh)
            RefreshHiveEvolutionUis(hiveId);
    }

    private void RefreshHiveEvolutionUis(EntityUid hiveId)
    {
        var xenos = EntityQueryEnumerator<ActorComponent, XenoEvolutionComponent, HiveMemberComponent>();
        while (xenos.MoveNext(out var uid, out _, out _, out var member))
        {
            if (member.Hive != hiveId)
                continue;

            _ui.SetUiState(uid, XenoEvolutionUIKey.Key, BuildEvolveState(uid));
        }
    }
    // Stories-EvoQueue-End
}
