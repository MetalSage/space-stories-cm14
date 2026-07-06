using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared._Stories.Breacher.Components;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Effects;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Breacher;

public sealed class BreacherArmorSystem : EntitySystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCStatusEffectSystem _rmcStatusEffect = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreacherArmorComponent, BreacherEnrageActionEvent>(OnEnrageAction);
        SubscribeLocalEvent<GunComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BreacherEnrageActiveComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            if (_timing.CurTime >= active.EndTime)
            {
                EndEnrage(uid, active);
                continue;
            }

            if (_timing.CurTime < active.NextPulse)
                continue;

            if (!TryComp(active.Armor, out BreacherArmorComponent? armor))
                continue;

            var remaining = active.EndTime - _timing.CurTime;
            var interval = remaining <= armor.BlinkThreshold ? armor.BlinkInterval : armor.PulseInterval;
            active.NextPulse = _timing.CurTime + interval;
            Dirty(uid, active);

            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { uid }, Filter.Pvs(uid, entityManager: EntityManager));
        }
    }

    private void OnEnrageAction(Entity<BreacherArmorComponent> ent, ref BreacherEnrageActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!_inventory.InSlotWithFlags((ent.Owner, null, null), SlotFlags.OUTERCLOTHING))
            return;

        if (!HasComp<BreacherWhitelistComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("stories-breacher-enrage-untrained"), user, user);
            return;
        }

        if (HasComp<BreacherEnrageActiveComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("stories-breacher-enrage-already-active"), user, user);
            return;
        }

        if (!_skills.HasSkill(user, ent.Comp.RequiredSkill, 1))
        {
            _popup.PopupClient(Loc.GetString("stories-breacher-enrage-untrained"), user, user);
            return;
        }

        args.Handled = true;

        var active = EnsureComp<BreacherEnrageActiveComponent>(user);
        active.EndTime = _timing.CurTime + ent.Comp.EnrageDuration;
        active.Armor = ent.Owner;
        active.NextPulse = _timing.CurTime;
        Dirty(user, active);

        var resist = EnsureComp<DamageProtectionBuffComponent>(user);
        resist.Modifiers[BuffKey(ent.Owner)] = _proto.Index(ent.Comp.EnrageDamageResist);
        Dirty(user, resist);

        _rmcStatusEffect.GiveStunResistance(user, ent.Comp.EnrageStunResistance);

        _popup.PopupPredicted(
            Loc.GetString("stories-breacher-enrage-start"),
            Loc.GetString("stories-breacher-enrage-start-others", ("user", user)),
            user,
            user);
    }

    private void OnShotAttempted(Entity<GunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (HasComp<BreacherEnrageActiveComponent>(args.User))
            args.Cancel();
    }

    private void EndEnrage(EntityUid uid, BreacherEnrageActiveComponent active)
    {
        RemCompDeferred<BreacherEnrageActiveComponent>(uid);

        if (TryComp(uid, out DamageProtectionBuffComponent? resist))
        {
            resist.Modifiers.Remove(BuffKey(active.Armor));
            Dirty(uid, resist);
        }

        // Resistance 1 is neutral (duration divided by 1 = unchanged), avoids touching
        // RMCStunResistanceComponent directly since it's Access-locked to RMCStatusEffectSystem.
        _rmcStatusEffect.GiveStunResistance(uid, 1f);

        _popup.PopupPredicted(
            Loc.GetString("stories-breacher-enrage-end"),
            Loc.GetString("stories-breacher-enrage-end-others", ("user", uid)),
            uid,
            uid);
    }

    private static string BuffKey(EntityUid armor)
    {
        return $"BreacherEnrage-{armor}";
    }
}
