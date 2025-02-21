using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

[Serializable, NetSerializable]
public record struct XenoBoxerKOTracker(float Count, TimeSpan Last);
