using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.Boiler;

[Serializable, NetSerializable]
public sealed class BoilerAcidAnimationToggleEvent : EntityEventArgs
{
    public readonly NetEntity Boiler;
    public readonly bool Active;

    public BoilerAcidAnimationToggleEvent(NetEntity boiler, bool active)
    {
        Boiler = boiler;
        Active = active;
    }
}
