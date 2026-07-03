using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.TTS;

/// <summary>
/// Defines configuration for TTS text sanitization rules (e.g. abbreviations to phonetics, transliteration, stripping characters).
/// </summary>
[Prototype("ttsSanitizeConfig")]
public sealed partial class TTSSanitizeConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Phonetic alphabet mapping (e.g. A -> Эй)
    /// </summary>
    [DataField]
    public Dictionary<string, string> PhoneticAlphabet { get; private set; } = new();

    /// <summary>
    /// Transliteration mapping (e.g. a -> а)
    /// </summary>
    [DataField]
    public Dictionary<string, string> ReverseTranslit { get; private set; } = new();

    /// <summary>
    /// Regex for characters allowed in output. E.g. @"[^a-zA-Zа-яА-ЯёЁ0-9,\-+?!. ]"
    /// All characters matching this regex will be removed.
    /// </summary>
    [DataField]
    public string AllowedCharsRegex { get; private set; } = @"[^a-zA-Zа-яА-ЯёЁ0-9,\-+?!. ]";
}
