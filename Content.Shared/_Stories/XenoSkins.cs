using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stories;

[Serializable, NetSerializable]
public enum XenoSkinsUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class XenoSkinsBuiMsg(ProtoId<XenoSkinsPrototype> choice) : BoundUserInterfaceMessage
{
    public readonly ProtoId<XenoSkinsPrototype> Choice = choice;
}

[Serializable, NetSerializable]
public sealed class XenoSkinChangeRSIEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly ResPath SkinPath;

    public XenoSkinChangeRSIEvent(NetEntity netEntity, ResPath skinPath)
    {
        NetEntity = netEntity;
        SkinPath = skinPath;
    }
}
public sealed partial class XenoOpenSkinsMenuActionEvent : InstantActionEvent;
