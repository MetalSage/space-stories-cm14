namespace Content.Shared._Stories.Ordnance;

[ByRefEvent]
public readonly record struct OrdnancePulseEvent(EntityUid Source, EntityUid? User);
