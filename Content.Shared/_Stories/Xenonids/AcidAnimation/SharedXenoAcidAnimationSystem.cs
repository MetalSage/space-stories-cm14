namespace Content.Shared._Stories.Xenonids.AcidAnimation;

public abstract class SharedXenoAcidAnimationSystem : EntitySystem
{
    protected bool IsAcidAnimationAction(EntityUid action, XenoAcidAnimationComponent comp)
    {
        var protoId = MetaData(action).EntityPrototype?.ID;
        return protoId != null && comp.ActionIds.Contains(protoId);
    }
}
