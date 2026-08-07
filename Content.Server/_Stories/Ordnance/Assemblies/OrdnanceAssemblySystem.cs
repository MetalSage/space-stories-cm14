using Content.Shared._Stories.Ordnance;
using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Triggers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Ordnance.Assemblies;

public sealed class OrdnanceAssemblySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly string AssemblyContainerId = "assembly_container";
    private static readonly EntProtoId HolderProto = "STAssemblyHolder";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrdnanceAssemblyComponent, InteractUsingEvent>(OnAssemblyInteractUsing);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, InteractUsingEvent>(OnHolderInteractUsing);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, UseInHandEvent>(OnHolderUseInHand);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, OrdnancePulseEvent>(OnHolderPulse);
    }

    private void OnAssemblyInteractUsing(Entity<OrdnanceAssemblyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<OrdnanceAssemblyComponent>(args.Used, out var otherAssembly))
        {
            if (ent.Comp.Holder != null || otherAssembly.Holder != null)
                return;

            args.Handled = true;
            CreateHolder(ent.Owner, args.Used, args.User);
        }
    }

    private void OnHolderUseInHand(Entity<OrdnanceAssemblyHolderComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.IsLocked)
        {
            Disassemble(ent, args.User);
        }
        else
        {
            if (ent.Comp.Part1 != null) OpenPartUI(ent.Owner, ent.Comp.Part1.Value, args.User);
            if (ent.Comp.Part2 != null) OpenPartUI(ent.Owner, ent.Comp.Part2.Value, args.User);
        }

        args.Handled = true;
    }

    private void OpenPartUI(EntityUid holder, EntityUid part, EntityUid user)
    {
        if (HasComp<OrdnanceTimerComponent>(part))
            _ui.TryOpenUi(holder, OrdnanceTimerUiKey.Key, user);

        if (HasComp<OrdnanceProxSensorComponent>(part))
            _ui.TryOpenUi(holder, OrdnanceProxSensorUiKey.Key, user);

        if (HasComp<OrdnanceSignallerComponent>(part))
            _ui.TryOpenUi(holder, OrdnanceSignallerUiKey.Key, user);
    }

    private void OnHolderInteractUsing(Entity<OrdnanceAssemblyHolderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tag.HasTag(args.Used, "Screwdriver"))
        {
            args.Handled = true;
            ent.Comp.IsLocked = !ent.Comp.IsLocked;
            Dirty(ent);

            var msg = ent.Comp.IsLocked ? "stories-assembly-lock" : "stories-assembly-unlock";
            _popup.PopupEntity(Loc.GetString(msg), ent, args.User);
        }
    }

    private void Disassemble(Entity<OrdnanceAssemblyHolderComponent> ent, EntityUid user)
    {
        if (ent.Comp.Container is { } container && ent.Comp.Part1 is { } part1 && ent.Comp.Part2 is { } part2)
        {
            _container.Remove(part1, container);
            _container.Remove(part2, container);

            if (TryComp<OrdnanceAssemblyComponent>(part1, out var p1Comp))
            {
                p1Comp.Holder = null;
                Dirty(part1, p1Comp);
            }

            if (TryComp<OrdnanceAssemblyComponent>(part2, out var p2Comp))
            {
                p2Comp.Holder = null;
                Dirty(part2, p2Comp);
            }

            var coordinates = _transform.GetMoverCoordinates(ent);
            _transform.SetCoordinates(part1, coordinates);
            _transform.SetCoordinates(part2, coordinates);
        }

        _popup.PopupEntity(Loc.GetString("stories-assembly-disassemble"), ent, user);
        QueueDel(ent);
    }

    private void CreateHolder(EntityUid part1, EntityUid part2, EntityUid user)
    {
        var mapCoords = _transform.GetMapCoordinates(part1);
        var holderUid = Spawn(HolderProto, mapCoords);
        var holderComp = EnsureComp<OrdnanceAssemblyHolderComponent>(holderUid);

        var container = _container.EnsureContainer<Container>(holderUid, AssemblyContainerId);
        holderComp.Container = container;

        _container.Insert(part1, container);
        _container.Insert(part2, container);

        holderComp.Part1 = part1;
        holderComp.Part2 = part2;
        holderComp.IsLocked = false;
        Dirty(holderUid, holderComp);

        var p1Comp = EnsureComp<OrdnanceAssemblyComponent>(part1);
        p1Comp.Holder = holderUid;
        p1Comp.IsSecured = true;
        Dirty(part1, p1Comp);

        var p2Comp = EnsureComp<OrdnanceAssemblyComponent>(part2);
        p2Comp.Holder = holderUid;
        p2Comp.IsSecured = true;
        Dirty(part2, p2Comp);

        UpdateAppearanceAndDescription(holderUid, part1, part2);
        _hands.TryPickupAnyHand(user, holderUid);
        _popup.PopupEntity(Loc.GetString("stories-assembly-assembled"), holderUid, user);
    }

    private void UpdateAppearanceAndDescription(EntityUid holder, EntityUid part1, EntityUid part2)
    {
        var name1 = MetaData(part1).EntityName;
        var name2 = MetaData(part2).EntityName;

        _metaData.SetEntityName(holder, Loc.GetString("stories-assembly-name", ("part1", name1), ("part2", name2)));
        _metaData.SetEntityDescription(holder, Loc.GetString("stories-assembly-description", ("part1", name1), ("part2", name2)));

        var sprite1 = GetSpriteId(part1);
        var sprite2 = GetSpriteId(part2);

        _appearance.SetData(holder, OrdnanceAssemblyVisuals.LeftId, sprite1);
        _appearance.SetData(holder, OrdnanceAssemblyVisuals.RightId, sprite2);
    }

    private string GetSpriteId(EntityUid uid)
    {
        return TryComp<OrdnanceAssemblyComponent>(uid, out var comp) ? comp.SpriteId : "blank";
    }

    private void OnHolderPulse(Entity<OrdnanceAssemblyHolderComponent> ent, ref OrdnancePulseEvent args)
    {
        var source = args.Source;

        if (ent.Comp.Part1 != null && ent.Comp.Part1 != source)
        {
            var part1Args = args;
            RaiseLocalEvent(ent.Comp.Part1.Value, ref part1Args);
        }

        if (ent.Comp.Part2 != null && ent.Comp.Part2 != source)
        {
            var part2Args = args;
            RaiseLocalEvent(ent.Comp.Part2.Value, ref part2Args);
        }

        if (Transform(ent).ParentUid is { Valid: true } parent)
        {
            var parentArgs = args;
            RaiseLocalEvent(parent, ref parentArgs);
        }
    }
}
