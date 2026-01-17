using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Bracer;

[Serializable] [NetSerializable]
public sealed partial class BracerPryDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable] [NetSerializable]
public sealed partial class BracerSmashPryDoAfterEvent : SimpleDoAfterEvent
{
}
