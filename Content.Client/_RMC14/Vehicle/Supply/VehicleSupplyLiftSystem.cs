using System;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.StepTrigger.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Vehicle.Supply;

public sealed class VehicleSupplyLiftSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string AnimationKey = "rmc_vehicle_supply_lift";
    private const string BaseLayerKey = "rmc-vehicle-supply-lift-base";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleSupplyLiftComponent, AfterAutoHandleStateEvent>(OnLiftHandleState);
        SubscribeLocalEvent<VehicleSupplyLiftComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt); // Stories-Vehicle
    }

    private void OnStepTriggerAttempt(Entity<VehicleSupplyLiftComponent> ent, ref StepTriggerAttemptEvent args) // Stories-Vehicle
    {
        if (ent.Comp.Mode == VehicleSupplyLiftMode.Raised || ent.Comp.Mode == VehicleSupplyLiftMode.Preparing)
            args.Cancelled = true;
    }

    private void OnLiftHandleState(Entity<VehicleSupplyLiftComponent> lift, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(lift, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((lift.Owner, sprite), BaseLayerKey, out var layer, false))
        {
            return;
        }

        if (lift.Comp.Mode != VehicleSupplyLiftMode.Preparing)
            _animation.Stop(lift.Owner, AnimationKey);

        switch (lift.Comp.Mode)
        {
            case VehicleSupplyLiftMode.Lowered:
                _sprite.LayerSetRsiState((lift.Owner, sprite), layer, lift.Comp.LoweredState);
                break;
            case VehicleSupplyLiftMode.Raised:
                _sprite.LayerSetRsiState((lift.Owner, sprite), layer, lift.Comp.RaisedState);
                break;
            case VehicleSupplyLiftMode.Lowering:
                lift.Comp.LoweringAnimation ??= new Animation
                {
                    Length = TimeSpan.FromSeconds(2.1f),
                    AnimationTracks =
                    {
                        new AnimationTrackSpriteFlick
                        {
                            LayerKey = BaseLayerKey,
                            KeyFrames =
                            {
                                new AnimationTrackSpriteFlick.KeyFrame(lift.Comp.LoweringState, 0)
                            }
                        }
                    }
                };

                _animation.Play(lift, (Animation)lift.Comp.LoweringAnimation, AnimationKey); // Stories-Vehicle
                break;
            case VehicleSupplyLiftMode.Raising:
                lift.Comp.RaisingAnimation ??= new Animation
                {
                    Length = TimeSpan.FromSeconds(2.1f),
                    AnimationTracks =
                    {
                        new AnimationTrackSpriteFlick
                        {
                            LayerKey = BaseLayerKey,
                            KeyFrames =
                            {
                                new AnimationTrackSpriteFlick.KeyFrame(lift.Comp.RaisingState, 0)
                            }
                        }
                    }
                };

                _animation.Play(lift, (Animation)lift.Comp.RaisingAnimation, AnimationKey); // Stories-Vehicle
                break;
        }
    }
}
