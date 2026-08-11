namespace Content.Shared._Stories.Xenonids.Crusher;

[ByRefEvent]
public record struct STCrusherSplashHitEvent(EntityUid Attacker, EntityUid MainTarget);
