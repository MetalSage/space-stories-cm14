using Content.Server.Chat.Systems;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Vehicle;

public sealed class VehicleSystem : EntitySystem
{
    [Dependency] private readonly VehicleAttachableHolderSystem _attachable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExpendableLightSystem _expendableLight = default!;
    [Dependency] private readonly ChatSystem _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivateExpendableLightOnShootComponent, AmmoShotEvent>(ActivateExpendableLightOnShot);
        SubscribeLocalEvent<MotionDetectorComponent, AfterInteractEvent>(OnMotionDetectorInteract);
        SubscribeLocalEvent<MotionDetectorComponent, MotionDetectorScanDoAfterEvent>(OnMotionDetectorScanFinished);
    }

    private void ActivateExpendableLightOnShot(Entity<ActivateExpendableLightOnShootComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<ExpendableLightComponent>(projectile, out var light))
                _expendableLight.TryActivate((projectile, light));
        }
    }

    private void OnMotionDetectorInteract(Entity<MotionDetectorComponent> md, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<VehicleComponent>(args.Target))
            return;

        if (!md.Comp.Enabled)
        {
            _popup.PopupEntity(
                Loc.GetString("st-motion-detector-scan-disabled", ("md", md), ("target", args.Target.Value)),
                md.Owner, args.User);
            return;
        }

        _popup.PopupPredicted(
            Loc.GetString("st-motion-detector-scan-start-self", ("md", md), ("target", args.Target.Value)),
            Loc.GetString("st-motion-detector-scan-start-others", ("user", args.User), ("md", md), ("target", args.Target.Value)),
            md.Owner, args.User);

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 3f, new MotionDetectorScanDoAfterEvent(),
            md.Owner, target: args.Target, used: md.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupPredicted(
                Loc.GetString("st-motion-detector-scan-stop-self", ("target", args.Target.Value)),
                Loc.GetString("st-motion-detector-scan-stop-others", ("user", args.User), ("md", md)),
                md.Owner, args.User);
            return;
        }

        args.Handled = true;
    }

    private void OnMotionDetectorScanFinished(Entity<MotionDetectorComponent> md, ref MotionDetectorScanDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        if (!md.Comp.Enabled)
        {
            _popup.PopupEntity(
                Loc.GetString("st-motion-detector-scan-disabled", ("md", md), ("target", args.Target.Value)),
                md.Owner, args.User);
            return;
        }

        if (!TryComp<VehicleComponent>(args.Target, out var vehicleComp))
            return;

        _popup.PopupPredicted(
            Loc.GetString("st-motion-detector-scan-finish-self", ("md", md), ("target", args.Target.Value)),
            Loc.GetString("st-motion-detector-scan-finish-others", ("user", args.User), ("md", md)),
            md.Owner, args.User);

        int humansInside = 0;
        int xenosInside = 0;

        var marineQuery = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (marineQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != vehicleComp.GridEnt)
                continue;
            humansInside++;
        }

        var xenoQuery = EntityQueryEnumerator<XenoComponent, TransformComponent>();
        while (xenoQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != vehicleComp.GridEnt)
                continue;
            xenosInside++;
        }

        if (humansInside > 0 || xenosInside > 0)
        {
            var msg = Loc.GetString("st-motion-detector-scan-result",
                ("md", md),
                ("target", args.Target.Value),
                ("humans", humansInside),
                ("xenos", xenosInside));

            _audio.PlayPvs(md.Comp.ScanSound, args.User);
            _chatManager.TrySendInGameICMessage(md.Owner, msg, InGameICChatType.Speak, true);
        }
        else
        {
            _audio.PlayPvs(md.Comp.ScanEmptySound, args.User);
            _popup.PopupEntity(
                Loc.GetString("st-motion-detector-scan-empty", ("md", md), ("target", args.Target.Value)),
                md.Owner, args.User);
        }
    }
}
