using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Synth;

[Serializable, NetSerializable]
public record GenerationSelectedActionEvent(EntProtoId<SynthGenerationComponent> Generation);
