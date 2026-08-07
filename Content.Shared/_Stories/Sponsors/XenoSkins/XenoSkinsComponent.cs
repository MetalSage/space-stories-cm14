using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Sponsors.XenoSkins;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class XenoSkinsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<XenoSkinsPrototype>> Skins = new();

    [DataField, AutoNetworkedField]
    public ProtoId<XenoSkinsPrototype>? CurrentSkin;

    [DataField, AutoNetworkedField]
    public EntProtoId Action = "STXenoSkinsMenuAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public TimeSpan DoAfterDelay = TimeSpan.FromSeconds(2.5);

    [ViewVariables]
    public DoAfterId? ActiveDoAfter;
}
