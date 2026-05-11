using Content.Shared._RMC14.Emote;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Clothing;

public sealed class HelmetTapSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HelmetTapComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<HelmetTapComponent> ent, ref InteractUsingEvent args)
    {
        var curTime = _timing.CurTime;

        if (curTime < ent.Comp.LastTapTime + ent.Comp.Cooldown)
            return;

        ent.Comp.LastTapTime = curTime;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.TapSound, ent, args.User);
        _emote.TryEmoteWithChat(args.User, "HelmetTap");

        args.Handled = true;
    }
}