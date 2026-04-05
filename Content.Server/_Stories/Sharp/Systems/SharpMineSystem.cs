using System;
using System.Collections.Generic;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Tools;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._Stories.Sharp;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Explosion.Components;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Toggleable;
using Robust.Shared.GameObjects;
using Content.Shared.Physics;
using Robust.Shared.Prototypes;
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
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    private readonly HashSet<EntityUid> _nearby = new();
    private readonly List<EntityUid> _toDetonate = new();
    private readonly List<EntityUid> _toDisarm = new();
    private readonly List<(EntityUid OldMine, string NewProto)> _toReplace = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _mineFactions = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _targetFactions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpMineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SharpMineComponent, DamageChangedEvent>(OnMineDamageChanged);
        SubscribeLocalEvent<SharpMineComponent, ExplosionReceivedEvent>(OnExplosionReceived);
        SubscribeLocalEvent<SharpMineComponent, StartCollideEvent>(OnMineStartCollide);
        SubscribeLocalEvent<SharpMineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SharpMineComponent, SharpMineDisarmDoAfterEvent>(OnMineDisarmDoAfter);
    }

    private void OnMapInit(EntityUid uid, SharpMineComponent comp, ref MapInitEvent args)
    {
        var runtime = EnsureComp<SharpMineRuntimeComponent>(uid);
        runtime.ActivateAt = _timing.CurTime + TimeSpan.FromSeconds(comp.ActivationDelay);
        runtime.Activated = false;
        UpdateMineAppearance(uid, runtime, false);
    }

    private void OnExplosionReceived(Entity<SharpMineComponent> mine, ref ExplosionReceivedEvent args)
    {
        if (args.Damage.GetTotal() <= 0 || TerminatingOrDeleted(mine.Owner))
            return;

        Detonate(mine.Owner);
    }

    private void OnMineDamageChanged(Entity<SharpMineComponent> mine, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || TerminatingOrDeleted(mine.Owner))
            return;

        if (!TryComp<MaxDamageComponent>(mine.Owner, out var maxDamage) ||
            args.Damageable.TotalDamage < maxDamage.Max)
            return;

        Detonate(mine.Owner);
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

    private void OnInteractUsing(Entity<SharpMineComponent> mine, ref InteractUsingEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(mine.Owner) || !HasComp<MultitoolComponent>(args.Used))
            return;

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

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnMineDisarmDoAfter(Entity<SharpMineComponent> mine, ref SharpMineDisarmDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(mine.Owner))
            return;

        if (args.Used is not { } used || !HasComp<MultitoolComponent>(used))
            return;

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

            if (!rt.Activated && _timing.CurTime >= rt.ActivateAt)
            {
                rt.Activated = true;
                UpdateMineAppearance(uid, rt, true);
            }
            else if (!rt.Activated)
            {
                var blinkInterval = mine.ArmingBlinkInterval;
                if (blinkInterval <= 0f)
                {
                    UpdateMineAppearance(uid, rt, true);
                    continue;
                }

                var armedVisual = (int) (_timing.CurTime.TotalSeconds / blinkInterval) % 2 == 0;
                UpdateMineAppearance(uid, rt, armedVisual);
            }

            // The mine only arms after a short delay; its 300s lifespan starts then.
            if (_timing.CurTime < rt.ActivateAt)
                continue;

            var age = (_timing.CurTime - rt.ActivateAt).TotalSeconds;

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
            var oldDamageable = CompOrNull<DamageableComponent>(oldMine);
            var newMine = Spawn(newProto, coords);
            if (oldRt != null)
            {
                var newRt = EnsureComp<SharpMineRuntimeComponent>(newMine);
                newRt.ActivateAt = oldRt.ActivateAt;
                newRt.Activated = oldRt.Activated;
                newRt.IffFactions.UnionWith(oldRt.IffFactions);
                UpdateMineAppearance(newMine, newRt, oldRt.AppearanceEnabled ?? oldRt.Activated);
            }

            if (oldDamageable != null &&
                oldDamageable.TotalDamage > 0 &&
                TryComp<DamageableComponent>(newMine, out var newDamageable))
            {
                _damageable.SetDamage(newMine, newDamageable, new DamageSpecifier(oldDamageable.Damage));
            }

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

    private void UpdateMineAppearance(EntityUid uid, SharpMineRuntimeComponent runtime, bool activated)
    {
        if (runtime.AppearanceEnabled is { } appearanceEnabled && appearanceEnabled == activated)
            return;

        runtime.AppearanceEnabled = activated;

        if (TryComp(uid, out AppearanceComponent? appearance))
            _appearance.SetData(uid, ToggleableVisuals.Enabled, activated, appearance);
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

        if (!mine.Comp.IgnoreAnyIff || !TryGetMineFactions(mine.Owner, _mineFactions))
            return true;

        var ev = new GetIFFFactionEvent(SlotFlags.IDCARD, _targetFactions);
        _targetFactions.Clear();
        RaiseLocalEvent(target, ref ev);

        foreach (var faction in _mineFactions)
        {
            if (ev.Factions.Contains(faction))
                return false;
        }

        return true;
    }

    private bool TryGetMineFactions(EntityUid mine, HashSet<EntProtoId<IFFFactionComponent>> factions)
    {
        factions.Clear();

        if (!TryComp<SharpMineRuntimeComponent>(mine, out var runtime) || runtime.IffFactions.Count == 0)
            return false;

        factions.UnionWith(runtime.IffFactions);
        return true;
    }
}
