using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Access.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Nuke;

public sealed class SharedSTNukeSystem : EntitySystem
{
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;

    private EntityQuery<CommunicationsTowerComponent> _towerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        _towerQuery = GetEntityQuery<CommunicationsTowerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<STNukeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<STNukeComponent, BeforeActivatableUIOpenEvent>(OnBeforeUI);
        SubscribeLocalEvent<STNukeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<STNukeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<STNukeComponent, STNukeDefuseDoAfterEvent>(OnDefuseComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeAnchorDoAfterEvent>(OnAnchorDoAfterComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeSafetyDoAfterEvent>(OnSafetyDoAfterComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeEncryptionDoAfterEvent>(OnEncryptionDoAfterComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeXenoResinDoAfterEvent>(OnXenoResinDoAfterComplete);

        Subs.BuiEvents<STNukeComponent>(STNukeUiKey.Key, subs =>
        {
            subs.Event<STNukeToggleAnchorMessage>(OnAnchorButtonPressed);
            subs.Event<STNukeToggleSafetyMessage>(OnSafetyButtonPressed);
            subs.Event<STNukeToggleCommandLockoutMessage>(OnCommandLockoutPressed);
            subs.Event<STNukeToggleEncryptionMessage>(OnToggleEncryption);
            subs.Event<STNukeToggleMessage>(OnToggleNuke);
        });
    }

    private void OnMapInit(Entity<STNukeComponent> ent, ref MapInitEvent args)
    {
        LinkTowers(ent);
        UpdateUserInterface(ent);
    }

    private void LinkTowers(Entity<STNukeComponent> ent)
    {
        var xform = _xformQuery.GetComponent(ent);
        var query = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();

        ent.Comp.LinkedTowers.Clear();

        while (query.MoveNext(out var uid, out var tower, out var towerXform))
        {
            if (towerXform.GridUid == xform.GridUid)
            {
                ent.Comp.LinkedTowers.Add(uid);
            }
        }
    }

    private void OnBeforeUI(Entity<STNukeComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void OnInteractUsing(Entity<STNukeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tools.HasQuality(args.Used, "Cutting"))
            return;

        if (!ent.Comp.Active || !ent.Comp.ExplodeOn.HasValue)
            return;

        args.Handled = true;

        _popup.PopupPredicted(
            Loc.GetString("st-nuke-defusing"),
            ent,
            args.User,
            PopupType.Medium
        );

        var delay = TimeSpan.FromSeconds(15) * _skills.GetSkillDelayMultiplier(args.User, ent.Comp.DefuseSkill);
        _tools.UseTool(
            args.Used,
            args.User,
            ent,
            (float)delay.TotalSeconds,
            new[] { "Cutting" },
            new STNukeDefuseDoAfterEvent(),
            0f
        );
    }

    private void OnInteractHand(Entity<STNukeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<XenoEvolutionGranterComponent>(args.User))
            return;

        if (!ent.Comp.Decryption || !ent.Comp.DecryptionOn.HasValue)
            return;

        args.Handled = true;

        _popup.PopupPredicted(
            Loc.GetString("st-nuke-xeno-resin-start", ("user", args.User)),
            ent,
            null,
            PopupType.Medium
        );

        _popup.PopupPredicted(
            Loc.GetString("st-nuke-xeno-resin-user"),
            ent,
            args.User,
            PopupType.MediumCaution
        );

        var ev = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new STNukeXenoResinDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(ev);
    }

    private void OnXenoResinDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeXenoResinDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.Decryption = false;
        ent.Comp.DecryptionOn = null;
        ent.Comp.DecryptionTime = TimeSpan.FromMinutes(10);
        ent.Comp.TowersWereOffline = false;

        _popup.PopupPredicted(
            Loc.GetString("st-nuke-xeno-resin-complete"),
            ent,
            null,
            PopupType.Large
        );

        AnnounceDecryptionHalted(ent);

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnDefuseComplete(Entity<STNukeComponent> ent, ref STNukeDefuseDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        Disable(ent);
        _popup.PopupPredicted(
            Loc.GetString("st-nuke-defused"),
            ent,
            null,
            PopupType.Large
        );
    }

    private void OnAnchorButtonPressed(Entity<STNukeComponent> ent, ref STNukeToggleAnchorMessage args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Active)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-disengage-first"), args.Actor);
            return;
        }

        if (ent.Comp.Decryption)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-stop-decrypting"), args.Actor);
            return;
        }

