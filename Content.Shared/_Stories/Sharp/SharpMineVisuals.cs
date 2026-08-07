using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Sharp;

[Serializable, NetSerializable]
public enum SharpMineVisuals
{
    State,
    Layer,
}

[Serializable, NetSerializable]
public enum SharpMineState
{
    Inactive,
    Arming,
    Level1,
    Level2,
    Level3,
    Level4,
}
