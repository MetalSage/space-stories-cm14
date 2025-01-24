using System.Numerics;
using Content.Client.Interactable;
using Content.Shared._RMC14.Xenonids.ScissorsCut;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client._RMC14.Xenonids.ScissorsCut;

public sealed class XenoScissorsCutSystem : SharedXenoScissorsCutSystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private const string AnimationKey = "cm-xeno-tail";

    protected override void DoLunge(Entity<XenoScissorsCutComponent, TransformComponent> user, Vector2 localPos, EntProtoId animationId)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var animationEnt = Spawn(animationId, user.Comp2.Coordinates);
        _transform.SetParent(animationEnt, user);

        var sprite = EnsureComp<SpriteComponent>(animationEnt);
        sprite.NoRotation = true;
        sprite.Rotation = localPos.ToWorldAngle();

        // lie by 20% so the player feels less bad about missing
        var distance = localPos.Length() * 0.80f;

        var origin = _transform.GetMapCoordinates(user);
        var unobstructedDistance = _interaction.UnobstructedDistance(origin, origin.Offset(localPos));
        if (distance > unobstructedDistance)
            distance = unobstructedDistance;

        var startOffset = sprite.Rotation.RotateVec(new Vector2(0, -distance / 5f));
        var endOffset = sprite.Rotation.RotateVec(new Vector2(0, -distance));

        const float length = 0.20f;

        var moveAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new KeyFrame(startOffset, 0),
                        new KeyFrame(endOffset, length),
                    }
                }
            }
        };

        _animation.Play(animationEnt, moveAnimation, AnimationKey);
    }
}
