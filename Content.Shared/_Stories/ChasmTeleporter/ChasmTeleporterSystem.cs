using System.Linq;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Teleporter;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Coordinates;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.ChasmTeleporter;

public sealed class ChasmTeleporterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private EntityQuery<PhysicsComponent> _physQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChasmTeleporterComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ChasmTeleporterComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ChasmTeleporterFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<ChasmTeleporterFallingComponent, ComponentShutdown>(OnComponentShutdown);

        _physQuery = GetEntityQuery<PhysicsComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ChasmTeleporterFallingComponent>();
        while (query.MoveNext(out var uid, out var falling))
        {
            if (_timing.CurTime < falling.NextDeletionTime)
                continue;

            HandleTeleporting(falling.Chasm, uid);
        }
    }

    private void OnStepTriggered(Entity<ChasmTeleporterComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (_net.IsClient)
            return;

        if (HasComp<ChasmTeleporterFallingComponent>(args.Tripper))
            return;

        StartFalling(ent, args.Tripper);
    }

    private void OnStepTriggerAttempt(Entity<ChasmTeleporterComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnUpdateCanMove(Entity<ChasmTeleporterFallingComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnComponentShutdown(Entity<ChasmTeleporterFallingComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent.Owner);
    }

    public void StartFalling(Entity<ChasmTeleporterComponent> ent, EntityUid tripper)
    {
        if (_net.IsClient)
            return;

        var falling = EnsureComp<ChasmTeleporterFallingComponent>(tripper);
        falling.Chasm = ent;
        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(tripper);

        if (ent.Comp.PlaySound)
            _audio.PlayPredicted(ent.Comp.FallingSound, ent, tripper);
    }

    private void HandleTeleporting(EntityUid teleporter, EntityUid user)
    {
        RemCompDeferred<ChasmTeleporterFallingComponent>(user);

        if (!TryComp<ChasmTeleporterComponent>(teleporter, out var chasm))
            return;

        if (!TryFindTarget(chasm.TargetName, out var target) && TerminatingOrDeleted(target))
            return;

        var origin = _transform.GetMapCoordinates(target);
        if (origin.MapId == MapId.Nullspace)
            return;

        var xform = _transform.GetGrid(target);
        if (xform == null || !TryComp<MapGridComponent>(xform, out var grid))
            return;

        var radius = (int)chasm.Radius;
        var maxTries = radius * radius;
        var allCoordinates = new List<MapCoordinates>();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                allCoordinates.Add(new MapCoordinates(
                    origin.X + dx,
                    origin.Y + dy,
                    origin.MapId
                ));
            }
        }

        allCoordinates = allCoordinates.OrderBy(_ => _random.Next()).ToList();

        foreach (var coordinates in allCoordinates.Take(maxTries))
        {
            if (_rmcMap.IsTileBlocked(coordinates))
                continue;

            var tile = _mapSystem.GetTileRef(xform.Value, grid, coordinates);
            if (tile == null || tile.IsSpace())
                continue;

            var valid = true;
            var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(_transform.ToCoordinates(coordinates));

            while (anchored.MoveNext(out var ent) && valid)
            {
                if (_physQuery.TryGetComponent(ent, out var body) &&
                    body.BodyType == BodyType.Static &&
                    body.Hard &&
                    (body.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                {
                    valid = false;
                }
            }

            if (!valid)
                continue;

            _rmcPulling.TryStopAllPullsFromAndOn(user);
            _transform.SetMapCoordinates(user, coordinates);
            _stun.TryParalyze(user, chasm.ParalyzeTime, true);
            return;
        }
    }

    private bool TryFindTarget(string name, out EntityUid target)
    {
        var query = EntityQueryEnumerator<ChasmTeleporterTargetComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (string.IsNullOrEmpty(meta.EntityName))
                continue;

            if (meta.EntityName == name)
            {
                target = uid;
                return true;
            }
        }

        target = EntityUid.Invalid;
        return false;
    }
}
