using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Stories.AntiGrief.Cadet;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Extensions;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Rules;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Construction.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Rangefinder.Spotting;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Communications;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Throwing;

namespace Content.Shared._RMC14.Mortar;

public abstract class SharedMortarSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogs = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly FixtureSystem _fixture = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedCMChatSystem _rmcChat = default!;
    [Dependency] private readonly SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private readonly HashSet<MortarTargetInfo> _guidedTargets = new();

    public override void Initialize()
    {
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<MortarComponent, UseInHandEvent>(OnMortarUseInHand, before: [typeof(ActivatableUISystem)]);
        SubscribeLocalEvent<MortarComponent, DeployMortarDoAfterEvent>(OnMortarDeployDoAfter);
        SubscribeLocalEvent<MortarComponent, TargetMortarDoAfterEvent>(OnMortarTargetDoAfter);
        SubscribeLocalEvent<MortarComponent, DialMortarDoAfterEvent>(OnMortarDialDoAfter);
        SubscribeLocalEvent<MortarComponent, InteractUsingEvent>(OnMortarInteractUsing);
        SubscribeLocalEvent<MortarComponent, LoadMortarShellDoAfterEvent>(OnMortarLoadDoAfter);
        SubscribeLocalEvent<MortarComponent, UnanchorAttemptEvent>(OnMortarUnanchorAttempt);
        SubscribeLocalEvent<MortarComponent, AnchorStateChangedEvent>(OnMortarAnchorStateChanged);
        SubscribeLocalEvent<MortarComponent, ExaminedEvent>(OnMortarExamined);
        SubscribeLocalEvent<MortarComponent, ActivatableUIOpenAttemptEvent>(OnMortarActivatableUIOpenAttempt);
        SubscribeLocalEvent<MortarComponent, CombatModeShouldHandInteractEvent>(OnMortarShouldInteract);
        SubscribeLocalEvent<MortarComponent, DestructionEventArgs>(OnMortarDestruction);
        SubscribeLocalEvent<MortarComponent, BeforeDamageChangedEvent>(OnMortarBeforeDamageChanged);

        SubscribeLocalEvent<MortarCameraShellComponent, MortarShellLandEvent>(OnMortarCameraShellLand);

        Subs.BuiEvents<MortarComponent>(MortarUiKey.Key,
            subs =>
            {
                subs.Event<MortarTargetBuiMsg>(OnMortarTargetBui);
                subs.Event<MortarDialBuiMsg>(OnMortarDialBui);
                subs.Event<MortarViewCamerasMsg>(OnMortarViewCameras);
                subs.Event<MortarSetTargetEntityMsg>(OnMortarSetTargetEntity);
                subs.Event<MortarFlightTimeChangedMsg>(OnMortarFlightTimeChanged);
            });

        SubscribeLocalEvent<LaserDesignatorTargetComponent, MapInitEvent>(OnGuidedTargetInit);
        SubscribeLocalEvent<SpottedComponent, MapInitEvent>(OnGuidedTargetInit);
        SubscribeLocalEvent<ActiveFlareSignalComponent, MapInitEvent>(OnGuidedTargetInit);

        SubscribeLocalEvent<LaserDesignatorTargetComponent, ComponentShutdown>(OnGuidedTargetShutdown);
        SubscribeLocalEvent<SpottedComponent, ComponentShutdown>(OnGuidedTargetShutdown);
        SubscribeLocalEvent<ActiveFlareSignalComponent, ComponentShutdown>(OnGuidedTargetShutdown);
    }

    private void OnMortarBeforeDamageChanged(Entity<MortarComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!ent.Comp.Deployed) // cannot destroy in item form
            args.Cancelled = true;
    }

    private void OnMortarDestruction(Entity<MortarComponent> mortar, ref DestructionEventArgs args)
    {
        if (!mortar.Comp.Deployed || _net.IsClient)
            return;

        SpawnAtPosition(mortar.Comp.Drop, mortar.Owner.ToCoordinates());
    }

    private void OnMortarUseInHand(Entity<MortarComponent> mortar, ref UseInHandEvent args)
    {
        args.Handled = true;

        // Stories-AntiGrief-Start
        if (HasComp<CadetComponent>(args.User))
        {
            var popup = Loc.GetString("stories-cadet-mortar-use");
            _popup.PopupClient(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }
        // Stories-AntiGrief-End

        DeployMortar(mortar, args.User);
    }

    private void OnMortarDeployDoAfter(Entity<MortarComponent> mortar, ref DeployMortarDoAfterEvent args)
    {
        var user = args.User;
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (mortar.Comp.Deployed)
            return;

        if (!CanDeployPopup(mortar, user))
            return;

        mortar.Comp.Deployed = true;
        Dirty(mortar);

        if (_fixture.GetFixtureOrNull(mortar, mortar.Comp.FixtureId) is { } fixture)
            _physics.SetHard(mortar, fixture, true);

        _appearance.SetData(mortar, MortarVisualLayers.State, MortarVisuals.Deployed);

        var xform = Transform(mortar);
        var coordinates = _transform.GetMoverCoordinates(mortar, xform);
        var rotation = Transform(user).LocalRotation.GetCardinalDir().ToAngle();
        _transform.SetCoordinates(mortar, xform, coordinates, rotation);
        _transform.AnchorEntity((mortar, xform));

        if (!_rmcPlanet.IsOnPlanet(coordinates))
            _popup.PopupClient(Loc.GetString("rmc-mortar-deploy-end-not-planet"), user, user, PopupType.MediumCaution);

        _audio.PlayPredicted(mortar.Comp.DeploySound, mortar, user);
    }

    private void OnMortarTargetDoAfter(Entity<MortarComponent> mortar, ref TargetMortarDoAfterEvent args)
    {
        // Stories-AntiGrief-Start
        if (HasComp<CadetComponent>(args.User))
        {
            var popup = Loc.GetString("stories-cadet-mortar-use");
            _popup.PopupClient(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }
        // Stories-AntiGrief-End

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        var selfMsg = Loc.GetString("rmc-mortar-target-finish-self", ("mortar", mortar));
        var othersMsg = Loc.GetString("rmc-mortar-target-finish-others", ("user", user), ("mortar", mortar));
        _popup.PopupPredicted(selfMsg, othersMsg, user, user);
        if (_net.IsClient)
            return;

        var target = args.Vector;
        var position = _transform.GetMapCoordinates(mortar).Position;
        var offset = target;
        if (_rmcPlanet.TryGetOffset(_transform.GetMapCoordinates(mortar.Owner), out var planetOffset))
            offset -= planetOffset;

        mortar.Comp.Target = target;

        var tilesPer = mortar.Comp.TilesPerOffset;
        var xOffset = (int) Math.Floor(Math.Abs(offset.X - position.X) / tilesPer);
        var yOffset = (int) Math.Floor(Math.Abs(offset.Y - position.Y) / tilesPer);
        mortar.Comp.Offset = (_random.Next(-xOffset, xOffset + 1), _random.Next(-yOffset, yOffset + 1));

        Dirty(mortar);
    }

    private void OnMortarDialDoAfter(Entity<MortarComponent> mortar, ref DialMortarDoAfterEvent args)
    {
        // Stories-AntiGrief-Start
        if (HasComp<CadetComponent>(args.User))
        {
            var popup = Loc.GetString("stories-cadet-mortar-use");
            _popup.PopupClient(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }
        // Stories-AntiGrief-End

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        mortar.Comp.Dial = args.Vector;
        Dirty(mortar);

        var user = args.User;
        var selfMsg = Loc.GetString("rmc-mortar-dial-finish-self", ("mortar", mortar));
        var othersMsg = Loc.GetString("rmc-mortar-dial-finish-others", ("user", user), ("mortar", mortar));
        _popup.PopupPredicted(selfMsg, othersMsg, user, user);
    }

    private void OnMortarInteractUsing(Entity<MortarComponent> mortar, ref InteractUsingEvent args)
    {
        // Stories-AntiGrief-Start
        if (HasComp<CadetComponent>(args.User))
        {
            var popup = Loc.GetString("stories-cadet-mortar-use");
            _popup.PopupClient(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }
        // Stories-AntiGrief-End

        var shellId = args.Used;
        if (!TryComp(shellId, out MortarShellComponent? shell))
            return;

        args.Handled = true;
        var user = args.User;
        if (!HasSkillPopup(mortar, user, true))
            return;

        if (!CanLoadPopup(mortar, (shellId, shell), user, out _, out _))
            return;

        var ev = new LoadMortarShellDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, shell.LoadDelay, ev, mortar, mortar, shellId)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            ForceVisible = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var selfMsg = Loc.GetString("rmc-mortar-shell-load-start-self", ("mortar", mortar), ("shell", shellId));
            var othersMsg = Loc.GetString("rmc-mortar-shell-load-start-others",
                ("user", user),
                ("mortar", mortar),
                ("shell", shellId));
            _popup.PopupPredicted(selfMsg, othersMsg, mortar, user);

            _audio.PlayPredicted(mortar.Comp.ReloadSound, mortar, user);
        }
    }

    private void OnMortarLoadDoAfter(Entity<MortarComponent> mortar, ref LoadMortarShellDoAfterEvent args)
    {
        // Stories-AntiGrief-Start
        if (HasComp<CadetComponent>(args.User))
        {
            var popup = Loc.GetString("stories-cadet-mortar-use");
            _popup.PopupClient(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }
        // Stories-AntiGrief-End

        var user = args.User;
        if (args.Cancelled || args.Handled || args.Used is not { } shellId)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (!TryComp(shellId, out MortarShellComponent? shell))
            return;

        if (!mortar.Comp.Deployed)
            return;

        if (HasComp<ActiveMortarShellComponent>(shellId))
            return;

        if (!CanLoadPopup(mortar, (shellId, shell), user, out var travelTime, out var coordinates))
            return;

        _adminLogs.Add(LogType.RMCMortar, LogImpact.High, $"Mortar {ToPrettyString(mortar)} shell {ToPrettyString(shellId)} shot by {ToPrettyString(user)} aimed at {coordinates}");

        var container = _container.EnsureContainer<Container>(mortar, mortar.Comp.ContainerId);
        if (!_container.Insert(shellId, container))
            return;

        var time = _timing.CurTime;

        var active = new ActiveMortarShellComponent
        {
            Coordinates = _transform.ToCoordinates(coordinates),
            WarnAt = time + travelTime,
            ImpactWarnAt = time + travelTime + shell.ImpactWarningDelay,
            LandAt = time + travelTime + shell.ImpactDelay,
            IsGuided = shell.Guided,
            TargetEntity = mortar.Comp.LockedEntityTarget,
            LastUpdate = time,
            Mortar = mortar
        };

        var selfMsg = Loc.GetString("rmc-mortar-shell-load-finish-self", ("mortar", mortar), ("shell", shellId));
        var othersMsg = Loc.GetString("rmc-mortar-shell-load-finish-others", ("user", user), ("mortar", mortar), ("shell", shellId));
        _popup.PopupPredicted(selfMsg, othersMsg, user, user);

        othersMsg = Loc.GetString("rmc-mortar-shell-fire", ("mortar", mortar));
        _popup.PopupEntity(othersMsg, mortar, PopupType.MediumCaution);

        var filter = Filter.Pvs(mortar);
        _audio.PlayPvs(mortar.Comp.FireSound, mortar);

        var ev = new MortarFiredEvent(GetNetEntity(mortar));
        RaiseNetworkEvent(ev, filter);

        if (shell.Guided && (!_solution.TryGetSolution(shellId, shell.SolutionId, out var soln, out var solu) ||
         solu.Volume < shell.FuelVolume))
        {
            var mortarCoords = mortar.Owner.ToCoordinates();

            var rand = _random.NextFloat();
            if (rand < 0.2f)
            {
                _popup.PopupCoordinates("Снаряд подлетает на несколько метров вверх, сваливается на землю и подрывается! Кто-то должен быть прочесть инструкцию.", mortarCoords);
                _rmcExplosion.TriggerExplosive(shellId);

                QueueDel(shellId);
            }
            else if (rand < 0.7f)
            {
                _container.Remove(shellId, container, true, true);
                var origin = _transform.GetMapCoordinates(shellId);
                var target = _transform.GetMapCoordinates(mortar);
                var diff = (target.Position - origin.Position).Normalized() * 2;

                _popup.PopupCoordinates("Снаряд подлетает на несколько метров вверх и сваливается на землю! Кому-то сегодня очень повезло.", mortarCoords);
                _throwing.TryThrow(shellId, diff, 10);
            }
            else
            {
                _popup.PopupCoordinates(
                    "Снаряд не вылетел и застрял в миномёте! Кто-то должен проверить инструкции.",
                    mortarCoords);
                mortar.Comp.NeedAbort = true;
            }
            return;
        }

        mortar.Comp.LastFiredAt = time;

        AddComp(shellId, active, true);

        if (shell.Guided && mortar.Comp.ProjectileFlightTime > TimeSpan.Zero)
        {
            var flightTime = mortar.Comp.ProjectileFlightTime.TotalSeconds;
            
            TimeSpan warnDelay, impactWarnDelay;
            
            if (flightTime <= 4)
            {
                warnDelay = TimeSpan.FromSeconds(1);
                impactWarnDelay = TimeSpan.FromSeconds(Math.Max(1, flightTime - 1));
            }
            else if (flightTime <= 7)
            {
                warnDelay = mortar.Comp.ProjectileFlightTime / 2;
                impactWarnDelay = TimeSpan.FromSeconds(flightTime - 1);
            }
            else
            {
                warnDelay = TimeSpan.FromSeconds(3.0);
                impactWarnDelay = TimeSpan.FromSeconds(flightTime - 2);
            }
            
            active.LandAt = time + mortar.Comp.ProjectileFlightTime;
            active.WarnAt = time + warnDelay;
            active.ImpactWarnAt = time + impactWarnDelay;

            Dirty(mortar);
        }
    }

    private void OnMortarUnanchorAttempt(Entity<MortarComponent> mortar, ref UnanchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasSkillPopup(mortar, args.User, true))
            args.Cancel();
    }

    private void OnMortarAnchorStateChanged(Entity<MortarComponent> mortar, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        mortar.Comp.Deployed = false;
        Dirty(mortar);

        if (_fixture.GetFixtureOrNull(mortar, mortar.Comp.FixtureId) is { } fixture)
            _physics.SetHard(mortar, fixture, false);

        _appearance.SetData(mortar, MortarVisualLayers.State, MortarVisuals.Item);

        if (mortar.Comp.NeedAbort && _container.TryGetContainer(mortar, mortar.Comp.ContainerId, out var container))
        {
            foreach (var shell in container.ContainedEntities)
            {
                if (HasComp<ActiveMortarShellComponent>(shell))
                    continue;

                _container.Remove(shell, container);
                mortar.Comp.NeedAbort = false;
            }
        }
    }

    private void OnMortarExamined(Entity<MortarComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(MortarComponent)))
        {
            args.PushMarkup(Loc.GetString("rmc-mortar-less-accurate-with-range"));
        }
    }

    private void OnMortarActivatableUIOpenAttempt(Entity<MortarComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Deployed)
            args.Cancel();
    }

    private void OnMortarShouldInteract(Entity<MortarComponent> ent, ref CombatModeShouldHandInteractEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMortarCameraShellLand(Entity<MortarCameraShellComponent> ent, ref MortarShellLandEvent args)
    {
        _audio.PlayPvs(ent.Comp.Sound, args.Coordinates);

        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(args.Coordinates);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<MortarCameraComponent>(uid))
                QueueDel(uid);
        }

        var coords = _transform.ToMapCoordinates(args.Coordinates);
        Spawn(ent.Comp.Flare, coords);
        var camera = Spawn(ent.Comp.Camera, coords);

        if (_rmcPlanet.TryGetOffset(coords, out var offset))
            coords = coords.Offset(offset);

        var (x, y) = coords.Position;
        _metaData.SetEntityName(camera, Loc.GetString("rmc-mortar-camera-name", ("x", (int) x), ("y", (int) y)));
    }

    private void OnMortarTargetBui(Entity<MortarComponent> mortar, ref MortarTargetBuiMsg args)
    {
        args.Target.X.Cap(mortar.Comp.MaxTarget);
        args.Target.Y.Cap(mortar.Comp.MaxTarget);

        var user = args.Actor;
        var ev = new TargetMortarDoAfterEvent(args.Target);
        var doAfter = new DoAfterArgs(EntityManager, user, mortar.Comp.TargetDelay, ev, mortar)
        {
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var selfMsg = Loc.GetString("rmc-mortar-target-start-self", ("mortar", mortar));
            var othersMsg = Loc.GetString("rmc-mortar-target-start-others", ("user", user), ("mortar", mortar));
            _popup.PopupPredicted(selfMsg, othersMsg, user, user);
        }
    }

    private void OnMortarDialBui(Entity<MortarComponent> mortar, ref MortarDialBuiMsg args)
    {
        args.Target.X.Cap(mortar.Comp.MaxDial);
        args.Target.Y.Cap(mortar.Comp.MaxDial);

        var user = args.Actor;
        var ev = new DialMortarDoAfterEvent(args.Target);
        var doAfter = new DoAfterArgs(EntityManager, user, mortar.Comp.TargetDelay, ev, mortar)
        {
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var selfMsg = Loc.GetString("rmc-mortar-dial-start-self", ("mortar", mortar));
            var othersMsg = Loc.GetString("rmc-mortar-dial-start-others", ("user", user), ("mortar", mortar));
            _popup.PopupPredicted(selfMsg, othersMsg, user, user);
        }
    }

    private void OnMortarViewCameras(Entity<MortarComponent> ent, ref MortarViewCamerasMsg args)
    {
        _ui.OpenUi(ent.Owner, RMCCameraUiKey.Key, args.Actor);
    }

    private void OnMortarSetTargetEntity(Entity<MortarComponent> mortar, ref MortarSetTargetEntityMsg args)
    {
        if (_net.IsClient)
            return;

        var user = args.Actor;

        if (!HasActiveCommunicationTower(mortar))
        {
            _popup.PopupCursor(Loc.GetString("need-active-tower"), user, PopupType.Medium);
            RefreshAllMortarBUIs(_guidedTargets.ToList());
            return;
        }

        mortar.Comp.LockedEntityTarget = GetEntity(args.TargetEntity);
        mortar.Comp.Target = args.Coordinates;

        Dirty(mortar);

        var selfMsg = Loc.GetString("rmc-mortar-target-locked", ("target", GetEntity(args.TargetEntity)));
        _popup.PopupCursor(selfMsg, user, PopupType.Medium);

        RefreshAllMortarBUIs(_guidedTargets.ToList());
    }

    private void OnMortarFlightTimeChanged(Entity<MortarComponent> mortar, ref MortarFlightTimeChangedMsg args)
    {
        if (_net.IsClient)
            return;

        mortar.Comp.ProjectileFlightTime = args.FlightTime;

        Dirty(mortar);
        RefreshAllMortarBUIs(_guidedTargets.ToList());
    }

    private void DeployMortar(Entity<MortarComponent> mortar, EntityUid user)
    {
        if (mortar.Comp.Deployed)
            return;

        if (!CanDeployPopup(mortar, user))
            return;

        var ev = new DeployMortarDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, user, mortar.Comp.DeployDelay, ev, mortar)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupClient(Loc.GetString("rmc-mortar-deploy-start", ("mortar", mortar)), user, user);
    }

    protected bool HasSkillPopup(Entity<MortarComponent> mortar, EntityUid user, bool predicted)
    {
        if (_skills.HasSkills(user, mortar.Comp.Skill))
            return true;

        var msg = Loc.GetString("rmc-skills-no-training", ("target", mortar));
        if (predicted)
            _popup.PopupClient(msg, user, user, PopupType.SmallCaution);
        else
            _popup.PopupEntity(msg, user, user, PopupType.SmallCaution);

        return false;
    }

    private bool CanDeployPopup(Entity<MortarComponent> mortar, EntityUid user)
    {
        if (!HasSkillPopup(mortar, user, true))
            return false;

        if (!_area.CanMortarPlacement(user.ToCoordinates()))
        {
            _popup.PopupClient(Loc.GetString("rmc-mortar-covered", ("mortar", mortar)), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    protected virtual bool CanLoadPopup(
        Entity<MortarComponent> mortar,
        Entity<MortarShellComponent> shell,
        EntityUid user,
        out TimeSpan travelTime,
        out MapCoordinates coordinates)
    {
        travelTime = default;
        coordinates = default;
        return false;
    }

    public void PopupWarning(MapCoordinates coordinates, float range, LocId warning, LocId warningAbove, bool chat = false)
    {
        foreach (var session in _player.NetworkedSessions)
        {
            if (session.AttachedEntity is not { } recipient ||
                !_transformQuery.TryComp(recipient, out var xform) ||
                xform.MapID != coordinates.MapId)
            {
                continue;
            }

            var sessionCoordinates = _transform.GetMapCoordinates(xform);
            var distanceVec = (coordinates.Position - sessionCoordinates.Position);
            var distance = distanceVec.Length();
            if (distance > range)
                continue;

            var direction = distanceVec.GetDir().ToString().ToUpperInvariant();
            var msg = distance < 1
                ? Loc.GetString(warningAbove)
                : Loc.GetString(warning, ("direction", direction));
            _popup.PopupEntity(msg, recipient, recipient, PopupType.LargeCaution);

            if (chat)
            {
                msg = $"[bold][font size=24][color=red]\n{msg}\n[/color][/font][/bold]";
                _rmcChat.ChatMessageToOne(ChatChannel.Radio, msg, msg, default, false, session.Channel);
            }
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var shells = EntityQueryEnumerator<ActiveMortarShellComponent>();
        while (shells.MoveNext(out var uid, out var active))
        {
            if (active.IsGuided && active.TargetEntity != null && HasActiveCommunicationTower(uid))
            {
                if (TryComp<SpottedComponent>(active.TargetEntity, out _) &&
                    _transformQuery.TryComp(active.TargetEntity, out var targetTransform))
                {
                    var targetCoords = _transform.GetMoverCoordinates(active.TargetEntity.Value, targetTransform);

                    var mortarCoords = _transform.ToMapCoordinates(active.Mortar.ToCoordinates());
                    var dist = (targetCoords.Position - mortarCoords.Position).Length();
                    if (dist <= 80 && _area.CanMortarFire(targetCoords))
                    {
                        if (time >= active.LastUpdate + TimeSpan.FromSeconds(0.5))
                        {
                            active.LastUpdate = time;
                            active.Coordinates = targetCoords;
                        }
                    }
                }
            }

            if (!active.Warned && time >= active.WarnAt)
            {
                active.Warned = true;
                var coordinates = _transform.ToMapCoordinates(active.Coordinates);
                PopupWarning(coordinates,
                    active.WarnRange,
                    "rmc-mortar-shell-warning",
                    "rmc-mortar-shell-warning-above");
                _audio.PlayPvs(active.WarnSound, active.Coordinates);
            }

            if (!active.ImpactWarned && time >= active.ImpactWarnAt)
            {
                active.ImpactWarned = true;
                PopupWarning(_transform.ToMapCoordinates(active.Coordinates),
                             active.WarnRange,
                             "rmc-mortar-shell-impact-warning",
                             "rmc-mortar-shell-impact-warning-above");
            }

            if (time >= active.GuidedWarn + TimeSpan.FromSeconds(2.5))
            {
                active.GuidedWarn = time;
                PopupWarning(_transform.ToMapCoordinates(active.Coordinates),
                             10f,
                             "rmc-mortar-shell-proximity-warning",
                             "rmc-mortar-shell-proximity-warning-above");
            }

            if (time >= active.LandAt)
            {
                _transform.SetCoordinates(uid, active.Coordinates);

                var ev = new MortarShellLandEvent(active.Coordinates);
                RaiseLocalEvent(uid, ref ev);

                _rmcExplosion.TriggerExplosive(uid);

                if (!EntityManager.IsQueuedForDeletion(uid))
                    QueueDel(uid);
            }
        }

        var updated = false;
        foreach (var target in _guidedTargets.ToList())
        {
            var ent = GetEntity(target.Entity);
            if (ent == null || !HasComp<SpottedComponent>(ent))
                continue;

            if (!HasActiveCommunicationTower(ent))
                continue;

            if (_transformQuery.TryComp(ent, out var xform))
            {
                var mapCoords = _transform.GetMapCoordinates(xform);
                if (!_area.CanMortarFire(_transform.ToCoordinates(mapCoords)))
                    continue;

                if (_rmcPlanet.TryGetOffset(mapCoords, out var offset))
                    mapCoords = mapCoords.Offset(offset);

                var netCoords = GetNetCoordinates(_transform.ToCoordinates(mapCoords));
                if (!target.Coords.Equals(netCoords))
                {
                    _guidedTargets.Remove(target);
                    _guidedTargets.Add(new MortarTargetInfo(target.Entity, target.Name, netCoords));
                    // mortar.Comp.Target = new Vector2i((int)netCoords.X, (int)netCoords.Y); как то реализовать это
                    updated = true;
                }
            }
        }

        if (updated)
            RefreshAllMortarBUIs(_guidedTargets.ToList());
    }

    private void OnGuidedTargetInit<T>(Entity<T> target, ref MapInitEvent args) where T : IComponent
    {
        if (_net.IsClient)
            return;

        var uid = target.Owner;
        var xform = Comp<TransformComponent>(uid);

        var coords = _transform.GetMapCoordinates(xform);
        var name = Comp<MetaDataComponent>(uid).EntityName ?? "Target";

        if (!HasActiveCommunicationTower(uid) || !_area.CanMortarFire(_transform.ToCoordinates(coords)))
            return;

        if (_rmcPlanet.TryGetOffset(coords, out var offset))
            coords = coords.Offset(offset);

        var netCoords = GetNetCoordinates(_transform.ToCoordinates(coords));

        _guidedTargets.Add(new MortarTargetInfo(GetNetEntity(uid), name, netCoords));

        RefreshAllMortarBUIs(_guidedTargets.ToList());
    }

    private void OnGuidedTargetShutdown<T>(Entity<T> target, ref ComponentShutdown args) where T : IComponent
    {
        if (_net.IsClient)
            return;

        var netEntity = GetNetEntity(target.Owner);

        _guidedTargets.RemoveWhere(t => t.Entity == netEntity);

        RefreshAllMortarBUIs(_guidedTargets.ToList());
    }

    public void RefreshAllMortarBUIs(List<MortarTargetInfo> targets)
    {
        var query = EntityQueryEnumerator<MortarComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var mortar, out var ui))
        {
            NetEntity? locked = null;
            float? flight = null;

            if (mortar.LockedEntityTarget != null)
                locked = GetNetEntity(mortar.LockedEntityTarget.Value);

            if (mortar.ProjectileFlightTime != TimeSpan.Zero)
                flight = (float)mortar.ProjectileFlightTime.TotalSeconds;

            var state = new MortarState(targets, locked, flight);
            _ui.SetUiState(uid, MortarUiKey.Key, state);
        }
    }

    public bool HasActiveCommunicationTower(EntityUid target)
    {
        var targetGrid = _transform.GetGrid(target);

        var towers = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();
        while (towers.MoveNext(out var towerUid, out var tower, out var _))
        {
            var towerGrid = _transform.GetGrid(towerUid);

            if (towerGrid == targetGrid && tower.State == CommunicationsTowerState.On)
                return true;
        }

        return false;
    }

    public bool HasActiveGuidedShells(Entity<MortarComponent> mortar, [NotNullWhen(true)] out EntityUid? activeGuidedShell)
    {
        activeGuidedShell = null;

        var shells = EntityQueryEnumerator<ActiveMortarShellComponent, TransformComponent>();
        while (shells.MoveNext(out var shellUid, out var shell, out _))
        {
            if (shell.Mortar != mortar.Owner)
                continue;

            activeGuidedShell = shellUid;
            return true;
        }

        return false;
    }
}