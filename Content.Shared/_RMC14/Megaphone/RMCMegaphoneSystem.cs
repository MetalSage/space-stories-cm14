using Content.Shared.Interaction.Events;
using Content.Shared._RMC14.Dialog;
using Content.Shared._Stories.TTS;
using Content.Shared.Examine;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Megaphone;

public sealed class RMCMegaphoneSystem : EntitySystem
{
    [Dependency] private readonly DialogSystem _dialog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCMegaphoneComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCMegaphoneComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUseInHand(Entity<RMCMegaphoneComponent> ent, ref UseInHandEvent args)
    {
        args.Handled = true;

        var ev = new MegaphoneInputEvent(
            GetNetEntity(args.User),
            VoiceRangeMultiplier: ent.Comp.VoiceRangeMultiplier,
            TTSVolumeMultiplier: ent.Comp.TTSVolumeMultiplier, // Stories
            TTSRangeMultiplier: ent.Comp.TTSRangeMultiplier, // Stories
            TTSReferenceDistance: ent.Comp.TTSReferenceDistance, // Stories
            TTSRolloffFactor: ent.Comp.TTSRolloffFactor, // Stories
            TTSAudioEffects: ent.Comp.TTSAudioEffects); // Stories
        _dialog.OpenInput(args.User, Loc.GetString("rmc-megaphone-ui-text"), ev, largeInput: false, characterLimit: 150);
    }

    private void OnExamined(Entity<RMCMegaphoneComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("rmc-megaphone-examine"));
    }
}

[Serializable, NetSerializable]
public sealed record MegaphoneInputEvent(
    NetEntity Actor,
    string Message = "",
    float VoiceRangeMultiplier = 1.5f,
    float TTSVolumeMultiplier = 1.5f, // Stories
    float TTSRangeMultiplier = 1.5f, // Stories
    float TTSReferenceDistance = 4f, // Stories
    float TTSRolloffFactor = 0.25f, // Stories
    TTSAudioEffect TTSAudioEffects = TTSAudioEffect.Megaphone) : DialogInputEvent(Message); // Stories
