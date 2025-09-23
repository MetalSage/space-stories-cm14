using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio;

namespace Content.Shared._Stories.Vehicle.Systems;

public sealed partial class SharedVehicleSystem
{
    private void InitializeMovement()
    {
        SubscribeLocalEvent<VehicleMovementComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<VehicleMovementComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.LastSoundTime = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;
        var query = EntityQueryEnumerator<VehicleMovementComponent, InputMoverComponent>();
        while (query.MoveNext(out var uid, out var comp, out var moverComp))
        {
            var isMoving = _mover.UseMobMovement(uid) && _mover.GetWishDir((uid, moverComp)).Length() > 0.1f;

            if (!isMoving)
            {
                comp.IsCurrentlyMoving = false;
                continue;
            }

            if (!comp.IsCurrentlyMoving)
            {
                comp.IsCurrentlyMoving = true;
                comp.LastSoundTime = currentTime;
                
                PlayMovementSound((uid, comp));
            }

            if (currentTime - comp.LastSoundTime >= TimeSpan.FromSeconds(comp.SoundInterval))
            {
                PlayMovementSound((uid, comp));
                comp.LastSoundTime = currentTime;
            }
        }
    }

    private void PlayMovementSound(Entity<VehicleMovementComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _audio.PlayPredicted(ent.Comp.SoundCollection, ent, ent, ent.Comp.AudioParams);
    }
}
