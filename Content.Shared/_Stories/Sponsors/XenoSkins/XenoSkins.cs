using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Sponsors.XenoSkins;

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

[Serializable, NetSerializable]
public sealed partial class XenoSkinsDoAfterEvent : DoAfterEvent
{
    public readonly ResPath Path;
    public readonly ProtoId<XenoSkinsPrototype> Proto;

    public XenoSkinsDoAfterEvent(ResPath path, ProtoId<XenoSkinsPrototype> proto)
    {
        Path = path;
        Proto = proto;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

public sealed partial class XenoOpenSkinsMenuActionEvent : InstantActionEvent;
