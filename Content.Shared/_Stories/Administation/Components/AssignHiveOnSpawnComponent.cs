using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Extensions;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StoriesExtensions))]
public sealed partial class AssignHiveOnSpawnComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntityWhitelist? Whitelist;
}
