using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Evolution;

[RegisterComponent]
public sealed partial class StoriesXenoSpawnOnEvolveComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Spawn = default!;
}
