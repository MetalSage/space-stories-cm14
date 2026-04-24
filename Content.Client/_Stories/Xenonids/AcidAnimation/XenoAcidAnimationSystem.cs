using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.AcidAnimation;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._Stories.Xenonids.AcidAnimation;

public sealed class XenoAcidAnimationSystem : SharedXenoAcidAnimationSystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly HashSet<EntityUid> _visible = new();
    private EntityUid? _predictedXeno;
    private EntityUid? _predictedAction;
    private bool _predictedActive;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoAcidAnimationComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<XenoAcidAnimationComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<XenoAcidAnimationComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<XenoAcidAnimationComponent, AppearanceChangeEvent>(OnAppearanceChange);

        _actions.OnActionAdded += OnActionChanged;
        _actions.OnActionRemoved += OnActionChanged;
        _actions.ActionsUpdated += RefreshLocalPrediction;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _actions.OnActionAdded -= OnActionChanged;
        _actions.OnActionRemoved -= OnActionChanged;
        _actions.ActionsUpdated -= RefreshLocalPrediction;
    }

    private void OnActionChanged(EntityUid _)
    {
        RefreshLocalPrediction();
    }

    private void OnComponentStartup(Entity<XenoAcidAnimationComponent> ent, ref ComponentStartup args)
    {
        RefreshVisuals(ent);
        RefreshLocalPrediction();
    }

    private void OnComponentShutdown(Entity<XenoAcidAnimationComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            SetSpitVisible((ent.Owner, sprite), false);

        _visible.Remove(ent.Owner);

        if (_predictedXeno == ent.Owner)
        {
            _predictedXeno = null;
            _predictedAction = null;
            _predictedActive = false;
        }
    }

    private void OnAfterHandleState(Entity<XenoAcidAnimationComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshVisuals(ent);
    }

    private void OnAppearanceChange(Entity<XenoAcidAnimationComponent> ent, ref AppearanceChangeEvent args)
    {
        RefreshVisuals(ent);
    }

    private void RefreshLocalPrediction()
    {
        var oldXeno = _predictedXeno;
        var oldAction = _predictedAction;
        var oldActive = _predictedActive;

        EntityUid? xeno = null;
        EntityUid? action = null;
        var active = false;

        if (_player.LocalEntity is { } local &&
            TryComp<XenoAcidAnimationComponent>(local, out var acidAnimation) &&
            TryGetSelectedAcidAction((local, acidAnimation), out var selected))
        {
            xeno = local;
            action = selected;
            active = true;
        }

        if (oldXeno == xeno &&
            oldAction == action &&
            oldActive == active)
        {
            return;
        }

        if (oldXeno != xeno && oldXeno is { } previousXeno && oldActive)
            RaiseNetworkEvent(new XenoAcidAnimationToggleEvent(GetNetEntity(previousXeno), NetEntity.Invalid, false));

        _predictedXeno = xeno;
        _predictedAction = action;
        _predictedActive = active;

        if (xeno is { } currentXeno &&
            action is { } currentAction &&
            active &&
            (oldXeno != xeno || !oldActive))
        {
            RaiseNetworkEvent(new XenoAcidAnimationToggleEvent(GetNetEntity(currentXeno), GetNetEntity(currentAction), true));
        }
        else if (xeno is { } inactiveXeno &&
                 oldXeno == xeno &&
                 oldActive &&
                 !active)
        {
            RaiseNetworkEvent(new XenoAcidAnimationToggleEvent(GetNetEntity(inactiveXeno), NetEntity.Invalid, false));
        }

        if (oldXeno is { } visualOldXeno &&
            TryComp<XenoAcidAnimationComponent>(visualOldXeno, out var previousComp))
        {
            RefreshVisuals((visualOldXeno, previousComp));
        }

        if (xeno is { } visualCurrentXeno &&
            visualCurrentXeno != oldXeno &&
            TryComp<XenoAcidAnimationComponent>(visualCurrentXeno, out var comp))
        {
            RefreshVisuals((visualCurrentXeno, comp));
        }
    }

    private bool TryGetSelectedAcidAction(Entity<XenoAcidAnimationComponent> xeno, out EntityUid actionUid)
    {
        actionUid = default;

        var selected = _ui.GetUIController<ActionUIController>().SelectingTargetFor;
        if (selected is not { } selectedAction)
            return false;

        if (_actions.GetAction(selectedAction) is not { } action ||
            action.Comp.AttachedEntity != xeno.Owner ||
            !action.Comp.Toggled ||
            !IsAcidAnimationAction(action.Owner, xeno.Comp))
        {
            return false;
        }

        actionUid = action.Owner;
        return true;
    }

    private void RefreshVisuals(Entity<XenoAcidAnimationComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var shouldShow = ShouldShow(ent);
        if (_visible.Contains(ent.Owner) == shouldShow)
            return;

        SetSpitVisible((ent.Owner, sprite), shouldShow);
    }

    private bool ShouldShow(Entity<XenoAcidAnimationComponent> ent)
    {
        var active = _player.LocalEntity == ent.Owner
            ? _predictedXeno == ent.Owner && _predictedActive
            : ent.Comp.Active;

        if (!active)
            return false;

        if (_appearance.TryGetData(ent, RMCXenoStateVisuals.Dead, out bool dead) && dead)
            return false;

        if (_appearance.TryGetData(ent, RMCXenoStateVisuals.Downed, out bool downed) && downed)
            return false;

        if (_appearance.TryGetData(ent, RMCXenoStateVisuals.Resting, out bool resting) && resting)
            return false;

        return true;
    }

    private void SetSpitVisible(Entity<SpriteComponent> ent, bool visible)
    {
        if (!ent.Comp.LayerMapTryGet(XenoAcidAnimationVisualLayers.Base, out var layer))
            return;

        if (visible && _visible.Add(ent.Owner))
            ent.Comp.LayerSetAnimationTime(layer, 0f);
        else if (!visible)
            _visible.Remove(ent.Owner);

        _sprite.LayerSetAutoAnimated((ent.Owner, ent.Comp), layer, visible);
        ent.Comp.LayerSetVisible(layer, visible);
    }
}
