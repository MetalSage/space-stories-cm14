using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.AcidAnimation;

[Serializable, NetSerializable]
public sealed class XenoAcidAnimationToggleEvent : EntityEventArgs
{
    public readonly NetEntity Xeno;
    public readonly bool Active;

    public XenoAcidAnimationToggleEvent(NetEntity xeno, bool active)
    {
        Xeno = xeno;
        Active = active;
    }
}
