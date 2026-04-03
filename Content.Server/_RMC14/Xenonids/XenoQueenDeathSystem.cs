using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._RMC14.Xenonids;

public sealed class XenoQueenDeathSystem : EntitySystem
{
    private static readonly TimeSpan QueenDeathJitter = TimeSpan.FromSeconds(4);
    private const float FightOrFlightJitterAmplitude = 80f;
    private const float FightOrFlightJitterFrequency = 8f;

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnQueenMobStateChanged);
    }

    private void OnQueenMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!HasComp<XenoEvolutionGranterComponent>(args.Target))
            return;

        var xenos = EntityQueryEnumerator<XenoComponent>();
        while (xenos.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            _chat.TryEmoteWithChat(uid, "XenoRoar", ignoreActionBlocker: true, forceEmote: true);
            _jitter.DoJitter(uid, QueenDeathJitter, true, FightOrFlightJitterAmplitude, FightOrFlightJitterFrequency, true);
        }
    }
}
