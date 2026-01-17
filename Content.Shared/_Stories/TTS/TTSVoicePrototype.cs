using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.TTS;

/// <summary>
/// Prototype represent available TTS voices
/// </summary>
[Prototype("ttsVoice")]
// ReSharper disable once InconsistentNaming
public sealed class TTSVoicePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// List of categories forbidden to use this voice (e.g., "Human", "Hunter", "Xeno").
    /// If null or empty, available to everyone.
    /// </summary>
    [DataField("blacklist")]
    public HashSet<string>? Blacklist;

    [DataField("name")]
    public string Name { get; } = string.Empty;

    [DataField("sex", required: true)]
    public Sex Sex { get; }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("speaker", required: true)]
    public string Speaker { get; } = string.Empty;

    /// <summary>
    /// Whether the species is available "at round start" (In the character editor)
    /// </summary>
    [DataField("roundStart")]
    public bool RoundStart { get; } = true;

    [DataField("sponsorOnly")]
    public bool SponsorOnly { get; }
}
