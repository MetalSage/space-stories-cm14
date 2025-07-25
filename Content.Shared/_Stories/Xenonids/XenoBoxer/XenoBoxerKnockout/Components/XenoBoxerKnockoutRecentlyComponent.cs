using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.XenoBoxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoBoxerKnockoutRecentlyComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, XenoBoxerKnockoutTracker> Trackers = new();

    [DataField, AutoNetworkedField]
    public TimeSpan ExpireAfter = TimeSpan.FromSeconds(5);
}
