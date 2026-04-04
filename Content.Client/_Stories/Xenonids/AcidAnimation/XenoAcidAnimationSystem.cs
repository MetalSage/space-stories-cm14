using System.Numerics;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Ball;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared._Stories.Xenonids.AcidAnimation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Xenonids.AcidAnimation;

public sealed class XenoAcidAnimationSystem : EntitySystem
{
    private const string SpitLayerKey = "xenoAcidSpit";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _animationStarts = new();
    private readonly HashSet<EntityUid> _currentXenos = new();
    private readonly List<EntityUid> _xenosToStop = new();

    private EntityUid? _predictedXeno;
    private bool _predictedActive;

    public override void Initialize()
    {
        if (!_overlays.HasOverlay<XenoAcidAnimationOverlay>())
            _overlays.AddOverlay(new XenoAcidAnimationOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<XenoAcidAnimationOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        UpdatePredictedState();
        RefreshAnimations();
    }

    private void UpdatePredictedState()
    {
        EntityUid? xeno = null;
        var active = false;

        if (_player.LocalEntity is { } local &&
            TryComp<XenoAcidAnimationComponent>(local, out var acidAnimation))
        {
            xeno = local;
            active = HasSelectedAcidAction(local, acidAnimation);
        }

        if (_predictedXeno != xeno)
        {
            if (_predictedXeno != null && _predictedActive)
                RaiseNetworkEvent(new XenoAcidAnimationToggleEvent(GetNetEntity(_predictedXeno.Value), false));

            if (xeno != null && active)
                RaiseNetworkEvent(new XenoAcidAnimationToggleEvent(GetNetEntity(xeno.Value), true));
        }
        else if (xeno != null && _predictedActive != active)
        {
            RaiseNetworkEvent(new XenoAcidAnimationToggleEvent(GetNetEntity(xeno.Value), active));
        }

        _predictedXeno = xeno;
        _predictedActive = active;
    }

    private bool HasSelectedAcidAction(EntityUid uid, XenoAcidAnimationComponent comp)
    {
        var selected = _ui.GetUIController<ActionUIController>().SelectingTargetFor;
        if (selected is not { } actionUid)
            return false;

        foreach (var action in _actions.GetActions(uid))
        {
            if (action.Owner != actionUid)
                continue;

            var protoId = MetaData(action).EntityPrototype?.ID;
            return protoId != null && comp.ActionIds.Contains(protoId);
        }

        return false;
    }

    private void RefreshAnimations()
    {
        _currentXenos.Clear();

        var query = EntityQueryEnumerator<XenoAcidAnimationComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            HideSpitLayer((uid, sprite));

            if (!ShouldAnimate(uid, comp, sprite, out var dir))
                continue;

            _currentXenos.Add(uid);
            if (!_animationStarts.ContainsKey(uid))
                _animationStarts[uid] = _timing.CurTime;
        }

        _xenosToStop.Clear();
        foreach (var uid in _animationStarts.Keys)
        {
            if (!_currentXenos.Contains(uid))
                _xenosToStop.Add(uid);
        }

        foreach (var uid in _xenosToStop)
        {
            _animationStarts.Remove(uid);
        }
    }

    private void HideSpitLayer(Entity<SpriteComponent> ent)
    {
        Entity<SpriteComponent?> spriteEnt = (ent.Owner, ent.Comp);
        if (_sprite.LayerMapTryGet(spriteEnt, SpitLayerKey, out var layer, false))
            _sprite.LayerSetVisible(spriteEnt, layer, false);
    }

    public Vector2 GetOffset(XenoAcidAnimationComponent comp)
    {
        return comp.Offset;
    }

    public bool ShouldAnimate(EntityUid uid, XenoAcidAnimationComponent comp, SpriteComponent sprite, out RsiDirection dir)
    {
        dir = RsiDirection.South;
        var localEntity = _player.LocalEntity;
        var active = comp.Active || HasComp<XenoActiveChargingSpitComponent>(uid);

        if (localEntity == uid && _predictedXeno == uid && _predictedActive)
            active = true;

        if (!active)
        {
            return false;
        }

        if (_appearance.TryGetData(uid, RMCXenoStateVisuals.Dead, out bool dead) && dead)
            return false;

        if (_appearance.TryGetData(uid, RMCXenoStateVisuals.Downed, out bool downed) && downed)
            return false;

        if (_appearance.TryGetData(uid, RMCXenoStateVisuals.Resting, out bool resting) && resting)
            return false;

        dir = GetRenderDirection(uid, sprite);

        return !comp.HideNorth || dir != RsiDirection.North;
    }

    public TimeSpan GetAnimationTime(EntityUid uid, TimeSpan curTime)
    {
        if (!_animationStarts.TryGetValue(uid, out var started))
            return TimeSpan.Zero;

        return curTime - started;
    }

    private RsiDirection GetRenderDirection(EntityUid uid, SpriteComponent sprite)
    {
        var angle = (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();
        var dir = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle);

        if (sprite.EnableDirectionOverride)
            dir = sprite.DirectionOverride.Convert(RsiDirectionType.Dir4);

        return dir;
    }
}
