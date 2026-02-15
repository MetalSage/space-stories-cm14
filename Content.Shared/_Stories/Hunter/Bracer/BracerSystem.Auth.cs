using Content.Shared._RMC14.Chat;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Speech;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Robust.Shared.Audio;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    public bool AttemptUsage(EntityUid user, Entity<HunterBracerComponent> bracer, bool guaranteedDelimbOnFail = false)
    {
        if (_net.IsClient)
            return true;

        if (IsAuthorized(user, bracer.Comp))
            return true;

        if (_random.NextFloat() < bracer.Comp.UnauthorizedSuccessChance)
            return true;

        if (_random.NextFloat() < bracer.Comp.UnauthorizedMalfunctionChance)
        {
            TriggerMalfunction(user, bracer, guaranteedDelimbOnFail);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("st-bracer-fizzle"), user, user, PopupType.SmallCaution);
        return false;
    }

    public bool IsAuthorized(EntityUid user, HunterBracerComponent bracer)
    {
        return _skills.HasSkill((user, null), bracer.RequiredSkill, bracer.RequiredSkillLevel);
    }

    private void TriggerMalfunction(EntityUid user, Entity<HunterBracerComponent> bracer, bool guaranteedDelimb)
    {
        if (!_net.IsServer)
            return;

        if (guaranteedDelimb || _random.NextFloat() < bracer.Comp.MalfunctionDelimbChance)
        {
            DelimbUser(user, bracer);
            return;
        }

        if (_random.NextFloat() < 0.40f)
        {
            ActivateRandomFunction(user, bracer);
            return;
        }

        PunishUser(user, bracer);
    }

    private void ActivateRandomFunction(EntityUid user, Entity<HunterBracerComponent> bracer)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString("st-bracer-malfunction-random"), user, user, PopupType.MediumCaution);

        var action = _random.Next(0, 4);
        switch (action)
        {
            case 0:
                bracer.Comp.TranslatorActive = !bracer.Comp.TranslatorActive;
                Dirty(bracer);
                _audio.PlayPvs(bracer.Comp.TranslatorSound, user);
                break;
            case 1:
                var isCloaked = HasComp<BracerCloakedComponent>(user);
                SetCloak(user, bracer, !isCloaked, true);
                break;
            case 2:
                var attachmentsDeployed = IsAttachmentDeployed(bracer, LeftAttachmentSlotId, user) ||
                                          IsAttachmentDeployed(bracer, RightAttachmentSlotId, user);
                if (attachmentsDeployed)
                    RetractAttachments(user, bracer);
                else
                    DeployAttachments(user, bracer);
                break;
            case 3:
                if (bracer.Comp.CasterDeployed)
                    RetractPlasmaCaster(user, bracer);
                else
                    DeployPlasmaCaster(user, bracer);
                break;
        }

        PunishUser(user, bracer);
    }

    public void PunishUser(EntityUid user, Entity<HunterBracerComponent> bracer)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString("st-bracer-punishment-shock"), user, user, PopupType.MediumCaution);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks2.ogg"), user);
        Spawn("EffectSparks", _transform.GetMapCoordinates(user));

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Shock", 10);
        _damageable.TryChangeDamage(user, damage, origin: bracer.Owner);

        _stun.TryKnockdown(user, TimeSpan.FromSeconds(4), true);
        _stun.TryStun(user, TimeSpan.FromSeconds(4), true);
    }

    private void DelimbUser(EntityUid user, Entity<HunterBracerComponent> bracer)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString("st-bracer-punishment-delimb"), user, user, PopupType.LargeCaution);
        _audio.PlayPvs(bracer.Comp.DelimbSound, user);

        RetractAttachments(user, bracer);
        RetractPlasmaCaster(user, bracer);

        var hands = _body.GetBodyChildrenOfType(user, BodyPartType.Hand);
        foreach (var part in hands)
        {
            _transform.AttachToGridOrMap(part.Id);
        }

        _inventory.TryUnequip(user, "gloves", true, true);
    }
}
