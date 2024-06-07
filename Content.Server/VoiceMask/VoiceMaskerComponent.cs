using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server.VoiceMask;

[RegisterComponent]
public sealed partial class VoiceMaskerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] public string LastSetName = "Unknown";

    [DataField]
    public ProtoId<SpeechVerbPrototype>? LastSpeechVerb;

    [DataField]
    public string? LastSetVoice; // Stories-TTS

    [DataField]
    public EntProtoId Action = "ActionChangeVoiceMask";

    [DataField]
    public EntityUid? ActionEntity;
}
