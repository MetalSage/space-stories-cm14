using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using System.Numerics;
using Robust.Shared.Physics.Systems;
using Content.Shared._RMC14.Stun;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private void InitializeMovement()
    {
        //SubscribeLocalEvent<VehicleMovementComponent, MoveInputEvent>(OnVehicleMoveInput);
        SubscribeLocalEvent<VehicleMovementComponent, StartCollideEvent>(OnVehicleCollision);
    }

/* TODO FIX THIS SHIT
    private void OnVehicleMoveInput(Entity<VehicleMovementComponent> ent, ref MoveInputEvent args)
    {
        var currentTime = _timing.CurTime;
        var comp = ent.Comp;
        var inputComp = args.Entity.Comp;
        
        var hasMovement = inputComp.HeldMoveButtons != MoveButtons.None;
        var wasMoving = comp.IsCurrentlyMoving;
        
        var directionChanged = comp.LastMoveButtons != inputComp.HeldMoveButtons;
        
        comp.IsCurrentlyMoving = hasMovement;
        comp.LastMoveButtons = inputComp.HeldMoveButtons;

        if (hasMovement && currentTime >= comp.NextSoundTime)
        {
            if (!wasMoving)
            {
                _audio.PlayPredicted(comp.SoundCollection, ent, ent, comp.AudioParams);
                comp.NextSoundTime = currentTime + TimeSpan.FromSeconds(comp.SoundInterval);
            }
            else if (directionChanged && currentTime >= comp.NextDirectionChangeSoundTime)
            {
                _audio.PlayPredicted(comp.SoundCollection, ent, ent, comp.AudioParams);
                comp.NextDirectionChangeSoundTime = currentTime + TimeSpan.FromSeconds(comp.SoundInterval);
            }
        }
        
        if (!hasMovement && wasMoving)
        {
            comp.NextDirectionChangeSoundTime = currentTime;
        }
    }
*/

    private void OnVehicleCollision(Entity<VehicleMovementComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<RMCSizeComponent>(args.OtherEntity, out var rmcSize))
            return;

        if (rmcSize.Size != RMCSizes.Immobile)
            return;


    }
}
