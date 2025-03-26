using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class XenoSkinsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<XenoSkinsPrototype>> Skins = new();

    [DataField, AutoNetworkedField]
    public ProtoId<XenoSkinsPrototype> CurrentSkin;
}
