using System.Numerics;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Bombard;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Scattered;
using Content.Shared._RMC14.Xenonids.Spray;
using Content.Shared._Stories.Xenonids.Boiler;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Stories.Xenonids.Boiler;

public sealed class BoilerAcidAnimationSystem : EntitySystem
{
    private const string SpitLayerKey = "stBoilerSpit";
    private static readonly RSI.StateId SpitState = new("spit");

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _animationStarts = new();
    private readonly HashSet<EntityUid> _currentBoilers = new();
    private readonly List<EntityUid> _boilersToStop = new();

    private EntityUid? _predictedBoiler;
    private bool _predictedActive;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        UpdatePredictedState();
        RefreshAnimations();
    }

    private void UpdatePredictedState()
    {
        EntityUid? boiler = null;
        var active = false;

        if (_player.LocalEntity is { } local &&
            HasComp<BoilerAcidAnimationComponent>(local))
        {
            boiler = local;
            active = HasSelectedBoilerAction(local);
        }

        if (_predictedBoiler != boiler)
        {
            if (_predictedBoiler != null && _predictedActive)
                RaiseNetworkEvent(new BoilerAcidAnimationToggleEvent(GetNetEntity(_predictedBoiler.Value), false));

            if (boiler != null && active)
                RaiseNetworkEvent(new BoilerAcidAnimationToggleEvent(GetNetEntity(boiler.Value), true));
        }
        else if (boiler != null && _predictedActive != active)
        {
            RaiseNetworkEvent(new BoilerAcidAnimationToggleEvent(GetNetEntity(boiler.Value), active));
        }

        _predictedBoiler = boiler;
        _predictedActive = active;
    }

    private bool HasSelectedBoilerAction(EntityUid uid)
    {
        var selected = _ui.GetUIController<ActionUIController>().SelectingTargetFor;
        if (selected is not { } actionUid)
            return false;

        foreach (var action in _actions.GetActions(uid))
        {
            if (action.Owner != actionUid)
                continue;

            if (_actions.GetEvent(action) is
                XenoCorrosiveAcidEvent or
                XenoSprayAcidActionEvent or
                XenoBombardActionEvent or
                XenoScatteredSpitActionEvent)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshAnimations()
    {
        _currentBoilers.Clear();

        var query = EntityQueryEnumerator<BoilerAcidAnimationComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            if (sprite.BaseRSI is not { } rsi ||
                !rsi.TryGetState(SpitState, out _))
            {
                HideSpitLayer((uid, sprite));
                continue;
            }

            if (!ShouldAnimate(uid, comp, sprite))
            {
                HideSpitLayer((uid, sprite));
                continue;
            }

            _currentBoilers.Add(uid);
            if (!_animationStarts.ContainsKey(uid))
                _animationStarts[uid] = _timing.CurTime;

            var layer = EnsureSpitLayer((uid, sprite), rsi);
            var animationTime = (float) (_timing.CurTime - _animationStarts[uid]).TotalSeconds;
            _sprite.LayerSetAnimationTime((uid, sprite), layer, animationTime);
            _sprite.LayerSetVisible((uid, sprite), layer, true);
        }

        _boilersToStop.Clear();
        foreach (var uid in _animationStarts.Keys)
        {
            if (!_currentBoilers.Contains(uid))
                _boilersToStop.Add(uid);
        }

        foreach (var uid in _boilersToStop)
        {
            _animationStarts.Remove(uid);

            if (TryComp<SpriteComponent>(uid, out var sprite))
                HideSpitLayer((uid, sprite));
        }
    }

    private int EnsureSpitLayer(Entity<SpriteComponent> ent, RSI rsi)
    {
        Entity<SpriteComponent?> spriteEnt = (ent.Owner, ent.Comp);
        var layer = _sprite.LayerMapReserve(spriteEnt, SpitLayerKey);
        _sprite.LayerSetRsi(spriteEnt, layer, rsi, SpitState);
        _sprite.LayerSetOffset(spriteEnt, layer, Vector2.Zero);
        _sprite.LayerSetAutoAnimated(spriteEnt, layer, false);
        return layer;
    }

    private void HideSpitLayer(Entity<SpriteComponent> ent)
    {
        Entity<SpriteComponent?> spriteEnt = (ent.Owner, ent.Comp);
        if (_sprite.LayerMapTryGet(spriteEnt, SpitLayerKey, out var layer, false))
            _sprite.LayerSetVisible(spriteEnt, layer, false);
    }

    private bool ShouldAnimate(EntityUid uid, BoilerAcidAnimationComponent comp, SpriteComponent sprite)
    {
        var localEntity = _player.LocalEntity;
        var isLocalBoiler = localEntity == uid;
        if (isLocalBoiler)
        {
            if (_predictedBoiler != uid || !_predictedActive)
                return false;
        }
        else if (!comp.Active)
        {
            return false;
        }

        if (sprite.BaseRSI is not { } rsi ||
            !rsi.TryGetState("spit", out _))
        {
            return false;
        }

        if (_appearance.TryGetData(uid, RMCXenoStateVisuals.Dead, out bool dead) && dead)
            return false;

        if (_appearance.TryGetData(uid, RMCXenoStateVisuals.Downed, out bool downed) && downed)
            return false;

        if (_appearance.TryGetData(uid, RMCXenoStateVisuals.Resting, out bool resting) && resting)
            return false;

        var angle = (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();
        var dir = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle);

        if (sprite.EnableDirectionOverride)
            dir = sprite.DirectionOverride.Convert(RsiDirectionType.Dir4);

        return dir != RsiDirection.North;
    }
}
