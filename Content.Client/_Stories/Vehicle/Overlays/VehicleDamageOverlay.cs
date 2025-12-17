using Content.Shared._Stories.Vehicle;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Stories.Vehicle;

public sealed class VehicleDamageOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _damageShader;

    private float _currentDamageLevel = 0f;
    private float _currentTime = 0f;
    private const float DamageSmoothingRate = 2f;
    private float _targetDamageLevel = 0f;

    public VehicleDamageOverlay()
    {
        IoCManager.InjectDependencies(this);
        _damageShader = _prototypeManager.Index<ShaderPrototype>("DamageInterference").InstanceUnique();
    }

    public void UpdateDamage(float damageLevel, float currentTime)
    {
        _targetDamageLevel = damageLevel;
        _currentTime = currentTime;
    }

    public void ResetDamage()
    {
        _currentDamageLevel = 0f;
        _targetDamageLevel = 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var deltaTime = (float)args.DeltaSeconds;
        var damageDirection = _targetDamageLevel - _currentDamageLevel;

        if (Math.Abs(damageDirection) > 0.001f)
        {
            var smoothingRate = DamageSmoothingRate;
            if (damageDirection > 0)
                smoothingRate *= 2f;

            _currentDamageLevel += damageDirection * smoothingRate * deltaTime;
            _currentDamageLevel = Math.Clamp(_currentDamageLevel, 0f, 1f);
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.HasComponent<VehiclePilotComponent>(_playerManager.LocalEntity))
            return false;

        if (!_entityManager.TryGetComponent<EyeComponent>(_playerManager.LocalEntity, out var eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return _currentDamageLevel > 0.001f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var useAnimations = !_config.GetCVar(CCVars.ReducedMotion);

        if (ScreenTexture == null)
            return;
        var handle = args.WorldHandle;

        _damageShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _damageShader.SetParameter("damageLevel", _currentDamageLevel);

        var shaderTime = useAnimations ? _currentTime : 0f;
        _damageShader.SetParameter("time", shaderTime);

        handle.UseShader(_damageShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
