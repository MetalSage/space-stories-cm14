using System.Linq;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Projectiles.Aimed;
using Content.Shared._RMC14.Weapons.Ranged.AimedShot;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Trigger;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Attachable.Systems;

public sealed class AttachableAimedShotSystem : EntitySystem
{
    [Dependency] private readonly GunIFFSystem _gunIFF = default!;

    private const float BaseRange = 6.0f;  // Original range in tiles
    private const float BaseDelay = 0.2f;  // Original delay in seconds for 7 tiles
    private const float BaseTimePerTile = BaseDelay / BaseRange;

    public override void Initialize()
    {
        SubscribeLocalEvent<AttachableAimedShotComponent, AttachableAlteredEvent>(OnAttachableAltered);
        SubscribeLocalEvent<AttachableAimedShotHolderComponent, AmmoShotEvent>(OnAmmoShot,
            after: new[] {typeof(SharedRMCAimedShotSystem)}
        );
    }

    private void OnAttachableAltered(Entity<AttachableAimedShotComponent> ent, ref AttachableAlteredEvent args)
    {
        var holder = args.Holder;
        
        switch (args.Alteration)
        {
            case AttachableAlteredType.Attached:
                if (!HasComp<AimedShotComponent>(holder))
                {
                    var aimedShotComp = new AimedShotComponent
                    {
                        AimedShotCooldown = ent.Comp.AimedShotCooldown,
                        Range = ent.Comp.Range,
                        AimDuration = ent.Comp.AimDuration,
                        AimDistanceDifficulty = ent.Comp.AimDistanceDifficulty
                    };
                    
                    AddComp(holder, aimedShotComp, true);
                    AddComp(holder, new AttachableAimedShotHolderComponent { Range = ent.Comp.Range }, true);
                }
                break;
                
            case AttachableAlteredType.Detached:
                RemCompDeferred<AimedShotComponent>(holder);
                RemCompDeferred<AttachableAimedShotHolderComponent>(holder);
                SetTriggerAmmoTimer(holder, BaseRange);
                break;
        }
    }

    private void OnAmmoShot(Entity<AttachableAimedShotHolderComponent> ent, ref AmmoShotEvent args)
    {
        var aimedProjectiles = args.FiredProjectiles
            .Where(p => HasComp<AimedProjectileComponent>(p))
            .ToList();

        SetTriggerAmmoTimer(ent.Owner, aimedProjectiles.Count == 0 ? BaseRange : ent.Comp.Range);
        if (aimedProjectiles.Count == 0)
            return;

        var aimedShotEvent = new AmmoShotEvent()
        {
            FiredProjectiles = aimedProjectiles
        };

        _gunIFF.GiveAmmoIFF(ent.Owner, ref aimedShotEvent, false, true);
    }


    private void SetTriggerAmmoTimer(EntityUid weapon, float range)
    {
        if (!TryComp<OnShootTriggerAmmoTimerComponent>(weapon, out var timerComp))
            return;

        // Calculate new delay based on range
        // Original 7 tiles = 0.2 seconds, so base ratio is 0.2/7 = ~0.0286 seconds per tile
        timerComp.Delay = (float)Math.Round(range * BaseTimePerTile, 2);
        Dirty(weapon, timerComp);
    }
}
