using Content.Shared._Stories.ChasmTeleporter;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Stories.ChasmTeleporter;

public sealed class ChasmTeleporterVisualsSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;

    private readonly string _chasmFallAnimationKey = "chasm_fall";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmTeleporterFallingComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ChasmTeleporterFallingComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(Entity<ChasmTeleporterFallingComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            TerminatingOrDeleted(ent))
        {
            return;
        }

        ent.Comp.OriginalScale = sprite.Scale;

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_anim.HasRunningAnimation(player, _chasmFallAnimationKey))
            return;

        _anim.Play((ent, player), GetFallingAnimation(ent.Comp), _chasmFallAnimationKey);
    }

    private void OnComponentRemove(Entity<ChasmTeleporterFallingComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            TerminatingOrDeleted(ent))
        {
            return;
        }

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_anim.HasRunningAnimation(player, _chasmFallAnimationKey))
            _anim.Stop(player, _chasmFallAnimationKey);

        sprite.Scale = ent.Comp.OriginalScale;
    }

    private Animation GetFallingAnimation(ChasmTeleporterFallingComponent component)
    {
        var length = component.AnimationTime;

        return new Animation()
        {
            Length = length,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(component.OriginalScale, 0.0f),
                        new AnimationTrackProperty.KeyFrame(component.AnimationScale, length.Seconds),
                    },
                    InterpolationMode = AnimationInterpolationMode.Cubic
                }
            }
        };
    }
}
