using Content.Shared._Stories.Hunter.Profiles;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter;

[Serializable] [NetSerializable]
public sealed class UpdateHunterProfileEvent : EntityEventArgs
{
    public HunterProfile Profile = default!;
}
