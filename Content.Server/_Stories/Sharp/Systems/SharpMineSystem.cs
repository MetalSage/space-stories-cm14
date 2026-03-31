using System;
using System.Collections.Generic;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Tools;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._Stories.Sharp;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Explosion.Components;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Content.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Sharp;

public sealed class SharpMineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly List<EntityUid> _toDetonate = new();
    private readonly List<EntityUid> _toDisarm = new();
    private readonly List<(EntityUid OldMine, string NewProto)> _toReplace = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpMineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SharpMineComponent, ExplosionReceivedEvent>(OnExplosionReceived);
        SubscribeLocalEvent<SharpMineComponent, StartCollideEvent>(OnMineStartCollide);
        SubscribeLocalEvent<SharpMineComponent, AttackedEvent>(OnMineAttacked);
        SubscribeLocalEvent<SharpMineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SharpMineComponent, SharpMineDisarmDoAfterEvent>(OnMineDisarmDoAfter);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHitMine);
    }

    private void OnMapInit(EntityUid uid, SharpMineComponent comp, ref MapInitEvent args)
    {
        EnsureComp<SharpMineRuntimeComponent>(uid).SpawnTime = _timing.CurTime;
    }

    private void OnExplosionReceived(Entity<SharpMineComponent> mine, ref ExplosionReceivedEvent args)
    {
        if (args.Damage.GetTotal() <= 0 || TerminatingOrDeleted(mine.Owner))
            return;

        Detonate(mine.Owner);
    }

    private void OnProjectileHitMine(Entity<ProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(args.Target))
            return;

        if (TryComp<SharpStickyDartComponent>(projectile.Owner, out _) &&
            (!TryComp<SharpMineComponent>(args.Target, out var targetMine) ||
             !TryComp<SharpMineRuntimeComponent>(args.Target, out var rt) ||
             (_timing.CurTime - rt.SpawnTime).TotalSeconds < targetMine.StickyDartGracePeriod))
        {
            return;
        }

        if (HasComp<SharpMineComponent>(args.Target))
            Detonate(args.Target);
    }

    private void OnMineStartCollide(Entity<SharpMineComponent> mine, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(mine.Owner))
            return;

        if (TryComp<SharpStickyDartComponent>(args.OtherEntity, out _))
            return;

        if (!TryComp<DamageOnCollideComponent>(args.OtherEntity, out var damageOnCollide))
            return;

        if (damageOnCollide.Acidic ||
            damageOnCollide.Fire ||
            HasTriggerDamage(damageOnCollide.Damage, mine.Comp.DetonateOnDamage))
        {
            Detonate(mine.Owner);
        }
    }

    private void OnMineAttacked(Entity<SharpMineComponent> mine, ref AttackedEvent args)
    {
        if (TerminatingOrDeleted(mine.Owner))
            return;

        Detonate(mine.Owner);
    }

    private void OnInteractUsing(Entity<SharpMineComponent> mine, ref InteractUsingEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(mine.Owner) || !HasComp<MultitoolComponent>(args.Used))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            TimeSpan.FromSeconds(3),
            new SharpMineDisarmDoAfterEvent(),
            mine,
            target: mine,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnMineDisarmDoAfter(Entity<SharpMineComponent> mine, ref SharpMineDisarmDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(mine.Owner))
            return;

        if (args.Used is not { } used || !HasComp<MultitoolComponent>(used))
        {
            return;
        }

        args.Handled = true;
        DisarmMine(mine);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _toDetonate.Clear();
        _toDisarm.Clear();
        _toReplace.Clear();

        var query = EntityQueryEnumerator<SharpMineComponent, TransformComponent, SharpMineRuntimeComponent>();
        while (query.MoveNext(out var uid, out var mine, out var xform, out var rt))
        {
            if (TerminatingOrDeleted(uid)) continue;

            if (IsTouchingAcidOrFire(uid, mine))
            {
                _toDetonate.Add(uid);
                continue;
            }

            var age = (_timing.CurTime - rt.SpawnTime).TotalSeconds;

            if (age >= mine.MaxLifespan)
            {
                _toDisarm.Add(uid);
                continue;
            }

            var desiredLevel = 1 + (int)(age / mine.LevelUpInterval);
            if (desiredLevel > mine.MaxLevel)
                desiredLevel = mine.MaxLevel;

            if (desiredLevel != mine.Level)
            {
                var protoId = MetaData(uid).EntityPrototype?.ID;
                if (protoId != null && protoId.EndsWith(mine.Level.ToString()))
                {
                    var newProto = protoId.Substring(0, protoId.Length - 1) + desiredLevel.ToString();
                    _toReplace.Add((uid, newProto));
                    continue;
                }
                mine.Level = desiredLevel;
                Dirty(uid, mine);
            }

            _nearby.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, mine.TriggerRadius, _nearby);

            var enemyFound = false;
            foreach (var other in _nearby)
            {
                if (!CanTriggerMine((uid, mine), other))
                    continue;

                enemyFound = true;
                break;
            }

            if (enemyFound) _toDetonate.Add(uid);
        }

        foreach (var (oldMine, newProto) in _toReplace)
        {
            if (TerminatingOrDeleted(oldMine)) continue;
            var coords = Transform(oldMine).Coordinates;
            var oldRt = CompOrNull<SharpMineRuntimeComponent>(oldMine);
            var newMine = Spawn(newProto, coords);
            if (oldRt != null) EnsureComp<SharpMineRuntimeComponent>(newMine).SpawnTime = oldRt.SpawnTime;
            QueueDel(oldMine);
        }

        foreach (var uid in _toDetonate)
        {
            if (!TerminatingOrDeleted(uid)) Detonate(uid);
        }

        foreach (var uid in _toDisarm)
        {
            if (!TerminatingOrDeleted(uid) &&
                TryComp(uid, out SharpMineComponent? mine))
            {
                DisarmMine((uid, mine));
            }
        }
    }

    private void Detonate(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var explosionRadius = TryComp(uid, out SharpMineComponent? mine)
            ? mine.ExplosionRadius
            : 0f;

        if (HasComp<Content.Shared._RMC14.Atmos.TileFireOnTriggerComponent>(uid))
        {
            var fireEv = new Content.Shared._RMC14.Explosion.RMCTriggerEvent();
            RaiseLocalEvent(uid, ref fireEv);
        }

        if (TryComp<ExplosiveComponent>(uid, out var explosive))
            _explosion.TriggerExplosive(uid, explosive, delete: true, radius: explosionRadius);
        else
            QueueDel(uid);
    }

    private void DisarmMine(Entity<SharpMineComponent> mine)
    {
        if (mine.Comp.DisarmSpawnProto is { } spawnProto)
            Spawn(spawnProto, Transform(mine).Coordinates);

        QueueDel(mine);
    }

    private bool TerminatingOrDeleted(EntityUid uid)
    {
        return Deleted(uid) || MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating;
    }

    private static bool HasTriggerDamage(DamageSpecifier damage, DamageSpecifier triggerDamage)
    {
        foreach (var (damageType, amount) in triggerDamage.DamageDict)
        {
            if (amount <= 0)
                continue;

            if (damage.DamageDict.TryGetValue(damageType, out var actual) && actual > 0)
                return true;
        }

        return false;
    }

    private bool IsTouchingAcidOrFire(EntityUid uid, SharpMineComponent mine)
    {
        foreach (var other in _physics.GetEntitiesIntersectingBody(uid, (int)CollisionGroup.AllMask))
        {
            if (other == uid || TerminatingOrDeleted(other))
                continue;

            if (HasComp<RMCIgniteOnCollideComponent>(other))
                return true;

            if (!TryComp<DamageOnCollideComponent>(other, out var damageOnCollide))
                continue;

            if (damageOnCollide.Acidic ||
                damageOnCollide.Fire ||
                HasTriggerDamage(damageOnCollide.Damage, mine.DetonateOnDamage))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanTriggerMine(Entity<SharpMineComponent> mine, EntityUid target)
    {
        if (target == mine.Owner || TerminatingOrDeleted(target))
            return false;

        if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
            return false;

        if (!mine.Comp.IgnoreAnyIff)
            return true;

        var ev = new GetIFFFactionEvent(SlotFlags.IDCARD, new());
        RaiseLocalEvent(target, ref ev);
        return ev.Factions.Count == 0;
    }
}
