using Content.Shared.Damage;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared._Stories.Vehicle;

namespace Content.Client._Stories.Vehicle;

public sealed class VehicleDamageOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    
    private VehicleDamageOverlay _overlay = default!;
    public static string DamageOverlayKey = "VehicleDamageInterference";
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehiclePilotComponent, ComponentInit>(OnMapInit);
        SubscribeLocalEvent<VehiclePilotComponent, ComponentShutdown>(OnShutdown);
        
        SubscribeLocalEvent<VehiclePilotComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<VehiclePilotComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        _overlay = new();
    }
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateOverlayDamage();
    }
    
    private void UpdateOverlayDamage()
    {
        var player = _player.LocalEntity;
        if (player == null || !TryComp<VehiclePilotComponent>(player, out var pilot))
            return;
        if (pilot == null || pilot.Vehicle == null)
            return;
        
        var damageLevel = CalculateDamageLevel(pilot.Vehicle.Value);
        _overlay.UpdateDamage(damageLevel, (float)_timing.CurTime.TotalSeconds);
    }
    
    private float CalculateDamageLevel(EntityUid vehicle)
    {
        if (!TryComp<DamageableComponent>(vehicle, out var damageable) ||
            !TryComp<VehicleComponent>(vehicle, out var vehicleComp))
            return 0f;
        
        var totalDamage = (float)damageable.TotalDamage;
        var maxHealth = (float)vehicleComp.MaxHealth;
        
        if (maxHealth <= 0)
            return 0f;
        
        // damage as a percentage 0 = no damage 1 = gg
        var damageRatio = Math.Min(totalDamage / maxHealth, 1f);
        
        if (damageRatio < 0.2f)
            return 0f;
        
        var adjustedRatio = (damageRatio - 0.2f) / 0.8f;
        return Math.Min(adjustedRatio * adjustedRatio, 1f);
    }
    
    private void OnPlayerAttached(EntityUid uid, VehiclePilotComponent component, LocalPlayerAttachedEvent args)
    {
        if (component.DrawOverlay)
            _overlayMan.AddOverlay(_overlay);
    }
    
    private void OnPlayerDetached(EntityUid uid, VehiclePilotComponent component, LocalPlayerDetachedEvent args)
    {
        _overlay.ResetDamage();
        _overlayMan.RemoveOverlay(_overlay);
    }
    
    private void OnMapInit(EntityUid uid, VehiclePilotComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid && component.DrawOverlay)
            _overlayMan.AddOverlay(_overlay);
    }
    
    private void OnShutdown(EntityUid uid, VehiclePilotComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlay.ResetDamage();
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}