using Content.Shared._Stories.VehicleElevator.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.GameTicking;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using System.Numerics;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsRailingMode;
using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Shared._Stories.VehicleElevator;

public abstract class SharedVehicleElevatorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleElevatorComponent, StepTriggerAttemptEvent>(OnElevatorStepTriggerAttempt);

        SubscribeLocalEvent<VehicleElevatorRailingComponent, MapInitEvent>(OnRailingMapInit);
    }

    private void OnElevatorStepTriggerAttempt(Entity<VehicleElevatorComponent> elevator, ref StepTriggerAttemptEvent args)
    {
        if (elevator.Comp.Mode == RequisitionsElevatorMode.Raised)
            args.Cancelled = true;
    }

    private void OnRailingMapInit(Entity<VehicleElevatorRailingComponent> railing, ref MapInitEvent args)
    {
        UpdateRailing(railing);
    }

    private void UpdateRailing(Entity<VehicleElevatorRailingComponent> railing)
    {
        if (!TryComp(railing, out FixturesComponent? fixtures) ||
            _fixtures.GetFixtureOrNull(railing, railing.Comp.Fixture, fixtures) is not { } fixture)
        {
            return;
        }

        var hard = railing.Comp.Mode is Raising or Raised;
        _physics.SetHard(railing, fixture, hard);

        if (hard)
            EnsureComp<ClimbableComponent>(railing);
        else
            RemCompDeferred<ClimbableComponent>(railing);
    }

    protected void SetRailingMode(Entity<VehicleElevatorRailingComponent> railing, RequisitionsRailingMode mode)
    {
        if (railing.Comp.Mode == mode)
            return;

        railing.Comp.Mode = mode;
        Dirty(railing);

        UpdateRailing(railing);
    }

    protected void SendUIStateAll()
    {
        var query = EntityQueryEnumerator<VehicleElevatorComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            SendUIState((uid, computer));
        }
    }

    protected void SendUIState(Entity<VehicleElevatorComputerComponent> computer)
    {
        var elevator = GetElevator(computer);
        var mode = elevator?.Comp.NextMode ?? elevator?.Comp.Mode;
        var busy = elevator?.Comp.Busy ?? false;
        var hasOrder = elevator?.Comp.CurrentOrder != null;

        var state = new VehicleElevatorBuiState(mode, busy, hasOrder, computer.Comp.IsActive);
        _ui.SetUiState(computer.Owner, VehicleElevatorUIKey.Key, state);
    }

    protected Entity<VehicleElevatorComponent>? GetElevator(Entity<VehicleElevatorComputerComponent> computer)
    {
        var elevators = new List<Entity<VehicleElevatorComponent, TransformComponent>>();
        var query = EntityQueryEnumerator<VehicleElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var xform))
        {
            elevators.Add((uid, elevator, xform));
        }

        if (elevators.Count == 0)
            return null;

        if (elevators.Count == 1)
            return elevators[0];

        var computerCoords = _transform.GetMapCoordinates(computer);
        Entity<VehicleElevatorComponent>? closest = null;
        var closestDistance = float.MaxValue;
        foreach (var (uid, elevator, xform) in elevators)
        {
            var elevatorCoords = _transform.GetMapCoordinates(uid, xform);
            if (computerCoords.MapId != elevatorCoords.MapId)
                continue;

            var distance = (elevatorCoords.Position - computerCoords.Position).LengthSquared();
            if (closestDistance > distance)
            {
                closestDistance = distance;
                closest = (uid, elevator);
            }
        }

        if (closest == null)
            return elevators[0];

        return closest;
    }
}
