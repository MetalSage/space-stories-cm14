using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Localizations;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Hunter.Equipment;

[ByRefEvent]
public record struct HunterTrackGearEvent(EntityUid User, EntityUid Bracer);

public sealed class HunterGearTrackingSystem : EntitySystem
{
    [Dependency]
    private readonly MetaDataSystem _metaData = default!;

    [Dependency]
    private readonly INetManager _net = default!;

    [Dependency]
    private readonly SharedPopupSystem _popup = default!;

    [Dependency]
    private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterBracerComponent, HunterTrackGearEvent>(OnTrackGear);
    }

    private void OnTrackGear(Entity<HunterBracerComponent> bracer, ref HunterTrackGearEvent args)
    {
        if (_net.IsClient)
            return;

        var user = args.User;
        var userXform = Transform(user);
        var userMap = userXform.MapID;
        var userPos = _transform.GetWorldPosition(userXform);

        EntityUid? bestLocalItem = null;
        var bestLocalDist = float.MaxValue;

        EntityUid? bestRemoteItem = null;

        void ProcessCandidate(EntityUid uid, TransformComponent xform)
        {
            if (xform.MapID == MapId.Nullspace || !xform.MapUid.HasValue)
                return;

            var targetMapUid = xform.MapUid.Value;

            if (!HasComp<RMCPlanetComponent>(targetMapUid) && !HasComp<AlmayerComponent>(targetMapUid))
                return;

            if (xform.MapID == userMap)
            {
                var dist = (userPos - _transform.GetWorldPosition(xform)).Length();
                if (dist < bestLocalDist)
                {
                    bestLocalDist = dist;
                    bestLocalItem = uid;
                }
            }
            else
                bestRemoteItem ??= uid;
        }

        var gearQuery = EntityQueryEnumerator<HunterGearTrackableComponent, TransformComponent>();
        while (gearQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (IsOwnedByLivingHunter(uid))
                continue;

            ProcessCandidate(uid, xform);
        }

        var deadHunterQuery = EntityQueryEnumerator<HunterComponent, MobStateComponent, TransformComponent>();
        while (deadHunterQuery.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (uid == user)
                continue;

            if (mobState.CurrentState != MobState.Dead)
                continue;

            ProcessCandidate(uid, xform);
        }

        if (bestLocalItem != null)
        {
            var targetName = Name(bestLocalItem.Value);
            var targetXform = Transform(bestLocalItem.Value);
            var targetPos = _transform.GetWorldPosition(targetXform);

            var angle = (targetPos - userPos).ToWorldAngle();
            var direction = angle.GetCardinalDir();
            var dirText = ContentLocalizationManager.FormatDirection(direction);

            var msg = Loc.GetString(
                "st-hunter-tracker-found",
                ("target", targetName),
                ("distance", (int)bestLocalDist),
                ("direction", dirText)
            );

            _popup.PopupEntity(msg, user, user, PopupType.Medium);
        }
        else if (bestRemoteItem != null)
        {
            var targetName = Name(bestRemoteItem.Value);
            var targetXform = Transform(bestRemoteItem.Value);
            var mapName = "Unknown Location";

            if (targetXform.MapUid.HasValue)
                mapName = Name(targetXform.MapUid.Value);

            var msg = Loc.GetString("st-hunter-tracker-found-cross-map", ("target", targetName), ("location", mapName));

            _popup.PopupEntity(msg, user, user, PopupType.Medium);
        }
        else
            _popup.PopupEntity(Loc.GetString("st-hunter-tracker-none"), user, user, PopupType.MediumCaution);
    }

    private bool IsOwnedByLivingHunter(EntityUid item)
    {
        var current = item;
        for (var i = 0; i < 10; i++)
        {
            if (!current.IsValid())
                break;

            if (HasComp<HunterComponent>(current))
            {
                if (
                    TryComp<MobStateComponent>(current, out var mobState)
                    && mobState.CurrentState == MobState.Dead
                )
                    return false;
                return true;
            }

            if (!TryComp<TransformComponent>(current, out var xform))
                break;

            var parent = xform.ParentUid;
            if (!parent.IsValid() || parent == current)
                break;

            current = parent;
        }

        return false;
    }
}
