using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared._RMC14.Xenonids.Projectile.Spit;
using Content.Shared._Stories.Breacher.Components;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Breacher;

public sealed class BreacherShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly SharedDirectionalAttackBlockSystem _directionalBlock = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Worn on the back: covered automatically via DamageModifyEvent's IInventoryRelayEvent relay.
        SubscribeLocalEvent<BreacherShieldComponent, DamageModifyEvent>(OnEquippedDamageModify);

        // Held in hand: hands aren't part of SlotFlags, so the relay above never reaches it --
        // subscribe on HandsComponent (present directly on whoever's taking the damage) and check
        // held items ourselves instead.
        SubscribeLocalEvent<HandsComponent, DamageModifyEvent>(OnHandsDamageModify);

        // Projectiles (bullets, xeno spit) resolve entirely through ProjectileHitEvent *before*
        // DamageModifyEvent ever fires -- setting Handled here skips damage AND anything else that
        // reacts to the same event (acid stacks, neurotoxin), not just the raw damage number.
        // Must run before the systems that apply those on-hit effects.
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit,
            before: new[] { typeof(XenoSpitSystem), typeof(SharedNeurotoxinSystem) });

        // "Raised" piggybacks on the existing Wieldable mechanic instead of a custom toggle --
        // free sound + wielded-inhand sprite swap from the engine, no bespoke animation needed.
        SubscribeLocalEvent<BreacherShieldComponent, ItemWieldedEvent>(OnWielded);
        SubscribeLocalEvent<BreacherShieldComponent, ItemUnwieldedEvent>(OnUnwielded);

        SubscribeLocalEvent<BreacherShieldComponent, DroppedEvent>(OnDropped);
    }

    private void OnDropped(Entity<BreacherShieldComponent> ent, ref DroppedEvent args)
    {
        // The shield isn't glued to the hand like the hammer -- instead, whenever it leaves the
        // wearer's hands for any reason (disarm, knockdown, a plain drop), it tries to snap
        // straight onto their back instead of hitting the floor.
        _inventory.TryEquip(args.User, ent.Owner, "back", predicted: true);
    }

    private void OnEquippedDamageModify(Entity<BreacherShieldComponent> ent, ref DamageModifyEvent args)
    {
        if (Transform(ent).ParentUid is not { Valid: true } wearer)
            return;

        if (args.Origin is not { } attacker)
            return;

        if (TryBlock(wearer, attacker, ent))
            args.Damage = new DamageSpecifier();
    }

    private void OnHandsDamageModify(Entity<HandsComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } attacker)
            return;

        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!TryComp(held, out BreacherShieldComponent? shield))
                continue;

            if (TryBlock(ent.Owner, attacker, (held, shield)))
            {
                args.Damage = new DamageSpecifier();
                return;
            }
        }
    }

    private void OnProjectileHit(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Handled)
            return;

        if (args.Shooter is not { } attacker)
            return;

        if (_inventory.TryGetSlotEntity(args.Target, "back", out var backItem) &&
            TryComp(backItem, out BreacherShieldComponent? equippedShield) &&
            TryBlock(args.Target, attacker, (backItem.Value, equippedShield)))
        {
            args.Handled = true;
            return;
        }

        if (TryComp(args.Target, out HandsComponent? hands))
        {
            foreach (var held in _hands.EnumerateHeld((args.Target, hands)))
            {
                if (!TryComp(held, out BreacherShieldComponent? heldShield))
                    continue;

                if (TryBlock(args.Target, attacker, (held, heldShield)))
                {
                    args.Handled = true;
                    return;
                }
            }
        }
    }

    private void OnWielded(Entity<BreacherShieldComponent> ent, ref ItemWieldedEvent args)
    {
        _appearance.SetData(ent, BreacherShieldVisuals.Raised, true);
    }

    private void OnUnwielded(Entity<BreacherShieldComponent> ent, ref ItemUnwieldedEvent args)
    {
        _appearance.SetData(ent, BreacherShieldVisuals.Raised, false);
    }

    /// <summary>
    ///     Rolls the block chance and applies feedback (sparks, flash, sound, popup) if it
    ///     succeeds. Does not touch damage itself -- callers are responsible for that, since a
    ///     projectile hit and a damage-modify hit clear it differently.
    /// </summary>
    private bool TryBlock(EntityUid wearer, EntityUid attacker, Entity<BreacherShieldComponent> shield)
    {
        if (!HasComp<BreacherWhitelistComponent>(wearer))
            return false;

        if (!_directionalBlock.IsFacingTarget(wearer, attacker))
            return false;

        // The chance roll and everything downstream of it must be server-authoritative only --
        // rolling this in predicted Shared code causes the client and server to disagree on
        // whether the block succeeded, showing the effect even when the real (server) roll failed.
        if (!_net.IsServer)
            return false;

        var raised = TryComp(shield.Owner, out WieldableComponent? wieldable) && wieldable.Wielded;
        var chance = raised ? shield.Comp.RaisedBlockChance : shield.Comp.PassiveBlockChance;
        if (!_random.Prob(chance))
            return false;

        Spawn("EffectSparks", Transform(wearer).Coordinates);
        _colorFlash.RaiseEffect(Color.White, new List<EntityUid> { wearer }, Filter.Pvs(wearer, entityManager: EntityManager));
        _audio.PlayPvs(shield.Comp.BlockSound, wearer);
        _popup.PopupEntity(
            Loc.GetString("stories-breacher-shield-block-self"),
            wearer,
            wearer);

        return true;
    }
}
