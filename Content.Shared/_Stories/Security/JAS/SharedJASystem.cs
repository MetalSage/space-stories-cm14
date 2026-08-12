using Content.Shared.Access.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Security.JAS;

public sealed partial class SharedJASystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<JASComponent>(JASBUiKey.Key,
            subs =>
            {
                subs.Event<JASPrivilegedIdInsertedMsg>(OnPrivilegedIdInserted);
                subs.Event<JASPrivilegedIdEjectedMsg>(OnPrivilegedIdEjected);

                subs.Event<JASTabSelectedMsg>(OnTabSelected);

                subs.Event<JASDetailsSaveMsg>(OnDetailsSave);

                subs.Event<JASChargeAddMsg>(OnChargeAdd);
                // subs.Event<JASChargeRemoveMsg>(OnChargeRemove);
            });
    }

    private void OnPrivilegedIdInserted(Entity<JASComponent> ent, ref JASPrivilegedIdInsertedMsg args)
    {
        if (_net.IsClient)
            return;

        if (!_hands.TryGetActiveItem(args.Actor, out var handEntity) || !TryComp<IdCardComponent>(handEntity, out var cardComponent))
            return;
        var container = _container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.PrivilegedIdSlot);
        _container.Insert(handEntity.Value, container);

        ent.Comp.Authorized = true;
        ent.Comp.User = !string.IsNullOrEmpty(cardComponent.FullName) ? cardComponent.FullName : string.Empty;

        UpdateStates(ent);
    }

    private void OnPrivilegedIdEjected(Entity<JASComponent> ent, ref JASPrivilegedIdEjectedMsg args)
    {
        if (_net.IsClient)
            return;

        var container = _container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.PrivilegedIdSlot);
        var contained = container.ContainedEntity;
        if (contained == null)
            return;

        _container.Remove(contained.Value, container);
        _hands.PickupOrDrop(args.Actor, contained.Value);

        ent.Comp.Authorized = false;
        ent.Comp.Details = string.Empty;
        ent.Comp.CurrectTab = JASTabs.None;
        ent.Comp.User = "";

        UpdateStates(ent);
    }

    private void OnChargeAdd(Entity<JASComponent> ent, ref JASChargeAddMsg args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.ChoosedLaws.Add(args.Proto);
    }

    private void OnDetailsSave(Entity<JASComponent> ent, ref JASDetailsSaveMsg args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.Details = args.Msg;

        UpdateStates(ent);
    }

    private void OnTabSelected(Entity<JASComponent> ent, ref JASTabSelectedMsg args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.CurrectTab = args.Tab;

        UpdateStates(ent);
    }

    private void UpdateStates(Entity<JASComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, JASBUiKey.Key))
            return;

        var state = new JASBuiState(ent.Comp.Authorized, ent.Comp.User, ent.Comp.Details, ent.Comp.CurrectTab);
        _ui.SetUiState(ent.Owner, JASBUiKey.Key, state);
    }
}
