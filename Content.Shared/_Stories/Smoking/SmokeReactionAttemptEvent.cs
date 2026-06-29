using Robust.Shared.GameObjects;

namespace Content.Shared.Smoking;

/// <summary>
/// Raised when smoke attempts to react with an entity to allow systems (like bio-suits) to block it.
/// </summary>
[ByRefEvent]
public record struct SmokeReactionAttemptEvent(EntityUid Target, bool Cancelled = false);
