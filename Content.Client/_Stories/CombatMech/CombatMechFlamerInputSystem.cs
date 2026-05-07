using Content.Client._RMC14.Movement;
using Content.Client.CombatMode;
using Content.Client.Gameplay;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._Stories.CombatMech;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Containers;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Stories.CombatMech;

public sealed class CombatMechFlamerInputSystem : EntitySystem
{
    private TimeSpan _nextUnderbarrelShootAt;

    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RMCLagCompensationSystem _rmcLag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        // Combat mode checked here (client) and mirrored on the server in OnCombatMechUnderbarrelShoot.
        if (!_timing.IsFirstTimePredicted ||
            _player.LocalEntity is not { } pilot ||
            !TryComp(pilot, out InsideCombatVehicleComponent? inside) ||
            Deleted(inside.Vehicle) ||
            !_combatMode.IsInCombatMode(pilot) ||
            _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary) != BoundKeyState.Down ||
            !TryGetActiveMountedUnderbarrel(pilot, out var weapon, out var gun))
        {
            return;
        }

        var time = _timing.CurTime;
        if (time < _nextUnderbarrelShootAt || time < gun.NextFire)
            return;

        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mousePos.MapId == MapId.Nullspace)
            return;

        var coordinates = _transform.ToCoordinates(mousePos);

        NetEntity? target = null;
        if (_state.CurrentState is GameplayStateBase screen)
            target = GetNetEntity(screen.GetClickedEntity(mousePos));

        if (_player.LocalSession == null)
            return;

        _rmcLag.SendLastRealTick();
        RaisePredictiveEvent(new CombatMechUnderbarrelShootEvent(
            GetNetCoordinates(coordinates),
            GetNetEntity(weapon),
            target));

        var fireRate = Math.Max(gun.FireRateModified, gun.FireRate);
        _nextUnderbarrelShootAt = gun.NextFire > time
            ? gun.NextFire
            : time + TimeSpan.FromSeconds(1 / Math.Max(fireRate, 0.1f));
    }

    private bool TryGetActiveMountedUnderbarrel(EntityUid pilot, out EntityUid weapon, out GunComponent gun)
    {
        weapon = default;
        gun = default!;

        if (!TryComp(pilot, out HandsComponent? hands))
        {
            return false;
        }

        foreach (var held in _hands.EnumerateHeld((pilot, hands)))
        {
            if (!HasComp<CombatMechWeaponComponent>(held) ||
                !TryGetActiveUnderbarrelOnWeapon(held, out gun))
            {
                continue;
            }

            weapon = held;
            return true;
        }

        return false;
    }

    private bool TryGetActiveUnderbarrelOnWeapon(EntityUid weapon, out GunComponent gun)
    {
        gun = default!;

        if (!_container.TryGetContainer(weapon, CombatMechComponent.UnderbarrelSlot, out var container) ||
            container.Count <= 0)
        {
            return false;
        }

        foreach (var attachable in container.ContainedEntities)
        {
            if (!HasComp<CombatMechUnderbarrelComponent>(attachable) ||
                !TryComp(attachable, out AttachableToggleableComponent? toggleable) ||
                !toggleable.Active ||
                !TryComp(attachable, out GunComponent? gunComp))
            {
                continue;
            }

            gun = gunComp;
            return true;
        }

        return false;
    }
}
