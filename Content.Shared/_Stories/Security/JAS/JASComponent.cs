using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Security.JAS;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class JASComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Authorized = false;

    [DataField, AutoNetworkedField]
    public JASTabs CurrectTab = JASTabs.None;

    [DataField, AutoNetworkedField]
    public string User = string.Empty;

    [DataField, AutoNetworkedField]
    public string PrivilegedIdSlot = "PrivilegedIdSlot";

    [DataField, AutoNetworkedField]
    public string SuspectIdSlot =  "SuspectIdSlot";

    [DataField, AutoNetworkedField]
    public string Details =  string.Empty;

    [ViewVariables, DataField, AutoNetworkedField]
    public List<ProtoId<LawChargePrototype>> ChoosedLaws = new();

    [ViewVariables, DataField("laws"), AutoNetworkedField]
    public HashSet<ProtoId<LawCategoryPrototype>> LawCategories = new();
}

[Serializable, NetSerializable]
public enum JASTabs
{
    None,
    Details,
    Charges,
    Witness,
    Export,
}
