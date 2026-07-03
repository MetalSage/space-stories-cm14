using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.TTS;

[Prototype("stTtsReplacement")]
public sealed partial class TTSReplacementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Pattern { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string ReplacedWith { get; private set; } = string.Empty;

    [DataField]
    public bool IsRegex { get; private set; }
}
