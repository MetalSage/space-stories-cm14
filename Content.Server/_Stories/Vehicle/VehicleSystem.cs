using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Vehicle.Systems;
using Content.Shared._Stories.Attachables;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Server.Light.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Light.Components;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.MotionDetector;
using Content.Server.Chat.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Audio.Systems;
using Content.Shared._RMC14.Xenonids;

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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BallisticVehicleAmmoProviderComponent, VehicleGunReloadEvent>(OnReload);
        SubscribeLocalEvent<ActivateExpendableLightOnShootComponent, AmmoShotEvent>(ActivateExpendableLightOnShot);

        SubscribeLocalEvent<MotionDetectorComponent, AfterInteractEvent>(OnMotionDetectorInteract);
        SubscribeLocalEvent<MotionDetectorComponent, MotionDetectorScanDoAfterEvent>(OnMotionDetectorScanFinished);
    }

    private void OnReload(Entity<BallisticVehicleAmmoProviderComponent> provider, ref VehicleGunReloadEvent args)
    {
        if (!_attachable.TryGetHolder(provider.Owner, out var holder) ||
            holder is not { } apc)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        if (provider.Comp.Shots >= provider.Comp.Capacity)
            return;

        var magazine = TryMagazine(apc, provider.Comp);
        if (magazine == null)
            return;

        provider.Comp.Shots = provider.Comp.Capacity;

        QueueDel(magazine);

        Dirty(provider, provider.Comp);
    }

    private EntityUid? TryMagazine(EntityUid apc, BallisticVehicleAmmoProviderComponent comp)
    {
        _container.TryGetContainer(apc, comp.AmmoContainerId, out var apcContainer);

        if (apcContainer == null)
            return null;

        foreach (var magazine in apcContainer.ContainedEntities)
        {
            if (!TryComp<VehicleGunMagazineComponent>(magazine, out var magazineComp))
                continue;

            if (comp.Prototype != magazineComp.Prototype)
                continue;

            QueueDel(magazine);
            return magazine;
        }
        return null;
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
            _popup.PopupEntity($"The {ToPrettyString(md)} must be activated to scan {ToPrettyString(args.Target.Value)}.", md.Owner, args.User);
            return;
        }

        var selfMsg = $"You start recalibrating {ToPrettyString(md)} to scan the vehicle's interior for signatures.";
        var otherMsg = $"{ToPrettyString(args.User)} fumbles with {ToPrettyString(md)} aimed at {ToPrettyString(args.Target.Value)}.";
        _popup.PopupPredicted(selfMsg, otherMsg, md.Owner, args.User);

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 3f, new MotionDetectorScanDoAfterEvent(),
            md.Owner, target: args.Target, used: md.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            var selfStopMsg = $"You stop trying to scan {ToPrettyString(args.Target.Value)}'s interior.";
            var otherStopMsg = $"{ToPrettyString(args.User)} stops fumbling with {ToPrettyString(md)}.";
            _popup.PopupPredicted(selfStopMsg, otherStopMsg, md.Owner, args.User);
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
            _popup.PopupEntity($"The {ToPrettyString(md)} must be activated to scan {ToPrettyString(args.Target.Value)}.", md.Owner, args.User);
            return;
        }

        if (!TryComp<VehicleComponent>(args.Target, out var vehicleComp))
            return;

        var otherMsg = $"{ToPrettyString(args.User)} finishes fumbling with {ToPrettyString(md)}.";
        var selfMsg = $"You finish recalibrating {ToPrettyString(md)} and scanning {ToPrettyString(args.Target)}'s interior for signatures.";

        _popup.PopupPredicted(selfMsg, otherMsg, md.Owner, args.User);

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
            var msg = $"The {ToPrettyString(md)} shows " +
                      (humansInside > 0 ? $"approximately {humansInside} signatures" : "no signatures") +
                      (xenosInside > 0 ? $" and about {xenosInside} abnormal signatures" : "") +
                      $" inside of {ToPrettyString(args.Target.Value)}.";

            _audio.PlayPvs(md.Comp.ScanSound, args.User);
            _chatManager.TrySendInGameICMessage(md.Owner, msg, InGameICChatType.Speak, true);
        }
        else
        {
            _audio.PlayPvs(md.Comp.ScanEmptySound, args.User);
            _popup.PopupEntity($"The {ToPrettyString(md)} can't pick up any signatures, so the vehicle should be empty. In theory.", md.Owner, args.User);
        }
    }
}