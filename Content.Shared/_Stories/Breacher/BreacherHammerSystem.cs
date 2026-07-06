using System.Numerics;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Tether;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared._Stories.Breacher.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Breacher;

public sealed class BreacherHammerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreacherHammerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BreacherHammerComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<BreacherHammerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<BreacherHammerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BreacherHammerComponent, BreacherWallBreachDoAfterEvent>(OnWallBreachDoAfter);

        // Tether-breaking conditions that aren't just "dragged too far" (handled in Update):
        // swallowed by a xeno, or the armor that grants access gets taken off.
        SubscribeLocalEvent<DevouredComponent, ComponentAdd>(OnOwnerDevoured);
        SubscribeLocalEvent<BreacherArmorComponent, ClothingGotUnequippedEvent>(OnOwnerArmorUnequipped);
    }

    private void OnOwnerDevoured(Entity<DevouredComponent> ent, ref ComponentAdd args)
    {
        BreakTethersOwnedBy(ent.Owner, "stories-breacher-hammer-tether-break-devoured");
    }

    private void OnOwnerArmorUnequipped(Entity<BreacherArmorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        BreakTethersOwnedBy(args.Wearer, "stories-breacher-hammer-tether-break-armor");
    }

    private void BreakTethersOwnedBy(EntityUid owner, string popupLocId)
    {
        var query = EntityQueryEnumerator<BreacherHammerComponent>();
        while (query.MoveNext(out var uid, out var hammer))
        {
            if (hammer.TetherOwner != owner)
                continue;

            hammer.TetherOwner = null;
            Dirty(uid, hammer);
            RemCompDeferred<RMCTetherComponent>(uid);

            if (TryComp(uid, out PhysicsComponent? physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

            _popup.PopupClient(Loc.GetString(popupLocId), owner, owner);
        }
    }

    /// <summary>
    ///     Deliberately breaching a wall (as opposed to just swinging at it in combat) -- click the
    ///     hammer on a wall/girder to start a timed demolition: reinforced walls take longest,
    ///     regular walls collapse to a girder faster, and a bare girder goes down quickest of all.
    /// </summary>
    private void OnAfterInteract(Entity<BreacherHammerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<BreacherWhitelistComponent>(args.User))
            return;

        var duration = GetBreachDuration(target);
        if (duration is not { } delay)
            return;

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, delay,
            new BreacherWallBreachDoAfterEvent(GetNetEntity(target)), ent, target, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupPredicted(
                Loc.GetString("stories-breacher-hammer-breach-start-self"),
                Loc.GetString("stories-breacher-hammer-breach-start-others", ("user", args.User)),
                args.User,
                args.User);
        }
    }

    private void OnWallBreachDoAfter(Entity<BreacherHammerComponent> ent, ref BreacherWallBreachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!_net.IsServer)
            return;

        var target = GetEntity(args.Target);
        if (Deleted(target))
            return;

        var burst = GetBreachBurstDamage(target);
        if (burst is not { } damage)
            return;

        _damageable.TryChangeDamage(target, damage, origin: args.User);
        _audio.PlayPvs(new SoundCollectionSpecifier("MetalSlam"), target);
        _popup.PopupEntity(Loc.GetString("stories-breacher-hammer-breach-finish"), args.User, args.User);
    }

    /// <summary>
    ///     Detects walls generically via the "Wall" tag (used by every RMC14 wall variant --
    ///     CMWallMetal, CMWallReinforced, CMWallJungle, etc.), splitting reinforced from regular by
    ///     ID substring since there's no shared marker distinguishing them. Girders are matched by
    ///     exact ID (CMGirder/CMGirderReinforced), since RMC14 doesn't use the vanilla "Girder".
    /// </summary>
    private TimeSpan? GetBreachDuration(EntityUid target)
    {
        var id = MetaData(target).EntityPrototype?.ID;
        if (id is "CMGirder" or "CMGirderReinforced")
            return TimeSpan.FromSeconds(3);

        if (!_tag.HasTag(target, "Wall"))
            return null;

        return id is not null && id.Contains("Reinforced")
            ? TimeSpan.FromSeconds(15)
            : TimeSpan.FromSeconds(7.5);
    }

    /// <summary>
    ///     A one-shot Structural damage burst sized to land past the "turn into girder"/"destroy"
    ///     threshold for that specific prototype without also blowing past the *next* threshold up
    ///     (which would skip the girder stage and destroy the wall outright, or skip debris drops).
    ///     RMC14's StructuralMarine/RMCGirder modifier sets don't reduce Structural damage at all,
    ///     so these are just the real thresholds plus a small safety margin, no coefficient math.
    /// </summary>
    private DamageSpecifier? GetBreachBurstDamage(EntityUid target)
    {
        var id = MetaData(target).EntityPrototype?.ID;

        float? raw = id switch
        {
            "CMGirder" or "CMGirderReinforced" => 75f, // both break at 50, next threshold is 125/500
            _ when _tag.HasTag(target, "Wall") && id is not null && id.Contains("Reinforced")
                => 9100f, // RMCBaseWallReinforced: girder at 9000, destroy at 9500
            _ when _tag.HasTag(target, "Wall") => 3050f, // CMWallMetal: girder at 3000, destroy at 3125
            _ => null,
        };

        if (raw is not { } value)
            return null;

        return new DamageSpecifier { DamageDict = new() { ["Structural"] = value } };
    }

    private void OnGetVerbs(Entity<BreacherHammerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!HasComp<BreacherWhitelistComponent>(args.User))
            return;

        var user = args.User;
        var tethered = ent.Comp.TetherOwner == user;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(tethered ? "stories-breacher-hammer-untether" : "stories-breacher-hammer-tether"),
            Priority = -1,
            Act = () =>
            {
                if (tethered)
                {
                    ent.Comp.TetherOwner = null;
                    RemCompDeferred<RMCTetherComponent>(ent.Owner);
                    Dirty(ent);
                    _popup.PopupClient(Loc.GetString("stories-breacher-hammer-untether-self"), user, user);
                }
                else
                {
                    ent.Comp.TetherOwner = user;
                    Dirty(ent);
                    var tether = EnsureComp<RMCTetherComponent>(ent.Owner);
                    tether.TetherOrigin = user;
                    tether.TetherWidth = 0.2f;
                    Dirty(ent.Owner, tether);
                    _popup.PopupClient(Loc.GetString("stories-breacher-hammer-tether-self"), user, user);
                }
            },
        });
    }

    private void OnAttemptMelee(Entity<BreacherHammerComponent> ent, ref AttemptMeleeEvent args)
    {
        if (HasComp<BreacherWhitelistComponent>(args.User))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("stories-breacher-hammer-untrained");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var stacksQuery = EntityQueryEnumerator<BreacherHammerStacksComponent>();
        while (stacksQuery.MoveNext(out var uid, out var stacks))
        {
            if (_timing.CurTime < stacks.NextDecay)
                continue;

            if (stacks.LastHitAt + stacks.IncrementGraceTime > _timing.CurTime)
                continue;

            stacks.StackCount--;
            stacks.NextDecay = _timing.CurTime + stacks.DecayEvery;
            Dirty(uid, stacks);

            if (stacks.StackCount <= 0)
                RemCompDeferred<BreacherHammerStacksComponent>(uid);
        }

        if (!_net.IsServer)
            return;

        var tetherQuery = EntityQueryEnumerator<BreacherHammerComponent>();
        while (tetherQuery.MoveNext(out var uid, out var hammer))
        {
            if (hammer.TetherOwner is not { } owner)
                continue;

            if (!Exists(owner) || TerminatingOrDeleted(owner))
            {
                hammer.TetherOwner = null;
                RemCompDeferred<RMCTetherComponent>(uid);
                Dirty(uid, hammer);
                continue;
            }

            // Skip pulling while the owner is the one actually holding it -- nothing to chase.
            if (_hands.IsHolding(owner, uid))
            {
                if (TryComp(uid, out PhysicsComponent? heldPhysics))
                    _physics.SetLinearVelocity(uid, Vector2.Zero, body: heldPhysics);
                continue;
            }

            if (!TryComp(uid, out PhysicsComponent? physics))
                continue;

            var hammerCoords = _transform.ToMapCoordinates(Transform(uid).Coordinates);
            var ownerCoords = _transform.ToMapCoordinates(Transform(owner).Coordinates);

            if (hammerCoords.MapId != ownerCoords.MapId)
                continue;

            var offset = ownerCoords.Position - hammerCoords.Position;
            var distance = offset.Length();

            if (distance > hammer.TetherBreakDistance)
            {
                hammer.TetherOwner = null;
                Dirty(uid, hammer);
                RemCompDeferred<RMCTetherComponent>(uid);
                _popup.PopupEntity(Loc.GetString("stories-breacher-hammer-tether-break-distance"), owner, owner);
                continue;
            }

            // Close enough (in the owner's hand range) -- stop, don't jitter in place.
            if (distance <= hammer.TetherStopDistance)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                continue;
            }

            // The farther away it's been dragged, the faster it reels back in, capped so it
            // doesn't feel like teleporting even from the far end of TetherMaxDistance.
            var speed = MathF.Min(hammer.TetherMaxSpeed, hammer.TetherBaseSpeed * (distance / hammer.TetherMaxDistance));
            _physics.SetLinearVelocity(uid, offset.Normalized() * speed, body: physics);
        }
    }

    private void OnMeleeHit(Entity<BreacherHammerComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var wielded = TryComp(ent.Owner, out WieldableComponent? wieldable) && wieldable.Wielded;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<XenoComponent>(target))
                continue;

            var instant = ent.Comp.InstantOnIncapacitated && _mobState.IsIncapacitated(target);
            ApplyHit(target, args.User, wielded, instant);
        }
    }

    private void ApplyHit(EntityUid target, EntityUid attacker, bool wielded, bool instant)
    {
        var stacks = EnsureComp<BreacherHammerStacksComponent>(target);
        stacks.StackCount = Math.Min(Math.Max(stacks.OneHandedHitsForKnockdown, stacks.TwoHandedGuaranteedHits), stacks.StackCount + 1);
        stacks.LastHitAt = _timing.CurTime;
        stacks.NextDecay = _timing.CurTime + stacks.DecayEvery;
        Dirty(target, stacks);

        var trigger = instant;

        if (!trigger)
        {
            if (wielded)
            {
                var steps = Math.Max(1, stacks.TwoHandedGuaranteedHits - 1);
                var progress = (stacks.StackCount - 1) / (float) steps;
                var chance = MathF.Min(1f, stacks.TwoHandedBaseChance +
                    MathF.Pow(progress, stacks.TwoHandedCurvePower) * (1f - stacks.TwoHandedBaseChance));

                // Small, quick castes (runner and similar) are noticeably easier to knock down --
                // big castes (crusher, ravager, boiler) are handled by the superslow branch below
                // and don't touch this chance at all, so they're unaffected either way.
                if (TryComp(target, out RMCSizeComponent? smallSizeComp) && smallSizeComp.Size <= RMCSizes.SmallXeno)
                    chance = MathF.Min(1f, chance * stacks.SmallXenoChanceMultiplier);

                trigger = _random.Prob(chance);
            }
            else
            {
                trigger = stacks.StackCount >= stacks.OneHandedHitsForKnockdown;
            }
        }

        if (!trigger)
            return;

        // Everything past this point must be server-authoritative only -- the chance roll above
        // runs in predicted Shared code, so the client and server can disagree on whether it
        // proc'd. Gating here stops the client from showing the effect on a roll the server didn't
        // actually confirm.
        if (!_net.IsServer)
            return;

        // Clear feedback that the chance actually proc'd -- otherwise a superslow on a big xeno
        // is easy to mistake for some other random slow effect.
        _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
        Spawn("EffectSparks", Transform(target).Coordinates);
        _audio.PlayPvs(stacks.ProcSound, target);
        _popup.PopupEntity(
            Loc.GetString("stories-breacher-hammer-proc-self"),
            attacker,
            attacker);

        if (stacks.ProcBonusDamage.DamageDict.Count > 0)
            _damageable.TryChangeDamage(target, stacks.ProcBonusDamage, origin: attacker);

        var isBig = TryComp(target, out RMCSizeComponent? sizeComp) && sizeComp.Size >= RMCSizes.Big;

        if (isBig)
        {
            // Large/immobile xenos (queen, king, etc.) are too heavy to knock down or throw --
            // they just get slowed to a crawl instead, matching the source's mob_size check.
            _slow.TrySuperSlowdown(target, stacks.BigXenoSuperSlowDuration);
        }
        else
        {
            _stun.TryKnockdown(target, stacks.KnockdownDuration, true);
            _stun.TryStun(attacker, stacks.AttackerStunDuration, true);

            var attackerCoords = Transform(attacker).Coordinates;
            var targetCoords = Transform(target).Coordinates;
            var direction = _transform.ToMapCoordinates(targetCoords).Position - _transform.ToMapCoordinates(attackerCoords).Position;
            if (direction != System.Numerics.Vector2.Zero)
                _throwing.TryThrow(target, direction, stacks.ThrowStrength, attacker);
        }

        RemCompDeferred<BreacherHammerStacksComponent>(target);
    }
}

[Serializable, NetSerializable]
public sealed partial class BreacherWallBreachDoAfterEvent : DoAfterEvent
{
    public readonly NetEntity Target;

    public BreacherWallBreachDoAfterEvent(NetEntity target)
    {
        Target = target;
    }

    public override DoAfterEvent Clone() => this;
}
