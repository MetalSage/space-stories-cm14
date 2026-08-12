using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Security.JAS;

[Serializable, NetSerializable]
public sealed partial class JASBuiState : BoundUserInterfaceState
{
    public bool Authorized;
    public string User;
    public string? Details;
    public JASTabs Tab;

    public JASBuiState(bool authorized, string user, string? details, JASTabs tab)
    {
        Authorized = authorized;
        User = user;
        Details = details;
        Tab = tab;
    }
}

[Serializable, NetSerializable]
public enum JASBUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class JASTabSelectedMsg(JASTabs tab) : BoundUserInterfaceMessage
{
    public JASTabs Tab = tab;
}

[Serializable, NetSerializable]
public sealed class JASPrivilegedIdInsertedMsg() : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class JASPrivilegedIdEjectedMsg() : BoundUserInterfaceMessage {}


[Serializable, NetSerializable]
public sealed class JASSuspectIdInsertedMsg() : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class JASSuspectIdEjectedMsg():  BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class JASDataDiskImportMsg() : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class JASDataDiskExportMsg() : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class JASChargeAddMsg(ProtoId<LawChargePrototype> proto) : BoundUserInterfaceMessage
{
    public ProtoId<LawChargePrototype> Proto = proto;
}

[Serializable, NetSerializable]
public sealed class JASChargeRemoveMsg(ProtoId<LawChargePrototype> proto) : BoundUserInterfaceMessage
{
    public ProtoId<LawChargePrototype> Proto = proto;
}

[Serializable, NetSerializable]
public sealed class JASDetailsSaveMsg(string msg) : BoundUserInterfaceMessage
{
    public string Msg = msg;
}
