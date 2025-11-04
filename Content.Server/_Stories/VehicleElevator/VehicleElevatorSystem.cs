using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._Stories.VehicleElevator;
using Content.Shared._Stories.VehicleElevator.Components;
using Content.Shared.Database;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Server._Stories.VehicleRequisitions;

public sealed partial class VehicleElevatorSystem : SharedVehicleElevatorSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleElevatorComputerComponent, MapInitEvent>(OnComputerMapInit);
        SubscribeLocalEvent<VehicleElevatorComputerComponent, BeforeActivatableUIOpenEvent>(OnComputerBeforeActivatableUIOpen);

        Subs.BuiEvents<VehicleElevatorComputerComponent>(VehicleElevatorUIKey.Key, subs =>
        {
            subs.Event<VehicleElevatorBuyMsg>(OnBuy);
            subs.Event<VehicleElevatorPlatformMsg>(OnPlatform);
        });
    }

    private void OnComputerMapInit(Entity<VehicleElevatorComputerComponent> computer, ref MapInitEvent args)
    {
        SendUIState(computer);
    }

    private void OnComputerBeforeActivatableUIOpen(Entity<VehicleElevatorComputerComponent> computer, ref BeforeActivatableUIOpenEvent args)
    {
        SendUIState(computer);
    }

    private void OnBuy(Entity<VehicleElevatorComputerComponent> computer, ref VehicleElevatorBuyMsg args)
    {
        var actor = args.Actor;

        if (!computer.Comp.IsActive)
            return;

        if (!computer.Comp.Orders.Contains(args.Order))
            return;

        if (GetElevator(computer) is not { } elevator)
            return;

        if (elevator.Comp.Mode != Lowered || elevator.Comp.Busy || elevator.Comp.CurrentOrder != null)
            return;

        elevator.Comp.CurrentOrder = args.Order;
        elevator.Comp.ToggledAt = _timing.CurTime;
        elevator.Comp.Busy = true;
        SetMode(elevator, Preparing, Raising);

        computer.Comp.IsActive = false;
        computer.Comp.UsedOnce = true;

        Dirty(elevator);
        Dirty(computer);
        SendUIStateAll();

        _adminLogs.Add(LogType.RMCRequisitionsBuy, $"{ToPrettyString(args.Actor):actor} ordered vehicle requisitions item {args.Order}");
    }

    private void OnPlatform(Entity<VehicleElevatorComputerComponent> computer, ref VehicleElevatorPlatformMsg args)
    {
        if (computer.Comp.UsedOnce)
            return;

        if (GetElevator(computer) is not { } elevator)
            return;

        var comp = elevator.Comp;
        if (comp.NextMode != null || comp.Busy)
            return;

        if (comp.Mode == Lowering || comp.Mode == Raising)
            return;

        if (args.Raise && comp.Mode == Raised)
            return;

        if (!args.Raise && comp.Mode == Lowered)
            return;

        RequisitionsElevatorMode? nextMode = comp.Mode switch
        {
            Lowered => Raising,
            Raised => Lowering,
            _ => null
        };

        if (nextMode == null)
            return;

        comp.ToggledAt = _timing.CurTime;
        comp.Busy = true;
        SetMode(elevator, Preparing, nextMode);
        Dirty(elevator);
    }

    private void UpdateRailings(Entity<VehicleElevatorComponent> elevator, RequisitionsRailingMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator);
        var railings = _lookup.GetEntitiesInRange<VehicleElevatorRailingComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            SetRailingMode(railing, mode);
        }
    }

    private void UpdateGears(Entity<VehicleElevatorComponent> elevator, RequisitionsGearMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator);
        var railings = _lookup.GetEntitiesInRange<VehicleElevatorGearComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            if (railing.Comp.Mode == mode)
                continue;

            railing.Comp.Mode = mode;
            Dirty(railing);
        }
    }

    private void TryPlayAudio(Entity<VehicleElevatorComponent> elevator)
    {
        var comp = elevator.Comp;
        if (comp.Audio != null)
            return;

        var time = _timing.CurTime;
        if (comp.NextMode == Lowering || comp.Mode == Lowering)
        {
            if (time < comp.ToggledAt + comp.LowerSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.LoweringSound, elevator)?.Entity;
            return;
        }

        if (comp.NextMode == Raising || comp.Mode == Raising)
        {
            if (time < comp.ToggledAt + comp.RaiseSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.RaisingSound, elevator)?.Entity;
        }
    }

    private void SetMode(Entity<VehicleElevatorComponent> elevator, RequisitionsElevatorMode mode, RequisitionsElevatorMode? nextMode)
    {
        elevator.Comp.Mode = mode;
        elevator.Comp.NextMode = nextMode;
        Dirty(elevator);

        RequisitionsGearMode? gearMode = mode switch
        {
            Lowered or Raised or Preparing => RequisitionsGearMode.Static,
            Lowering or Raising => RequisitionsGearMode.Moving,
            _ => null
        };

        if (gearMode != null)
            UpdateGears(elevator, gearMode.Value);

        RequisitionsRailingMode? railingMode = mode switch
        {
            Lowered => RequisitionsRailingMode.Raised,
            Raised => RequisitionsRailingMode.Lowered,
            _ => null
        };

        if (railingMode != null)
            UpdateRailings(elevator, railingMode.Value);

        SendUIStateAll();
    }

    private void SpawnOrder(Entity<VehicleElevatorComponent> elevator)
    {
        if (elevator.Comp.Mode == Raised && elevator.Comp.CurrentOrder != null)
        {
            var coordinates = _transform.GetMoverCoordinates(elevator);
            SpawnAtPosition(elevator.Comp.CurrentOrder.Value, coordinates);
            elevator.Comp.CurrentOrder = null;
            Dirty(elevator);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var updateUI = false;
        var elevators = EntityQueryEnumerator<VehicleElevatorComponent>();

        while (elevators.MoveNext(out var uid, out var elevator))
        {
            if (ProcessElevator((uid, elevator)))
                updateUI = true;

            if (elevator.RailingFinishAt is {} finish && time >= finish)
            {
                elevator.RailingFinishAt = null;
                SetMode((uid, elevator), elevator.Mode, elevator.NextMode);
                Dirty(uid, elevator);
                updateUI = true;
            }
        }

        if (updateUI)
            SendUIStateAll();
    }

    private bool ProcessElevator(Entity<VehicleElevatorComponent> ent)
    {
        var time = _timing.CurTime;
        var elevator = ent.Comp;

        if (elevator.ToggledAt == null)
            return false;

        if (time > elevator.ToggledAt + elevator.ToggleDelay)
        {
            elevator.ToggledAt = null;
            elevator.Busy = false;
            Dirty(ent);
            return true;
        }

        TryPlayAudio(ent);

        var delay = elevator.NextMode == Raising ? elevator.RaiseDelay : elevator.LowerDelay;
        if (elevator.Mode == Preparing &&
            elevator.NextMode != null &&
            time > elevator.ToggledAt + delay)
        {
            SetMode(ent, elevator.NextMode.Value, null);
            return false;
        }

        if (elevator.Mode != Lowering && elevator.Mode != Raising)
            return false;

        var startDelay = delay + elevator.NextMode switch
        {
            Lowering => elevator.LowerDelay,
            Raising => elevator.RaiseDelay,
            _ => TimeSpan.Zero,
        };

        var moveDelay = startDelay + elevator.Mode switch
        {
            Lowering => elevator.LowerDelay,
            Raising => elevator.RaiseDelay,
            _ => TimeSpan.Zero,
        };

        if (time > elevator.ToggledAt + moveDelay)
        {
            elevator.Audio = null;

            var mode = elevator.Mode switch
            {
                Raising => Raised,
                Lowering => Lowered,
                _ => elevator.Mode,
            };

            SetMode(ent, mode, elevator.NextMode);

            RequisitionsRailingMode? railingMode = mode switch
            {
                Raised => RequisitionsRailingMode.Lowering,
                Lowered => RequisitionsRailingMode.Raising,
                _ => null
            };

            if (railingMode != null)
            {
                UpdateRailings(ent, railingMode.Value);
                elevator.RailingFinishAt = time + elevator.RailingAnimDelay;
                Dirty(ent);
            }

            SpawnOrder(ent);
            return true;
        }

        return false;
    }
}
