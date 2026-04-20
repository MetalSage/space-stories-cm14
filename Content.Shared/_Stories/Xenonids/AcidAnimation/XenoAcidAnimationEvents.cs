using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.AcidAnimation;

[Serializable, NetSerializable]
public sealed class XenoAcidAnimationToggleEvent : EntityEventArgs
{
    public NetEntity Xeno;
    public NetEntity Action;
    public bool Active;

    public XenoAcidAnimationToggleEvent(NetEntity xeno, NetEntity action, bool active)
    {
        Xeno = xeno;
        Action = action;
        Active = active;
    }
}
