namespace Content.Shared.Weapons.Melee.Events;

/// <summary>
/// Raised directed on a weapon when attempt a melee attack.
/// </summary>
[ByRefEvent]
public record struct AttemptMeleeEvent(EntityUid User, bool Cancelled = false, string? Message = null);

/// <summary>
/// Raised directed on the target entity when being attacked.
/// </summary>
[ByRefEvent]
public record struct GettingMeleeAttemptEvent(EntityUid Attacker, bool Cancelled = false); // Stories-Vehicle-Content
