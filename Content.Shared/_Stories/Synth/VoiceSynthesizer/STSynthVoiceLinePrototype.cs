using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth.VoiceSynthesizer;

[Prototype("stSynthVoiceLine")]
public sealed partial class STSynthVoiceLinePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Category;

    [DataField(required: true)]
    public LocId Text;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public SoundSpecifier? AlternateSound;
}
