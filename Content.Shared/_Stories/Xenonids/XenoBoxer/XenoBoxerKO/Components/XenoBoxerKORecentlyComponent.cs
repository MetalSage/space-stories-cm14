using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoBoxerKORecentlyComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, XenoBoxerKOTracker> Trackers = new();

    [DataField, AutoNetworkedField]
    public TimeSpan ExpireAfter = TimeSpan.FromSeconds(5);
}
