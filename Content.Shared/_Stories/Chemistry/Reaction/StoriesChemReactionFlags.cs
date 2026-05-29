using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Chemistry.Reaction;

[Flags]
[Serializable, NetSerializable]
public enum StoriesChemReactionFlags
{
    None = 0,
    Calm = 1 << 0,
    Bubbling = 1 << 1,
    Glowing = 1 << 2,
    Fire = 1 << 3,
    Smoking = 1 << 4,
    Endothermic = 1 << 5
}
