using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[RegisterComponent] [NetworkedComponent]
public sealed partial class BracerCloakedComponent : Component;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class BracerRecentlyUncloakedComponent : Component
{
    [DataField] [AutoNetworkedField]
    public TimeSpan ExpireTime;
}

[RegisterComponent] [NetworkedComponent]
[Access(typeof(BracerSystem))]
public sealed partial class BracerCancelUseWithCloakComponent : Component
{
    [DataField]
    public string CancelMessage = "st-bracer-cloak-cannot-use";
}
