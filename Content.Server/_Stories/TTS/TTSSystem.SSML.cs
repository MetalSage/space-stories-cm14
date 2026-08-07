namespace Content.Server._Stories.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private string ToSsmlText(string text, SoundTraits traits = SoundTraits.None)
    {
        var result = text;
        if (traits.HasFlag(SoundTraits.RateFast))
            result = $"{result}";
        if (traits.HasFlag(SoundTraits.PitchVerylow))
            result = $"{result}";
        return $"{result}";
    }

    [Flags]
    private enum SoundTraits : ushort
    {
        None = 0,
        RateFast = 1 << 0,
        PitchVerylow = 1 << 1,
    }
}
