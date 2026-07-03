using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Stories.CombatMech;

public sealed partial class CombatMechSystem
{
    private void OnCombatMechMeleeHit(Entity<CombatMechMeleeDamageMultiplierComponent> ent, ref MeleeHitEvent args)
    {
        if (_net.IsClient)
            return;

        if (!args.IsHit || args.HitEntities.Count == 0 || ent.Comp.Multiplier <= 1f)
            return;

        var extraDamage = args.BaseDamage * (ent.Comp.Multiplier - 1f);
        if (extraDamage.Empty)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<CombatMechComponent>(target))
                continue;

            _damageable.TryChangeDamage(target, extraDamage, origin: args.User, tool: args.Weapon);
        }
    }
}
