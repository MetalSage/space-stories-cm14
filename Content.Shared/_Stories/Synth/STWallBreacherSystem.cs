using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Wieldable.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth;

public sealed class STWallBreacherSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STWallBreacherComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<STWallBreacherComponent, STWallBreachDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<STWallBreacherComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_tag.HasTag(target, WallTag))
            return;

        if (!TryComp<WieldableComponent>(ent, out var wieldable) || !wieldable.Wielded)
        {
            _popup.PopupClient(Loc.GetString("st-synth-wall-breacher-not-wielded", ("tool", ent.Owner)), args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (_whitelist.IsBlacklistPass(ent.Comp.Blacklist, target))
        {
            _popup.PopupClient(Loc.GetString("st-synth-wall-breacher-immune", ("wall", target)), args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.Duration, new STWallBreachDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<STWallBreacherComponent> ent, ref STWallBreachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!_tag.HasTag(target, WallTag) || _whitelist.IsBlacklistPass(ent.Comp.Blacklist, target))
            return;

        if (_net.IsClient)
            return;

        if (!_prototype.TryIndex(ent.Comp.Girder, out _))
            return;

        var xform = Transform(target);
        var girder = Spawn(ent.Comp.Girder, xform.Coordinates);
        Transform(girder).LocalRotation = xform.LocalRotation;

        _audio.PlayPvs(ent.Comp.FinishSound, xform.Coordinates);

        QueueDel(target);
    }
}
