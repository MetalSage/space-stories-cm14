namespace Content.Shared._Stories.Xenonids.Predalien.PredalienRoar;

public sealed class STPredalienRevealEvent : EntityEventArgs
{
    public readonly EntityUid Target;

    public STPredalienRevealEvent(EntityUid target)
    {
        Target = target;
    }
}
