using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem
{
    private void InitializeMovement()
    {
        SubscribeLocalEvent<VehicleMovementComponent, MoveInputEvent>(OnVehicleMoveInput);
    }

    private void OnVehicleMoveInput(Entity<VehicleMovementComponent> ent, ref MoveInputEvent args)
    {
        var currentTime = _timing.CurTime;
        var comp = ent.Comp;

        var inputComp = args.Entity.Comp;
        var oldButtons = inputComp.HeldMoveButtons;

        var hasMovement = inputComp.HeldMoveButtons != MoveButtons.None;
        var wasMoving = comp.IsCurrentlyMoving;

        comp.IsCurrentlyMoving = hasMovement;

        if (hasMovement && (!wasMoving || inputComp.HeldMoveButtons != oldButtons))
        {
            if (currentTime >= comp.NextSoundTime)
            {
                _audio.PlayPredicted(comp.SoundCollection, ent, ent, comp.AudioParams);
                comp.NextSoundTime = currentTime + TimeSpan.FromSeconds(comp.SoundInterval);
            }
        }
    }
}
