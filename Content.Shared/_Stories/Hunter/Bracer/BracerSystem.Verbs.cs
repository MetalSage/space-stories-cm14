using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Equipment;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializeVerbs()
    {
        SubscribeLocalEvent<HunterBracerComponent, GetVerbsEvent<AlternativeVerb>>(
            OnGetBracerVerbs,
            after: new[] { typeof(ItemSlotsSystem) }
        );
        SubscribeLocalEvent<HunterComponent, GetVerbsEvent<InteractionVerb>>(OnHunterBodyVerbs);
    }

    private void OnHunterBodyVerbs(Entity<HunterComponent> victim, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var victimUid = victim.Owner;

        if (
            !TryComp<MobStateComponent>(victimUid, out var mobState)
            || mobState.CurrentState != MobState.Dead
        )
            return;

        if (!HasComp<HunterComponent>(user))
            return;

        if (!_inventory.TryGetSlotEntity(victimUid, "gloves", out var glovesUid) || !glovesUid.HasValue)
            return;

        if (!TryComp<HunterBracerComponent>(glovesUid, out var bracerComp))
            return;

        if (HasComp<BracerSelfDestructingComponent>(glovesUid))
            return;

        args.Verbs.Add(
            new InteractionVerb
            {
                Text = Loc.GetString("st-hunter-verb-remote-sd"),
                Act = () =>
                {
                    if (_net.IsClient)
                        return;

                    bracerComp.ExplosionType = SelfDestructType.Small;
                    Dirty(glovesUid.Value, bracerComp);

                    var ev = new RequestSelfDestructEvent { Performer = user };
                    OnRequestSelfDestruct((glovesUid.Value, bracerComp), ref ev);
                },
                Priority = 100,
            }
        );
    }

    private void OnGetBracerVerbs(Entity<HunterBracerComponent> bracer, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (
            IsAttachmentDeployed(bracer, LeftAttachmentSlotId, args.User)
            || IsAttachmentDeployed(bracer, RightAttachmentSlotId, args.User)
        )
        {
            var leftItemNet =
                _itemSlots.TryGetSlot(bracer, LeftAttachmentSlotId, out var left) && left.HasItem
                    ? GetNetEntity(left.Item!.Value)
                    : NetEntity.Invalid;
            var rightItemNet =
                _itemSlots.TryGetSlot(bracer, RightAttachmentSlotId, out var right) && right.HasItem
                    ? GetNetEntity(right.Item!.Value)
                    : NetEntity.Invalid;

            args.Verbs.RemoveWhere(verb =>
                verb.Category == VerbCategory.Eject
                && (
                    leftItemNet != NetEntity.Invalid && verb.IconEntity == leftItemNet
                    || rightItemNet != NetEntity.Invalid && verb.IconEntity == rightItemNet
                )
            );
        }

        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var target = args.Target;

        if (!_inventory.TryGetSlotEntity(user, "gloves", out var equippedGloves) || equippedGloves != target)
            return;

        if (IsAuthorized(user, bracer.Comp))
        {
            args.Verbs.Add(
                new AlternativeVerb
                {
                    Text = Loc.GetString("st-bracer-verb-track-gear"),
                    Priority = 80,
                    Act = () =>
                    {
                        if (!TryDrainPower(user, bracer, 20f))
                            return;

                        var ev = new HunterTrackGearEvent(user, bracer.Owner);
                        RaiseLocalEvent(bracer.Owner, ref ev);
                    },
                }
            );

            args.Verbs.Add(
                new AlternativeVerb
                {
                    Text = bracer.Comp.ShowClanName
                        ? Loc.GetString("st-bracer-verb-hide-clan-name")
                        : Loc.GetString("st-bracer-verb-show-clan-name"),
                    Priority = 90,
                    Act = () =>
                    {
                        if (_net.IsClient)
                            return;

                        bracer.Comp.ShowClanName = !bracer.Comp.ShowClanName;
                        Dirty(bracer.Owner, bracer.Comp);

                        UpdateIdentity(bracer.Owner, bracer.Comp, user);

                        var msg = bracer.Comp.ShowClanName
                            ? Loc.GetString("st-bracer-clan-name-on")
                            : Loc.GetString("st-bracer-clan-name-off");
                        _popup.PopupEntity(msg, user, user);
                    },
                }
            );

            if (!HasComp<BracerSelfDestructingComponent>(bracer))
            {
                args.Verbs.Add(
                    new AlternativeVerb
                    {
                        Text = Loc.GetString("st-bracer-change-explosion-type"),
                        Act = () =>
                        {
                            if (_net.IsClient)
                                return;

                            var newType = bracer.Comp.ExplosionType == SelfDestructType.Small
                                ? SelfDestructType.Big
                                : SelfDestructType.Small;

                            if (newType == SelfDestructType.Big)
                            {
                                var mapUid = Transform(user).MapUid;
                                if (!mapUid.HasValue || !HasComp<RMCPlanetComponent>(mapUid.Value))
                                {
                                    _popup.PopupEntity(Loc.GetString("st-bracer-sd-big-explosion-denied"), user, user);
                                    return;
                                }
                            }

                            bracer.Comp.ExplosionType = newType;
                            Dirty(bracer);

                            var typeKey = bracer.Comp.ExplosionType == SelfDestructType.Small
                                ? "st-bracer-sd-type-small"
                                : "st-bracer-sd-type-big";
                            var localizedTypeName = Loc.GetString(typeKey);

                            _popup.PopupEntity(
                                Loc.GetString("st-bracer-sd-type-changed", ("type", localizedTypeName)),
                                user,
                                user
                            );
                        },
                    }
                );
            }
        }

        args.Verbs.Add(
            new AlternativeVerb
            {
                Text = Loc.GetString(bracer.Comp.Locked ? "st-bracer-unlock" : "st-bracer-lock"),
                Priority = 100,
                Act = () =>
                {
                    if (!AttemptUsage(user, bracer))
                        return;

                    if (_net.IsClient)
                        return;

                    bracer.Comp.Locked = !bracer.Comp.Locked;
                    Dirty(bracer.Owner, bracer.Comp);

                    _audio.PlayPvs(
                        bracer.Comp.Locked ? bracer.Comp.LockSound : bracer.Comp.UnlockSound,
                        user
                    );

                    _popup.PopupEntity(
                        Loc.GetString(bracer.Comp.Locked ? "st-bracer-locked" : "st-bracer-unlocked"),
                        user,
                        user
                    );
                },
            }
        );
    }
}