        if (!_area.CanBuildSpecial(ent.Owner.ToCoordinates()) || !_rmcPlanet.IsOnPlanet(ent.Owner.ToCoordinates()))
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-cannot-deploy-here"), args.Actor);
            UpdateUserInterface(ent);
            return;
        }

        UpdateUserInterface(ent);

        var ev = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(5), new STNukeAnchorDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfter.TryStartDoAfter(ev);
    }

    private void OnAnchorDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeAnchorDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            UpdateUserInterface(ent);
            return;
        }

        var xform = Transform(ent);

        if (xform.Anchored)
        {
            _transform.Unanchor(ent, xform);
            _popup.PopupPredicted(Loc.GetString("st-nuke-unanchored"), ent, args.User, PopupType.Medium);
        }
        else
        {
            if (!_area.CanBuildSpecial(xform.Coordinates))
            {
                _popup.PopupPredictedCursor(Loc.GetString("st-nuke-cannot-deploy-here"), args.User);
                UpdateUserInterface(ent);
                return;
            }

            _transform.SetCoordinates(ent, xform, xform.Coordinates.SnapToGrid());
            _transform.AnchorEntity(ent, xform);
            _popup.PopupPredicted(Loc.GetString("st-nuke-anchored"), ent, args.User, PopupType.Medium);
        }

        if (ent.Comp.LinkedTowers.Count < 2)
            LinkTowers(ent);

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnSafetyButtonPressed(Entity<STNukeComponent> ent, ref STNukeToggleSafetyMessage args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Active)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-disengage-first"), args.Actor);
            return;
        }

        if (!_area.CanBuildSpecial(Transform(ent).Coordinates))
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-cannot-deploy-here"), args.Actor);
            return;
        }

        if (ent.Comp.Decryption)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-stop-decrypting"), args.Actor);
            return;
        }

        UpdateUserInterface(ent);

        var ev = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(5), new STNukeSafetyDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfter.TryStartDoAfter(ev);
    }

    private void OnSafetyDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeSafetyDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            UpdateUserInterface(ent);
            return;
        }

        ent.Comp.Safety = !ent.Comp.Safety;

        _popup.PopupPredicted(
            ent.Comp.Safety
                ? Loc.GetString("st-nuke-safety-enabled")
                : Loc.GetString("st-nuke-safety-disabled"),
            ent,
            args.User,
            PopupType.Medium
        );

        if (ent.Comp.Safety)
        {
            ent.Comp.Active = false;
            ent.Comp.Decryption = false;
            ent.Comp.DecryptionComplete = false;
            ent.Comp.TowersWereOffline = false;
        }

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnCommandLockoutPressed(Entity<STNukeComponent> ent, ref STNukeToggleCommandLockoutMessage args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.CommandLockout = !ent.Comp.CommandLockout;

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnToggleEncryption(Entity<STNukeComponent> ent, ref STNukeToggleEncryptionMessage args)
    {
        if (_net.IsClient)
            return;

        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        if (!xform.Anchored)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-anchor-first"), args.Actor, PopupType.Medium);
            return;
        }

        if (ent.Comp.Safety)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-safety-on"), args.Actor, PopupType.Medium);
            return;
        }

        if (!CheckTelecommsTowers(ent))
        {
            _popup.PopupCursor(
                Loc.GetString("st-nuke-towers-offline"),
                args.Actor,
                PopupType.LargeCaution
            );
            return;
        }

        UpdateUserInterface(ent);

        var ev = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(5), new STNukeEncryptionDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfter.TryStartDoAfter(ev);
    }

    private void OnEncryptionDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeEncryptionDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            UpdateUserInterface(ent);
            return;
        }

        ent.Comp.Decryption = !ent.Comp.Decryption;

        if (ent.Comp.Decryption)
        {
            ent.Comp.DecryptionOn = _timing.CurTime + ent.Comp.DecryptionTime;
            ent.Comp.AnnouncedHalfway = false;
            ent.Comp.AnnouncedOneMinute = false;
            ent.Comp.TowersWereOffline = false;

            AnnounceDecryptionStart(ent);
        }
        else
        {
            if (ent.Comp.DecryptionOn.HasValue)
            {
                var remaining = ent.Comp.DecryptionOn.Value - _timing.CurTime;
                var newTime = remaining + ent.Comp.PenaltionTime;

                ent.Comp.DecryptionTime = newTime > TimeSpan.FromMinutes(10)
                    ? TimeSpan.FromMinutes(10)
                    : newTime;
            }

            ent.Comp.DecryptionOn = null;
            ent.Comp.TowersWereOffline = false;

            AnnounceDecryptionHalted(ent);
        }

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnToggleNuke(Entity<STNukeComponent> ent, ref STNukeToggleMessage args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Active)
        {
            if (ent.Comp.DecryptionOn == null)
            {
                _popup.PopupCursor(
                    Loc.GetString("st-nuke-impossible-disengage"),
                    args.Actor,
                    PopupType.LargeCaution
                );
                return;
            }

            Disable(ent);
            return;
        }

        if (!ent.Comp.DecryptionComplete)
        {
            _popup.PopupCursor(
                Loc.GetString("st-nuke-decryption-not-complete"),
                args.Actor,
                PopupType.LargeCaution
            );
            return;
        }

        ent.Comp.Active = true;
        ent.Comp.ExplodeOn = _timing.CurTime + ent.Comp.DetonationTime;
        Dirty(ent);

        AnnounceActivated(ent);
        UpdateUserInterface(ent);
    }

    private bool CheckTelecommsTowers(Entity<STNukeComponent> ent)
    {
        var activeTowers = 0;

        foreach (var tower in ent.Comp.LinkedTowers)
        {
            if (!_towerQuery.TryGetComponent(tower, out var towerComp))
                continue;

            if (towerComp.State == CommunicationsTowerState.On)
                activeTowers++;
        }

        return activeTowers >= ent.Comp.RequiredTowers;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<STNukeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = new Entity<STNukeComponent>(uid, comp);

            if (comp.Decryption && comp.DecryptionOn.HasValue)
            {
                UpdateDecryption(ent);
                UpdateTowerCheck(ent);
            }

            if (comp.Active && comp.ExplodeOn.HasValue)
            {
                UpdateExplosion(ent);
            }

            if (comp.ExplodeStage1At.HasValue && !comp.ExplodeSoundPlayed)
            {
                if (_timing.CurTime >= comp.ExplodeStage1At.Value)
                {
                    _audio.PlayGlobal(
                        ent.Comp.NukeSound,
                        Filter.Broadcast(),
                        true
                    );
                    comp.ExplodeSoundPlayed = true;
                    Dirty(ent);

                    Spawn(comp.Explosion, uid.ToCoordinates());
                }
            }

            if (comp.ExplodeStage2At.HasValue && comp.ExplodeSoundPlayed && !comp.Nuked)
            {
                if (_timing.CurTime >= comp.ExplodeStage2At.Value)
                {
                    Nuke(ent);
                    comp.Nuked = true;

                    PredictedQueueDel(ent.Owner);
                }
            }
        }
    }

    private void Nuke(Entity<STNukeComponent> ent)
    {
        var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out var _, out var xform))
        {
            if (!_rmcPlanet.IsOnPlanet(uid.ToCoordinates()))
                continue;

            var coordinates = xform.Coordinates;
            Spawn("Ash", coordinates);
            QueueDel(uid);
        }

        var cameraQuery = EntityQueryEnumerator<RMCCameraComponent, TransformComponent>();
        while (cameraQuery.MoveNext(out var uid, out var _, out var _))
        {
            if (!_rmcPlanet.IsOnPlanet(uid.ToCoordinates()))
                continue;

            QueueDel(uid);

        }

        var dropshipDestQuery = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (dropshipDestQuery.MoveNext(out var uid, out var _, out var _))
        {
            if (!_rmcPlanet.IsOnPlanet(uid.ToCoordinates()))
                continue;

            QueueDel(uid);
        }

        var distressQuery = EntityQueryEnumerator<CMDistressSignalRuleComponent, ActiveGameRuleComponent>();
        while (distressQuery.MoveNext(out var uid, out var comp, out var _))
        {
            comp.Nuked = true;
            Dirty(uid, comp);
        }
    }

    private void UpdateTowerCheck(Entity<STNukeComponent> ent)
    {
        if (!ent.Comp.LastTowerCheck.HasValue ||
            _timing.CurTime - ent.Comp.LastTowerCheck.Value >= ent.Comp.TowerCheckInterval)
        {
            ent.Comp.LastTowerCheck = _timing.CurTime;

            var towersOnline = CheckTelecommsTowers(ent);

            if (!towersOnline && !ent.Comp.TowersWereOffline)
            {
                ent.Comp.TowersWereOffline = true;

                if (ent.Comp.DecryptionOn.HasValue)
                {
                    var newTime = ent.Comp.DecryptionOn.Value + ent.Comp.PenaltionTime;

                    var maxTime = _timing.CurTime + TimeSpan.FromMinutes(10);
                    ent.Comp.DecryptionOn = newTime > maxTime ? maxTime : newTime;
                    ent.Comp.Decryption = false;

                    _popup.PopupPredicted(
                        Loc.GetString("st-nuke-towers-offline-penalty"),
                        ent,
                        null,
                        PopupType.LargeCaution
                    );

                    Dirty(ent);
                }
            }
            else if (towersOnline && ent.Comp.TowersWereOffline)
            {
                ent.Comp.TowersWereOffline = false;
            }
        }
    }

    private void UpdateDecryption(Entity<STNukeComponent> ent)
    {
        if (!ent.Comp.DecryptionOn.HasValue)
            return;

        var remaining = ent.Comp.DecryptionOn.Value - _timing.CurTime;
        if (remaining <= TimeSpan.FromMinutes(5) && remaining > TimeSpan.Zero && !ent.Comp.AnnouncedHalfway)
        {
            AnnounceDecryptionHalfway(ent);
            ent.Comp.AnnouncedHalfway = true;
            Dirty(ent);
        }

        if (remaining <= TimeSpan.FromMinutes(1) && remaining > TimeSpan.Zero && !ent.Comp.AnnouncedOneMinute)
        {
            AnnounceDecryptionOneMinute(ent);
            ent.Comp.AnnouncedOneMinute = true;
            Dirty(ent);
        }

        if (_timing.CurTime >= ent.Comp.DecryptionOn.Value)
        {
            CompleteDecryption(ent);
        }

        UpdateUserInterface(ent);
    }

    private void CompleteDecryption(Entity<STNukeComponent> ent)
    {
        ent.Comp.Decryption = false;
        ent.Comp.DecryptionOn = null;
        ent.Comp.DecryptionComplete = true;
        ent.Comp.TowersWereOffline = false;

        Dirty(ent);
        _popup.PopupPredicted(
            Loc.GetString("st-nuke-decryption-complete"),
            ent,
            null,
            PopupType.Large
        );

        AnnounceDecryptionCompleted(ent);
        UpdateUserInterface(ent);
    }

    private void UpdateExplosion(Entity<STNukeComponent> ent)
    {
        if (!ent.Comp.ExplodeOn.HasValue)
            return;

        if (ent.Comp.Exploded)
            return;

        if (_timing.CurTime >= ent.Comp.ExplodeOn.Value)
        {
            Explode(ent);
            ent.Comp.Exploded = true;
            Dirty(ent);
        }

        UpdateUserInterface(ent);
    }

    private void Explode(Entity<STNukeComponent> ent)
    {
        var marineQuery = EntityQueryEnumerator<MarineComponent, ActorComponent>();
        while (marineQuery.MoveNext(out var uid, out _, out _))
        {
            _eye.SetTarget(uid, ent.Owner);
        }

        var xenoQuery = EntityQueryEnumerator<XenoComponent, ActorComponent>();
        while (xenoQuery.MoveNext(out var uid, out _, out _))
        {
            _eye.SetTarget(uid, ent.Owner);
        }

        _audio.PlayGlobal(ent.Comp.BeforeNukeSound, Filter.Broadcast(), true);

        var length = TimeSpan.FromSeconds(16);
        ent.Comp.ExplodeStage1At = _timing.CurTime + length;
        ent.Comp.ExplodeStage2At = _timing.CurTime + length + TimeSpan.FromSeconds(1);
        ent.Comp.ExplodeSoundPlayed = false;
        ent.Comp.Nuked = false;

        Dirty(ent);
    }

    public void Disable(Entity<STNukeComponent> ent)
    {
        ent.Comp.Active = false;
        ent.Comp.ExplodeOn = null;
        ent.Comp.DecryptionOn = null;
        ent.Comp.Decryption = false;
        ent.Comp.DecryptionComplete = false;
        ent.Comp.DecryptionTime = TimeSpan.FromMinutes(10);
        ent.Comp.TowersWereOffline = false;

        AnnounceDeactivated(ent);

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<STNukeComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, STNukeUiKey.Key))
            return;

        var xform = Transform(ent);
       var anchored = xform.Anchored;

        var allowed = true;
        var decryptionTime = "00:00";
        if (ent.Comp.Decryption && ent.Comp.DecryptionOn.HasValue)
        {
            var remaining = ent.Comp.DecryptionOn.Value - _timing.CurTime;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            decryptionTime = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
        }

        var timeLeft = "00:00";
        if (ent.Comp.Active && ent.Comp.ExplodeOn.HasValue)
        {
            var remaining = ent.Comp.ExplodeOn.Value - _timing.CurTime;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            timeLeft = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
        }

        var state = new STNukeBuiState(
            anchor: anchored,
            safety: ent.Comp.Safety,
            timing: ent.Comp.Active,
            timeLeft: timeLeft,
            commandLockout: ent.Comp.CommandLockout,
            allowed: allowed,
            decryptionComplete: ent.Comp.DecryptionComplete,
            decrypting: ent.Comp.Decryption,
            decryptionTime: decryptionTime,
            canDisengage: ent.Comp.DecryptionOn.HasValue || !ent.Comp.Active
        );

        _ui.SetUiState(ent.Owner, STNukeUiKey.Key, state);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<STNukeComponent> ent)
    {
        var xform = Transform(ent);

        _appearance.SetData(ent, STNukeVisuals.Deployed, xform.Anchored);
        _appearance.SetData(ent, STNukeVisuals.Unsafe, !ent.Comp.Safety);
        _appearance.SetData(ent, STNukeVisuals.Timing, ent.Comp.DecryptionComplete);
        _appearance.SetData(ent, STNukeVisuals.Activation, ent.Comp.Active);
    }

    private void AnnounceDecryptionStart(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var areaName = "Unknown";
        if (_area.TryGetArea(ent, out _, out var areaProto))
            areaName = areaProto.Name;

        var timeLeft = $"{(int)ent.Comp.DecryptionTime.TotalMinutes}:{ent.Comp.DecryptionTime.Seconds:D2}";

        var marineMsg = Loc.GetString("st-nuke-decryption-started-marine",
            ("time", timeLeft));
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-decryption-started-xeno",
            ("area", areaName),
            ("time", timeLeft));
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }

    private void AnnounceDecryptionHalfway(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var timeLeft = "5:00";

        var marineMsg = Loc.GetString("st-nuke-decryption-halfway-marine", ("time", timeLeft));
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-decryption-halfway-xeno");
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }

    private void AnnounceDecryptionOneMinute(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var marineMsg = Loc.GetString("st-nuke-decryption-one-minute-marine");
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-decryption-one-minute-xeno");
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }

    private void AnnounceDecryptionCompleted(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var marineMsg = Loc.GetString("st-nuke-decryption-completed-marine");
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-decryption-completed-xeno");
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }

    private void AnnounceDecryptionHalted(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var marineMsg = Loc.GetString("st-nuke-decryption-halted-marine");
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-decryption-halted-xeno");
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }

    private void AnnounceActivated(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var areaName = Loc.GetString("generic-unknown-title");
        if (_area.TryGetArea(ent, out _, out var areaProto))
            areaName = areaProto.Name;

        var timeLeft = $"{(int)ent.Comp.DetonationTime.TotalMinutes}:{ent.Comp.DetonationTime.Seconds:D2}";

        var marineMsg = Loc.GetString("st-nuke-activated-marine", ("time", timeLeft));
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-activated-xeno", ("area", areaName));
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }

    private void AnnounceDeactivated(Entity<STNukeComponent> ent)
    {
        if (_net.IsClient)
            return;

        var marineMsg = Loc.GetString("st-nuke-deactivated-marine");
        _marineAnnounce.AnnounceARES(null,
            marineMsg,
            new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg")
        );

        var xenoMsg = Loc.GetString("st-nuke-deactivated-xeno");
        _xenoAnnounce.AnnounceAll(
            ent,
            _xenoAnnounce.WrapHive(xenoMsg),
            new SoundCollectionSpecifier("XenoQueenCommand")
        );
    }
}
