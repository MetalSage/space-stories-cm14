using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._Stories.Sharp;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Sharp;

public sealed class SharpMineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GunIFFSystem _gunIFF = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const CollisionGroup HazardCollisionMask =
        CollisionGroup.BulletImpassable | CollisionGroup.MidImpassable | CollisionGroup.Impassable;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _touching = new();
    private readonly List<EntityUid> _toDetonate = new();
    private readonly List<EntityUid> _toDisarm = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharpMineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SharpMineComponent, DamageChangedEvent>(OnMineDamageChanged);
        SubscribeLocalEvent<SharpMineComponent, ExplosionReceivedEvent>(OnExplosionReceived);
        SubscribeLocalEvent<SharpMineComponent, StartCollideEvent>(OnMineStartCollide);
        SubscribeLocalEvent<SharpMineComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SharpMineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SharpMineComponent, SharpMineDisarmDoAfterEvent>(OnMineDisarmDoAfter);
    }

    private void OnMapInit(Entity<SharpMineComponent> mine, ref MapInitEvent args)
    {
        mine.Comp.ActivateAt = _timing.CurTime + TimeSpan.FromSeconds(mine.Comp.ActivationDelay);
        mine.Comp.Activated = false;
        mine.Comp.Detonated = false;

        ClampConfiguredLevel(mine);
        SetVisualState(mine, SharpMineState.Arming);
    }

    private void OnExplosionReceived(Entity<SharpMineComponent> mine, ref ExplosionReceivedEvent args)
    {
        if (args.Damage.GetTotal() <= 0 || TerminatingOrDeleted(mine.Owner))
            return;

        Detonate(mine);
    }

    private void OnMineDamageChanged(Entity<SharpMineComponent> mine, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || TerminatingOrDeleted(mine.Owner))
            return;

        if (!TryComp<MaxDamageComponent>(mine, out var maxDamage) ||
            args.Damageable.TotalDamage < maxDamage.Max)
            return;

        Detonate(mine);
    }

    private void OnMineStartCollide(Entity<SharpMineComponent> mine, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(mine.Owner))
            return;

        if (IsHazard(mine.Comp, args.OtherEntity))
            Detonate(mine);
    }

    private void OnInteractUsing(Entity<SharpMineComponent> mine, ref InteractUsingEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(mine.Owner))
            return;

        args.Handled = TryStartMineDisarm(mine, args.User, args.Used);
    }

    private void OnInteractHand(Entity<SharpMineComponent> mine, ref InteractHandEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(mine.Owner))
            return;

        args.Handled = TryStartMineDisarm(mine, args.User, null);
    }

    private bool TryStartMineDisarm(Entity<SharpMineComponent> mine, EntityUid user, EntityUid? used)
    {
        var doAfter = new DoAfterArgs(EntityManager,
            user,
            TimeSpan.FromSeconds(mine.Comp.DisarmDuration),
            new SharpMineDisarmDoAfterEvent(),
            mine,
            target: mine,
            used: used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnMineDisarmDoAfter(Entity<SharpMineComponent> mine, ref SharpMineDisarmDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(mine.Owner))
            return;

        args.Handled = true;
        DisarmMine(mine);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _toDetonate.Clear();
        _toDisarm.Clear();

        var query = EntityQueryEnumerator<SharpMineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mine, out var xform))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (IsTouchingHazard((uid, mine)))
            {
                _toDetonate.Add(uid);
                continue;
            }

            var visualDirty = false;

            if (!mine.Activated)
            {
                if (_timing.CurTime < mine.ActivateAt)
                    continue;

                mine.Activated = true;
                visualDirty = true;
            }

            var age = (_timing.CurTime - mine.ActivateAt).TotalSeconds;

            if (age >= mine.MaxLifespan)
            {
                _toDisarm.Add(uid);
                continue;
            }

            var desiredLevel = GetDesiredLevel((uid, mine), age);
            if (desiredLevel != mine.Level)
            {
                mine.Level = desiredLevel;
                Dirty(uid, mine);
                visualDirty = true;
            }

            if (visualDirty)
                SetVisualState((uid, mine), LevelToState(mine.Level));

            _nearby.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, mine.TriggerRadius, _nearby);

            foreach (var other in _nearby)
            {
                if (!CanTriggerMine((uid, mine), other))
                    continue;

                _toDetonate.Add(uid);
                break;
            }
        }

        foreach (var uid in _toDetonate)
        {
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out SharpMineComponent? mine))
                Detonate((uid, mine));
        }

        foreach (var uid in _toDisarm)
        {
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out SharpMineComponent? mine))
                DisarmMine((uid, mine));
        }
    }

    private void Detonate(Entity<SharpMineComponent> mine)
    {
        if (mine.Comp.Detonated || TerminatingOrDeleted(mine.Owner))
            return;

        mine.Comp.Detonated = true;

        if (TryGetLevelEntry(mine, out var levels, out var entry))
        {
            TriggerFireLevel(mine.Owner, levels, entry);
            TriggerExplosionLevel(mine, levels, entry);
        }

        QueueDel(mine.Owner);
    }

    private void TriggerFireLevel(EntityUid uid, SharpMineLevelsComponent levels, SharpMineLevel entry)
    {
        if (entry.TileFireSpawn is not { } spawn)
            return;

        if (entry.TileFireRange is not { } range)
        {
            Log.Warning($"SHARP mine {ToPrettyString(uid)} has tile fire level without range.");
            return;
        }

        var coords = _transform.GetMoverCoordinates(uid);
        _audio.PlayPvs(levels.TileFireSound, coords);

        var tile = coords.SnapToGrid(EntityManager, _map);
        _flammable.SpawnFireDiamond(spawn, tile, range, entry.TileFireIntensity, entry.TileFireDuration);
    }

    private void TriggerExplosionLevel(Entity<SharpMineComponent> mine, SharpMineLevelsComponent levels, SharpMineLevel entry)
    {
        if (levels.ExplosionType is not { } type)
            return;

        if (entry.ExplosiveMaxIntensity is not { } maxIntensity ||
            entry.ExplosiveIntensitySlope is not { } slope)
        {
            Log.Warning($"SHARP mine {ToPrettyString(mine.Owner)} has explosion level without max intensity or slope.");
            return;
        }

        if (entry.ExplosiveTotalIntensity is not { } totalIntensity || totalIntensity <= 0f)
        {
            Log.Warning($"SHARP mine {ToPrettyString(mine.Owner)} has explosion level without total intensity.");
            return;
        }

        _explosion.QueueExplosion(mine.Owner,
            type,
            totalIntensity,
            slope,
            maxIntensity,
            levels.ExplosionTileBreakScale,
            levels.ExplosionMaxTileBreak,
            levels.ExplosionCanCreateVacuum);

        var ev = new CMExplosiveTriggeredEvent();
        RaiseLocalEvent(mine.Owner, ref ev);
    }

    private void DisarmMine(Entity<SharpMineComponent> mine)
    {
        if (mine.Comp.DisarmSpawnProto is { } spawnProto)
            Spawn(spawnProto, Transform(mine).Coordinates);

        QueueDel(mine.Owner);
    }

    private void ClampConfiguredLevel(Entity<SharpMineComponent> mine)
    {
        var maxLevel = GetConfiguredMaxLevel(mine);
        var level = Math.Clamp(mine.Comp.Level, 1, maxLevel);

        if (level == mine.Comp.Level)
            return;

        Log.Warning($"Clamped SHARP mine {ToPrettyString(mine.Owner)} level from {mine.Comp.Level} to {level}.");
        mine.Comp.Level = level;
        Dirty(mine);
    }

    private int GetDesiredLevel(Entity<SharpMineComponent> mine, double age)
    {
        var maxLevel = GetConfiguredMaxLevel(mine);
        if (maxLevel <= 1)
            return 1;

        if (mine.Comp.LevelUpInterval <= 0f)
            return maxLevel;

        return Math.Min(1 + (int)(age / mine.Comp.LevelUpInterval), maxLevel);
    }

    private int GetConfiguredMaxLevel(Entity<SharpMineComponent> mine)
    {
        if (!TryComp<SharpMineLevelsComponent>(mine, out var levels))
            return 1;

        return Math.Max(1, Math.Min(mine.Comp.MaxLevel, levels.Levels.Count));
    }

    private bool TryGetLevelEntry(
        Entity<SharpMineComponent> mine,
        out SharpMineLevelsComponent levels,
        out SharpMineLevel entry)
    {
        levels = default!;
        entry = default!;

        if (!TryComp<SharpMineLevelsComponent>(mine, out var levelsComp))
        {
            Log.Warning($"SHARP mine {ToPrettyString(mine.Owner)} has no level configuration.");
            return false;
        }

        if (levelsComp.Levels.Count == 0)
        {
            Log.Warning($"SHARP mine {ToPrettyString(mine.Owner)} has empty level configuration.");
            return false;
        }

        levels = levelsComp;
        var level = Math.Clamp(mine.Comp.Level, 1, levels.Levels.Count);
        entry = levels.Levels[level - 1];
        return true;
    }

    private bool IsTouchingHazard(Entity<SharpMineComponent> mine)
    {
        _touching.Clear();
        _lookup.GetEntitiesIntersecting(mine.Owner, _touching, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sensors);

        foreach (var other in _touching)
        {
            if (other == mine.Owner || TerminatingOrDeleted(other))
                continue;

            if (!HasHazardCollisionLayer(other))
                continue;

            if (IsHazard(mine.Comp, other))
                return true;
        }

        return false;
    }

    private bool HasHazardCollisionLayer(EntityUid uid)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return false;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (((CollisionGroup) fixture.CollisionLayer & HazardCollisionMask) != 0)
                return true;
        }

        return false;
    }

    private bool IsHazard(SharpMineComponent mine, EntityUid other)
    {
        if (HasComp<SharpStickyDartComponent>(other))
            return false;

        if (HasComp<SharpMineTriggerGasComponent>(other) || HasComp<RMCIgniteOnCollideComponent>(other))
            return true;

        if (!TryComp<DamageOnCollideComponent>(other, out var damageOnCollide))
            return false;

        return damageOnCollide.Acidic ||
            damageOnCollide.Fire ||
            HasTriggerDamage(damageOnCollide.Damage, mine.DetonateOnDamage);
    }

    private void SetVisualState(Entity<SharpMineComponent> mine, SharpMineState state)
    {
        if (mine.Comp.AppearanceState == state)
            return;

        mine.Comp.AppearanceState = state;
        _appearance.SetData(mine, SharpMineVisuals.State, state);
    }

    private static SharpMineState LevelToState(int level) => level switch
    {
        <= 1 => SharpMineState.Level1,
        2 => SharpMineState.Level2,
        3 => SharpMineState.Level3,
        _ => SharpMineState.Level4,
    };

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

    private bool CanTriggerMine(Entity<SharpMineComponent> mine, EntityUid target)
    {
        if (target == mine.Owner || TerminatingOrDeleted(target))
            return false;

        if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
            return false;

        if (!mine.Comp.BlockFriendlyFire || mine.Comp.IffFactions.Count == 0)
            return true;

        foreach (var faction in mine.Comp.IffFactions)
        {
            if (_gunIFF.IsInFaction(target, faction))
                return false;
        }

        return true;
    }
}
