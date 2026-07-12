using System;
using System.Collections.Generic;
using Content.Server._RMC14.Atmos;
using Content.Server.Popups;
using Content.Shared._Stories.Chemistry.Reaction;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Chemistry.Reaction;

public sealed class StoriesReactionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RMCFlammableSystem _flammable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _bubblingTargets = new();
    private static readonly StoriesReactionConfig DefaultConfig = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolutionComponent, ChemicalReactionOccurredEvent>(OnReactionOccurred);
    }

    private void OnReactionOccurred(Entity<SolutionComponent> ent, ref ChemicalReactionOccurredEvent args)
    {
        var flags = args.Reaction.ReactionFlags;
        if (flags == StoriesChemReactionFlags.None || flags == StoriesChemReactionFlags.Calm)
            return;

        var uid = ent.Owner;
        var config = args.Reaction.StoriesConfig ?? DefaultConfig;

        var payload = new Solution();
        foreach (var prod in args.Reaction.Products)
        {
            payload.AddReagent(prod.Key, prod.Value * args.UnitReactions);
        }

        if ((flags & StoriesChemReactionFlags.Bubbling) != 0)
        {
            if (_random.Prob(config.BubblingProbability))
                _popup.PopupEntity(Loc.GetString(config.BubblingPopup), uid, PopupType.MediumCaution);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg"), uid);

            _bubblingTargets.Clear();
            _lookup.GetEntitiesInRange(uid, config.BubblingSplashRadius, _bubblingTargets);
            foreach (var e in _bubblingTargets)
            {
                if (e == uid) continue;
                var splashSol = payload.Clone();
                splashSol.ScaleSolution(config.BubblingSplashScale);
                _reactive.DoEntityReaction(e, splashSol, ReactionMethod.Touch);
            }
        }

        if ((flags & StoriesChemReactionFlags.Glowing) != 0)
        {
            if (_random.Prob(config.GlowingProbability))
                _popup.PopupEntity(Loc.GetString(config.GlowingPopup), uid, PopupType.Medium);
        }

        if ((flags & StoriesChemReactionFlags.Smoking) != 0)
        {
            _popup.PopupEntity(Loc.GetString(config.SmokingStartPopup), uid, PopupType.LargeCaution);
            _audio.PlayPvs(config.SmokingStartSound, uid);

            var delayed = EnsureComp<StoriesDelayedReactionComponent>(uid);
            delayed.PendingReactions.Add(new PendingReaction
            {
                Flag = StoriesChemReactionFlags.Smoking,
                TriggerAt = _timing.CurTime + TimeSpan.FromSeconds(config.SmokingDelay),
                Payload = payload.Clone(),
                ReactionId = args.Reaction.ID
            });
        }

        if ((flags & StoriesChemReactionFlags.Fire) != 0)
        {
            _popup.PopupEntity(Loc.GetString(config.FireStartPopup), uid, PopupType.LargeCaution);
            _audio.PlayPvs(config.FireStartSound, uid);

            var delayed = EnsureComp<StoriesDelayedReactionComponent>(uid);
            delayed.PendingReactions.Add(new PendingReaction
            {
                Flag = StoriesChemReactionFlags.Fire,
                TriggerAt = _timing.CurTime + TimeSpan.FromSeconds(config.FireDelay),
                Payload = payload.Clone(),
                ReactionId = args.Reaction.ID
            });
        }

        if ((flags & StoriesChemReactionFlags.Endothermic) != 0)
        {
            var sol = ent.Comp.Solution;
            var heatCap = sol.GetHeatCapacity(_proto);
            if (heatCap > 0)
            {
                sol.Temperature = Math.Max(0f, sol.Temperature - config.EndothermicTempDrop);
                _solution.UpdateChemicals(ent);
            }

            if (_random.Prob(config.EndothermicProbability))
                _popup.PopupEntity(Loc.GetString(config.EndothermicPopup), uid, PopupType.Medium);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<StoriesDelayedReactionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            for (var i = comp.PendingReactions.Count - 1; i >= 0; i--)
            {
                var pending = comp.PendingReactions[i];
                if (curTime < pending.TriggerAt)
                    continue;

                comp.PendingReactions.RemoveAt(i);
                ExecuteDelayedReaction(uid, pending);
            }

            if (comp.PendingReactions.Count == 0)
                RemCompDeferred<StoriesDelayedReactionComponent>(uid);
        }
    }

    private void ExecuteDelayedReaction(EntityUid uid, PendingReaction pending)
    {
        if (!_proto.TryIndex<ReactionPrototype>(pending.ReactionId, out var reactionProto))
            return;

        var config = reactionProto.StoriesConfig ?? DefaultConfig;
        bool isClosed = false;

        var containerUid = _transform.GetParentUid(uid);
        if (TryComp<OpenableComponent>(containerUid, out var openableContainer) && !openableContainer.Opened)
            isClosed = true;
        else if (TryComp<OpenableComponent>(uid, out var openable) && !openable.Opened)
            isClosed = true;

        if (pending.Flag == StoriesChemReactionFlags.Smoking)
        {
            if (isClosed)
            {
                _popup.PopupEntity(Loc.GetString(config.SmokingPreventedPopup), uid, PopupType.Medium);
                return;
            }

            _audio.PlayPvs(config.SmokeSound, uid);

            var smokeUid = Spawn(config.SmokeEntity, Transform(uid).Coordinates);
            if (_solution.EnsureSolutionEntity(smokeUid, SmokeComponent.SolutionName, out var smokeSolnEnt))
            {
                var scaledPayload = pending.Payload.Clone();
                scaledPayload.ScaleSolution(config.SmokeVolumeScale);
                _solution.TryAddSolution(smokeSolnEnt.Value, scaledPayload);
            }
            if (TryComp<SmokeComponent>(smokeUid, out var smoke))
            {
                smoke.SpreadAmount = Math.Max(1, (int)(pending.Payload.Volume.Float() / config.SmokeSpreadDivisor));
                Dirty(smokeUid, smoke);
            }
        }
        else if (pending.Flag == StoriesChemReactionFlags.Fire)
        {
            _flammable.SpawnFireDiamond(config.FireEntity, Transform(uid).Coordinates, config.FireRadius, config.FireIntensity, config.FireDuration);
        }
    }
}
