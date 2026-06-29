using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Ordnance.Assemblies;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrdnanceAssemblyComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsSecured;

    [DataField, AutoNetworkedField]
    public EntityUid? Holder;

    [DataField, AutoNetworkedField]
    public string SpriteId = "blank";
}
