using System.Numerics;
using Content.Shared._Stories.CombatMech;
using Content.Shared.Weapons.Melee.Events;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Stories.CombatMech;

public sealed class CombatMechLungeSystem : EntitySystem
{
    private const string MechLungeKey = "stories-combat-mech-lunge";

    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeAllEvent<LightAttackEvent>(OnLightAttack);
        SubscribeAllEvent<HeavyAttackEvent>(OnHeavyAttack);
        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
    }

    private void OnLightAttack(LightAttackEvent msg, EntitySessionEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var user = args.SenderSession?.AttachedEntity;
        if (user == null || !TryComp(user.Value, out InsideCombatVehicleComponent? inside))
            return;

        PlayLunge(inside.Vehicle, GetCoordinates(msg.Coordinates));
    }

    private void OnHeavyAttack(HeavyAttackEvent msg, EntitySessionEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var user = args.SenderSession?.AttachedEntity;
        if (user == null || !TryComp(user.Value, out InsideCombatVehicleComponent? inside))
            return;

        PlayLunge(inside.Vehicle, GetCoordinates(msg.Coordinates));
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var user = GetEntity(ev.Entity);
        if (!TryComp(user, out InsideCombatVehicleComponent? inside))
            return;

        PlayLunge(inside.Vehicle, ev.LocalPos);
    }

    private void PlayLunge(EntityUid vehicle, EntityCoordinates coordinates)
    {
        if (Deleted(vehicle) ||
            !TryComp(vehicle, out TransformComponent? xform) ||
            xform.MapID == MapId.Nullspace)
        {
            return;
        }

        var invMatrix = _transform.GetInvWorldMatrix(xform);
        var localPos = Vector2.Transform(_transform.ToMapCoordinates(coordinates).Position, invMatrix);
        localPos = xform.LocalRotation.RotateVec(localPos);
        PlayLunge(vehicle, localPos);
    }

    private void PlayLunge(EntityUid vehicle, Vector2 localPos)
    {
        if (localPos.LengthSquared() <= 0f)
            return;

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(0.1f),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(localPos.Normalized() * 0.15f, 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.1f),
                    },
                },
            },
        };

        _animation.Stop(vehicle, MechLungeKey);
        _animation.Play(vehicle, animation, MechLungeKey);
    }
}
