using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed class SharedStabilizingCrystalSystem : EntitySystem
{
    [Dependency]
    private readonly SharedAppearanceSystem _appearance = default!;

    [Dependency]
    private readonly SharedAudioSystem _audio = default!;

    [Dependency]
    private readonly BracerSystem _bracer = default!;

    [Dependency]
    private readonly InventorySystem _inventory = default!;

    [Dependency]
    private readonly MetaDataSystem _metaData = default!;

    [Dependency]
    private readonly INetManager _net = default!;

    [Dependency]
    private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StabilizingCrystalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StabilizingCrystalComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnStartup(Entity<StabilizingCrystalComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnUseInHand(Entity<StabilizingCrystalComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Used)
            return;

        var user = args.User;

        if (
            !_inventory.TryGetSlotEntity(user, "gloves", out var bracerUid)
            || !TryComp<HunterBracerComponent>(bracerUid, out var bracerComp)
        )
        {
            _popup.PopupClient(Loc.GetString("st-stabilizing-crystal-no-bracer"), user, user);
            return;
        }

        args.Handled = true;

        _audio.PlayPredicted(ent.Comp.UseSound, ent, user);
        _popup.PopupClient(Loc.GetString("st-stabilizing-crystal-used"), user, user);

        ent.Comp.Used = true;
        Dirty(ent);
        UpdateAppearance(ent);

        if (_net.IsServer)
        {
            _bracer.TryAddBracerCharge(user, bracerUid.Value, ent.Comp.EnergyToRestore);
            _metaData.SetEntityName(ent, Loc.GetString("st-stabilizing-crystal-name-used"));
            _metaData.SetEntityDescription(ent, Loc.GetString("st-stabilizing-crystal-desc-used"));
        }
    }

    private void UpdateAppearance(Entity<StabilizingCrystalComponent> ent)
    {
        var state = ent.Comp.Used ? StabilizingCrystalVisualState.Used : StabilizingCrystalVisualState.Normal;
        _appearance.SetData(ent, StabilizingCrystalVisuals.State, state);
    }
}
