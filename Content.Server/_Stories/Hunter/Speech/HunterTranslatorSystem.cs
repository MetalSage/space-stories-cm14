using System.Text;
using Content.Server.Chat.Systems;
using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Profiles;
using Content.Shared._Stories.Hunter.Speech;
using Robust.Shared.Random;

namespace Content.Server._Stories.Hunter.Speech;

public sealed class HunterTranslatorSystem : EntitySystem
{
    [Dependency] private readonly BracerSystem _bracer = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterTranslatorComponent, TransformSpeechEvent>(OnTransformSpeech);
    }

    private void OnTransformSpeech(Entity<HunterTranslatorComponent> ent, ref TransformSpeechEvent args)
    {
        if (
            TryComp<HunterComponent>(args.Sender, out var hunter)
            && _bracer.IsHunterWithBracer(args.Sender, out var bracer)
            && bracer.Value.Comp.TranslatorActive
            && ent.Comp.Style == HunterSoundStyle.Retro
        )
            args.Message = ApplyHunterSpeak(args.Message);
    }

    private string ApplyHunterSpeak(string message)
    {
        var sb = new StringBuilder(message);

        sb.Replace("c", "k");
        sb.Replace("C", "K");
        sb.Replace("x", "ks");
        sb.Replace("X", "Ks");
        sb.Replace("s", "z");
        sb.Replace("S", "Z");
        sb.Replace("ph", "f");
        sb.Replace("Ph", "F");
        sb.Replace("qu", "kw");
        sb.Replace("Qu", "Kw");

        sb.Replace("i", "Y");
        sb.Replace("l", "I");
        sb.Replace("a", "A");
        sb.Replace("r", "R");

        sb.Replace("с", "з");
        sb.Replace("С", "З");
        sb.Replace("к", "k");
        sb.Replace("К", "K");
        sb.Replace("х", "kh");
        sb.Replace("Х", "Kh");
        sb.Replace("ц", "ts");
        sb.Replace("Ц", "Ts");
        sb.Replace("ф", "ph");
        sb.Replace("Ф", "Ph");

        sb.Replace("а", "А");
        sb.Replace("о", "О");
        sb.Replace("р", "Р");

        return sb.ToString();
    }
}
