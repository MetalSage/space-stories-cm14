using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.Xenonids.Crusher;

[ByRefEvent]
public struct STWoundTreatStageAttemptEvent
{
    public readonly bool Deep;
    public bool FullyTreated;

    public STWoundTreatStageAttemptEvent(bool deep)
    {
        Deep = deep;
        FullyTreated = false;
    }
}
