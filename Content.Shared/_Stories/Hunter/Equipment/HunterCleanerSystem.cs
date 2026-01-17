using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Equipment;

public sealed class HunterCleanerSystem : EntitySystem
{
    [Dependency]
    private readonly SharedXenoAcidSystem _acid = default!;

    [Dependency]
    private readonly SharedContainerSystem _container = default!;

    [Dependency]
    private readonly SharedDoAfterSystem _doAfter = default!;

    [Dependency]
    private readonly INetManager _net = default!;

    [Dependency]
    private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterCleanerVialComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HunterCleanerVialComponent, HunterCleanerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HunterCleanerVialComponent, GetVerbsEvent<UtilityVerb>>(OnGetVerbs);
    }

    private void OnAfterInteract(Entity<HunterCleanerVialComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryStartMelt(args.User, args.Target.Value, ent))
            args.Handled = true;
    }

    private void OnGetVerbs(Entity<HunterCleanerVialComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        var user = args.User;
        var target = args.Target;

        if (!CanMelt(user, target, ent, true))
            return;

        var verb = new UtilityVerb
        {
            Act = () => TryStartMelt(user, target, ent),
            Text = Loc.GetString("st-hunter-cleaner-verb"),
        };

        args.Verbs.Add(verb);
    }

    private bool CanMelt(EntityUid user, EntityUid target, Entity<HunterCleanerVialComponent> vial, bool quiet = false)
    {
        if (!HasComp<HunterComponent>(user))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("st-hunter-cleaner-no-skill"), user, user);
            return false;
        }

        if (!HasComp<ItemComponent>(target))
            return false;

        if (_container.IsEntityInContainer(target))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("st-hunter-cleaner-cannot-held"), user, user);
            return false;
        }

        if (HasComp<HunterCleanerVialComponent>(target))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("st-hunter-cleaner-cannot-self"), user, user);
            return false;
        }

        if (_acid.IsMelted(target))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("st-hunter-cleaner-already-melting"), user, user);
            return false;
        }

        return true;
    }

    private bool TryStartMelt(EntityUid user, EntityUid target, Entity<HunterCleanerVialComponent> vial)
    {
        if (!CanMelt(user, target, vial))
            return false;

        _popup.PopupClient(Loc.GetString("st-hunter-cleaner-start", ("target", target)), user, user);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            vial.Comp.DoAfterDuration,
            new HunterCleanerDoAfterEvent(),
            vial,
            target,
            vial
        )
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        return true;
    }

    private void OnDoAfter(Entity<HunterCleanerVialComponent> ent, ref HunterCleanerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            var target = args.Args.Target.Value;
            var user = args.User;

            if (_container.IsEntityInContainer(target))
            {
                _popup.PopupEntity(Loc.GetString("st-hunter-cleaner-cannot-held"), user, user);
                return;
            }

            _popup.PopupEntity(Loc.GetString("st-hunter-cleaner-success", ("target", target)), target);

            _acid.ApplyAcid(ent.Comp.AcidPrototype, ent.Comp.Strength, target, 0, 0, ent.Comp.MeltTime);
        }
    }
}

[Serializable] [NetSerializable]
public sealed partial class HunterCleanerDoAfterEvent : SimpleDoAfterEvent
{
}
