namespace Content.Shared._RMC14.ARES.Lockdown;

[ByRefEvent]
public readonly record struct STARESCoreLockdownChangedEvent(EntityUid Core, bool Active);
