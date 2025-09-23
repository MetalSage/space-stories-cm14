using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Prying.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared._Stories.Attachables;

public sealed class VehicleAttachableHolderSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleAttachableHolderComponent, VehicleAttachableAttachDoAfterEvent>(OnAttachDoAfter);
        SubscribeLocalEvent<VehicleAttachableHolderComponent, VehicleAttachableDetachDoAfterEvent>(OnDetachDoAfter);
        SubscribeLocalEvent<VehicleAttachableHolderComponent, VehicleAttachableHolderAttachToSlotMessage>(OnAttachableHolderAttachToSlotMessage);
        SubscribeLocalEvent<VehicleAttachableHolderComponent, VehicleAttachableHolderDetachMessage>(OnAttachableHolderDetachMessage);
        SubscribeLocalEvent<VehicleAttachableHolderComponent, EntInsertedIntoContainerMessage>(OnAttached);
        SubscribeLocalEvent<VehicleAttachableHolderComponent, MapInitEvent>(OnHolderMapInit,
            after: new[] { typeof(ContainerFillSystem) });
        SubscribeLocalEvent<VehicleAttachableHolderComponent, InteractUsingEvent>(OnAttachableHolderInteractUsing);
        SubscribeLocalEvent<VehicleAttachableHolderComponent, BoundUIOpenedEvent>(OnAttachableHolderUiOpened);
    }

    private void OnHolderMapInit(Entity<VehicleAttachableHolderComponent> holder, ref MapInitEvent args)
    {
        var xform = Transform(holder.Owner);
        var coords = new EntityCoordinates(holder.Owner, Vector2.Zero);

        foreach (var slotId in holder.Comp.Slots.Keys)
        {
            var slot = holder.Comp.Slots[slotId];
            var attachment = slot.StartingAttachable;

            if (attachment == null)
                continue;

            var container = _container.EnsureContainer<ContainerSlot>(holder, slotId);
            container.OccludesLight = false;

            var attachableUid = Spawn(attachment, coords);
            if (!_container.Insert(attachableUid, container, containerXform: xform))
                continue;
        }

        Dirty(holder);
    }

    private void OnAttachableHolderInteractUsing(Entity<VehicleAttachableHolderComponent> holder, ref InteractUsingEvent args)
    {
        if (HasComp<XenoComponent>(args.User))
            return;

        if (CanAttach(holder, args.Used))
        {
            StartAttach(holder, args.Used, args.User);
            args.Handled = true;
            return;
        }

        if (HasComp<PryingComponent>(args.Used))
        {
            _ui.OpenUi(holder.Owner, VehicleAttachmentUI.StripKey, args.User);
            args.Handled = true;
            return;
        }
    }

    private void OnAttachableHolderDetachMessage(EntityUid holderUid,
        VehicleAttachableHolderComponent holderComponent,
        VehicleAttachableHolderDetachMessage args)
    {
        StartDetach((holderUid, holderComponent), args.Slot, args.Actor);
    }

    private void OnAttachableHolderAttachToSlotMessage(EntityUid holderUid,
        VehicleAttachableHolderComponent holderComponent,
        VehicleAttachableHolderAttachToSlotMessage args)
    {
        if (!TryComp<HandsComponent>(args.Actor, out var handsComponent))
            return;

        _hands.TryGetActiveItem((args.Actor, handsComponent), out var attachableUid);

        if (attachableUid == null)
            return;

        StartAttach((holderUid, holderComponent), attachableUid.Value, args.Actor, args.Slot);
    }

    public void StartAttach(Entity<VehicleAttachableHolderComponent> holder,
        EntityUid attachableUid,
        EntityUid userUid,
        string slotId = "")
    {
        if (HasComp<XenoComponent>(userUid))
            return;

        var validSlots = GetValidSlots(holder, attachableUid);

        if (validSlots.Count == 0)
            return;

        if (string.IsNullOrEmpty(slotId))
        {
            slotId = validSlots[0];
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            userUid,
            Comp<VehicleAttachableComponent>(attachableUid).AttachDoAfter,
            new VehicleAttachableAttachDoAfterEvent(slotId),
            holder,
            target: holder.Owner,
            used: attachableUid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnAttachDoAfter(EntityUid uid, VehicleAttachableHolderComponent component, VehicleAttachableAttachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } target || args.Used is not { } used)
            return;

        if (!TryComp<VehicleAttachableHolderComponent>(args.Target, out var holder) ||
            !HasComp<VehicleAttachableComponent>(args.Used))
            return;

        if (Attach((target, holder), used, args.User, args.SlotId))
            args.Handled = true;
    }

    public bool Attach(Entity<VehicleAttachableHolderComponent> holder,
        EntityUid attachableUid,
        EntityUid userUid,
        string slotId = "")
    {
        if (!CanAttach(holder, attachableUid, ref slotId))
            return false;

        var container = _container.EnsureContainer<ContainerSlot>(holder, slotId);
        container.OccludesLight = false;

        if (container.Count > 0 && !Detach(holder, container.ContainedEntities[0], userUid, slotId))
            return false;

        if (!_container.Insert(attachableUid, container))
            return false;

        Dirty(holder);

        _audio.PlayPredicted(Comp<VehicleAttachableComponent>(attachableUid).AttachSound,
            holder,
            userUid);

        return true;
    }

    private void OnAttached(Entity<VehicleAttachableHolderComponent> holder, ref EntInsertedIntoContainerMessage args)
    {
        if (!HasComp<VehicleAttachableComponent>(args.Entity) || !holder.Comp.Slots.ContainsKey(args.Container.ID))
            return;

        UpdateStripUi(holder.Owner, holder.Comp);

        var ev = new VehicleAttachableAlteredEvent(holder.Owner, VehicleAttachableAlteredType.Attached);
        RaiseLocalEvent(args.Entity, ref ev);

        var holderEv = new VehicleAttachableHolderAttachablesAlteredEvent(args.Entity, args.Container.ID,
        VehicleAttachableAlteredType.Attached);
        RaiseLocalEvent(holder, ref holderEv);
    }

    //Detaching
    public void StartDetach(Entity<VehicleAttachableHolderComponent> holder, string slotId, EntityUid userUid)
    {
        if (TryGetAttachable(holder, slotId, out var attachable) && holder.Comp.Slots.ContainsKey(slotId) &&
        !holder.Comp.Slots[slotId].Locked)
        {
            StartDetach(holder, attachable.Owner, userUid);
        }
    }

    public void StartDetach(Entity<VehicleAttachableHolderComponent> holder, EntityUid attachableUid, EntityUid userUid)
    {
        if (HasComp<XenoComponent>(userUid))
            return;

        var delay = Comp<VehicleAttachableComponent>(attachableUid).AttachDoAfter;
        var args = new DoAfterArgs(
            EntityManager,
            userUid,
            delay,
            new VehicleAttachableDetachDoAfterEvent(),
            holder,
            holder.Owner,
            attachableUid)
        {
            NeedHand = true,
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnDetachDoAfter(EntityUid uid, VehicleAttachableHolderComponent component, VehicleAttachableDetachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null || args.Used == null)
            return;

        if (!TryComp<VehicleAttachableHolderComponent>(args.Target, out var holderComponent) || !HasComp<VehicleAttachableComponent>(args.Used))
            return;

        if (!Detach((args.Target.Value, holderComponent), args.Used.Value, args.User))
            return;

        args.Handled = true;
    }

    public bool Detach(Entity<VehicleAttachableHolderComponent> holder,
        EntityUid attachableUid,
        EntityUid userUid,
        string? slotId = null)
    {
        if (TerminatingOrDeleted(holder) || !holder.Comp.Running)
            return false;

        if (string.IsNullOrEmpty(slotId) && !TryGetSlotId(holder.Owner, attachableUid, out slotId))
            return false;

        if (!_container.TryGetContainer(holder, slotId, out var container) || container.Count <= 0)
            return false;

        if (!TryGetAttachable(holder, slotId, out var attachable))
            return false;

        if (!_container.Remove(attachable.Owner, container, force: true))
            return false;

        UpdateStripUi(holder.Owner, holder.Comp);
        var ev = new VehicleAttachableAlteredEvent(holder.Owner, VehicleAttachableAlteredType.Detached, userUid);
        RaiseLocalEvent(attachableUid, ref ev);

        var holderEv = new VehicleAttachableHolderAttachablesAlteredEvent(attachableUid, slotId, VehicleAttachableAlteredType.Detached);
        RaiseLocalEvent(holder.Owner, ref holderEv);

        _audio.PlayPredicted(Comp<VehicleAttachableComponent>(attachableUid).DetachSound,
            holder,
            userUid);

        Dirty(holder);
        _hands.TryPickupAnyHand(userUid, attachable);

        return true;
    }

    private bool CanAttach(Entity<VehicleAttachableHolderComponent> holder, EntityUid attachableUid)
    {
        var slotId = "";
        return CanAttach(holder, attachableUid, ref slotId);
    }

    private bool CanAttach(Entity<VehicleAttachableHolderComponent> holder, EntityUid attachableUid, ref string slotId)
    {
        if (!HasComp<VehicleAttachableComponent>(attachableUid))
            return false;

        if (!string.IsNullOrWhiteSpace(slotId))
            return _whitelist.IsWhitelistPass(holder.Comp.Slots[slotId].Whitelist, attachableUid);

        foreach (var key in holder.Comp.Slots.Keys)
        {
            if (_whitelist.IsWhitelistPass(holder.Comp.Slots[key].Whitelist, attachableUid))
            {
                slotId = key;
                return true;
            }
        }

        return false;
    }

    public bool TryGetAttachable(Entity<VehicleAttachableHolderComponent> holder,
        string slotId,
        out Entity<VehicleAttachableComponent> attachable)
    {
        attachable = default;

        if (!_container.TryGetContainer(holder, slotId, out var container) || container.Count <= 0)
            return false;

        var ent = container.ContainedEntities[0];
        if (!TryComp<VehicleAttachableComponent>(ent, out var attachableComp))
            return false;

        attachable = (ent, attachableComp);
        return true;
    }

    private void EnsureSlots(Entity<VehicleAttachableHolderComponent> holder)
    {
        foreach (var slotId in holder.Comp.Slots.Keys)
        {
            var container = _container.EnsureContainer<ContainerSlot>(holder, slotId);
            container.OccludesLight = false;
        }
    }

    private List<string> GetValidSlots(Entity<VehicleAttachableHolderComponent> holder, EntityUid attachableUid, bool ignoreLock = false)
    {
        var list = new List<string>();

        if (!HasComp<VehicleAttachableComponent>(attachableUid))
            return list;

        foreach (var slotId in holder.Comp.Slots.Keys)
        {
            if (_whitelist.IsWhitelistPass(holder.Comp.Slots[slotId].Whitelist, attachableUid) && (!ignoreLock || !holder.Comp.Slots[slotId].Locked))
                list.Add(slotId);
        }

        return list;
    }

    public bool TryGetSlotId(EntityUid holderUid, EntityUid attachableUid, [NotNullWhen(true)] out string? slotId)
    {
        slotId = null;

        if (!TryComp<VehicleAttachableHolderComponent>(holderUid, out var holderComponent) ||
            !TryComp<VehicleAttachableComponent>(attachableUid, out _))
        {
            return false;
        }

        foreach (var id in holderComponent.Slots.Keys)
        {
            if (!_container.TryGetContainer(holderUid, id, out var container) || container.Count <= 0)
                continue;

            if (container.ContainedEntities[0] != attachableUid)
                continue;

            slotId = id;
            return true;
        }

        return false;
    }

    public bool HasSlot(Entity<VehicleAttachableHolderComponent?> holder, string slotId)
    {
        if (holder.Comp == null)
        {
            if (!TryComp<VehicleAttachableHolderComponent>(holder.Owner, out var holderComponent))
                return false;

            holder.Comp = holderComponent;
        }

        return holder.Comp.Slots.ContainsKey(slotId);
    }

    public bool TryGetHolder(EntityUid attachable, [NotNullWhen(true)] out EntityUid? holderUid)
    {
        if (!TryComp<TransformComponent>(attachable, out var transformComponent) ||
            !transformComponent.ParentUid.Valid ||
            !HasComp<VehicleAttachableHolderComponent>(transformComponent.ParentUid))
        {
            holderUid = null;
            return false;
        }

        holderUid = transformComponent.ParentUid;
        return true;
    }

    public bool TryGetUser(EntityUid attachable, [NotNullWhen(true)] out EntityUid? userUid)
    {
        userUid = null;

        if (!TryGetHolder(attachable, out var holderUid))
            return false;

        if (!TryComp<TransformComponent>(holderUid, out var transformComponent) || !transformComponent.ParentUid.Valid)
            return false;

        userUid = transformComponent.ParentUid;
        return true;
    }

    private void AlterAllAttachables(Entity<VehicleAttachableHolderComponent> holder, VehicleAttachableAlteredType alteration)
    {
        foreach (var slotId in holder.Comp.Slots.Keys)
        {
            if (!_container.TryGetContainer(holder, slotId, out var container) || container.Count <= 0)
                continue;

            var ev = new VehicleAttachableAlteredEvent(holder.Owner, alteration);
            RaiseLocalEvent(container.ContainedEntities[0], ref ev);
        }
    }

    private void OnAttachableHolderUiOpened(EntityUid holderUid,
        VehicleAttachableHolderComponent holderComponent,
        BoundUIOpenedEvent args)
    {
        UpdateStripUi(holderUid);
    }

    private Dictionary<string, (string?, bool, string?, string?)> GetSlotsForStripUi(Entity<VehicleAttachableHolderComponent> holder)
    {
        var result = new Dictionary<string, (string?, bool, string?, string?)>();
        var metaQuery = GetEntityQuery<MetaDataComponent>();

        foreach (var slotId in holder.Comp.Slots.Keys)
        {
            if (TryGetAttachable(holder, slotId, out var attachable) &&
                metaQuery.TryGetComponent(attachable.Owner, out var metadata))
            {
                result.Add(slotId, (metadata.EntityName, holder.Comp.Slots[slotId].Locked, attachable.Comp.Description, attachable.Comp.Stats));
            }
            else
            {
                result.Add(slotId, (null, holder.Comp.Slots[slotId].Locked, null, null));
            }
        }

        return result;
    }

    private void UpdateStripUi(EntityUid holderUid, VehicleAttachableHolderComponent? holderComponent = null)
    {
        if (!Resolve(holderUid, ref holderComponent))
            return;

        var state =
            new VehicleAttachableHolderStripUserInterfaceState(GetSlotsForStripUi((holderUid, holderComponent)));
        _ui.SetUiState(holderUid, VehicleAttachmentUI.StripKey, state);
    }

}
