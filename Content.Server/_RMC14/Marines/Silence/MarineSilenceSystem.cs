using Content.Server.Actions;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Honor;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Silence;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Marines.Silence;

public sealed class MarineSilenceSystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> RankHierarchy = "RMCMarineRankHierarchy";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MarineSilenceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MarineSilenceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MarineSilenceComponent, MarineSilenceActionEvent>(OnSilence);
    }

    private void OnMapInit(Entity<MarineSilenceComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        _actions.SetUseDelay(ent.Comp.ActionEntity, ent.Comp.Cooldown);
    }

    private void OnShutdown(Entity<MarineSilenceComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnSilence(Entity<MarineSilenceComponent> ent, ref MarineSilenceActionEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent) || ent.Comp.NextSilenceAt > _timing.CurTime ||
            !TryComp(ent, out TransformComponent? transform) || !TryGetRankIndex(ent.Owner, out var issuerRank))
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(transform.Coordinates, ent.Comp.Range, _nearby);
        _chat.TrySendInGameICMessage(ent, Loc.GetString(GetCallout(ent.Comp.Authority)), InGameICChatType.Speak, false);

        foreach (var nearby in _nearby)
        {
            if (nearby == ent.Owner ||
                !IsEligibleMarine(nearby) ||
                !_interaction.InRangeUnobstructed(ent.Owner, nearby, ent.Comp.Range) ||
                !TryGetRankIndex(nearby, out var targetRank) ||
                targetRank >= issuerRank)
            {
                continue;
            }

            ApplySilence(nearby, ent.Comp);
        }

        ent.Comp.NextSilenceAt = _timing.CurTime + ent.Comp.Cooldown;
        _actions.SetCooldown(ent.Comp.ActionEntity, ent.Comp.Cooldown);
        args.Handled = true;
    }

    private void ApplySilence(EntityUid marine, MarineSilenceComponent command)
    {
        var silence = EnsureComp<MarineSilencedForcedWhisperComponent>(marine);
        silence.ExpiresAt = _timing.CurTime + command.Duration;
        SendSilencedMessage(marine, command.Authority);

        Timer.Spawn(command.Duration, () =>
        {
            if (TryComp(marine, out MarineSilencedForcedWhisperComponent? active) && active.ExpiresAt <= _timing.CurTime)
                RemCompDeferred<MarineSilencedForcedWhisperComponent>(marine);
        });
    }

    private void SendSilencedMessage(EntityUid marine, MarineSilenceAuthority authority)
    {
        if (!_players.TryGetSessionByEntity(marine, out var session))
            return;

        var officer = authority == MarineSilenceAuthority.Officer;
        var message = Loc.GetString(officer ? "rmc-marine-silence-officer-message" : "rmc-marine-silence-sergeant-message");
        _chatManager.ChatMessageToOne(
            ChatChannel.Local,
            FormattedMessage.RemoveMarkupOrThrow(message),
            message,
            EntityUid.Invalid,
            false,
            session.Channel,
            officer ? Color.FromHex("#E6D69A") : Color.FromHex("#8B0000"));
    }

    private bool TryGetRankIndex(EntityUid entity, out int rankIndex)
    {
        rankIndex = -1;
        if (!TryComp(entity, out RankComponent? rank) || rank.Rank == null ||
            !_prototypes.TryIndex(RankHierarchy, out var hierarchy))
        {
            return false;
        }

        rankIndex = hierarchy.Values.IndexOf(rank.Rank.Value);
        return rankIndex >= 0;
    }

    private bool IsEligibleMarine(EntityUid entity)
    {
        return HasComp<MarineComponent>(entity) && HasComp<RankComponent>(entity) && !_mobState.IsDead(entity);
    }

    private string GetCallout(MarineSilenceAuthority authority)
    {
        return authority switch
        {
            MarineSilenceAuthority.Officer => _random.Pick(new[]
            {
                "rmc-marine-silence-officer-1", "rmc-marine-silence-officer-2", "rmc-marine-silence-officer-3",
            }),
            MarineSilenceAuthority.Sergeant => _random.Pick(new[]
            {
                "rmc-marine-silence-sergeant-1", "rmc-marine-silence-sergeant-2", "rmc-marine-silence-sergeant-3", "rmc-marine-silence-sergeant-4",
            }),
            MarineSilenceAuthority.MilitaryPolice => _random.Pick(new[]
            {
                "rmc-marine-silence-mp-1", "rmc-marine-silence-mp-2", "rmc-marine-silence-mp-3",
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, null),
        };
    }
}
