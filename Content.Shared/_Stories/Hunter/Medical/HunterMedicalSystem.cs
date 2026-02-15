using Content.Shared._Stories.Hunter.Medical.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Medical;

public sealed class HunterMedicalSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterHealingGunComponent, InteractUsingEvent>(OnGunInteractUsing);
    }

    private void OnGunInteractUsing(Entity<HunterHealingGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<HealingGelComponent>(args.Used, out _))
            return;

        if (ent.Comp.Loaded)
        {
            _popup.PopupClient(Loc.GetString("st-healing-gun-already-loaded"), args.User, args.User);
            return;
        }

        args.Handled = true;
        ent.Comp.Loaded = true;
        Dirty(ent);
        UpdateGunAppearance(ent);

        _audio.PlayPredicted(ent.Comp.ReloadSound, ent, args.User);
        _popup.PopupClient(
            Loc.GetString("st-healing-gun-reloaded", ("gun", ent.Owner), ("ammo", args.Used)),
            args.User,
            args.User
        );

        if (_net.IsServer)
            QueueDel(args.Used);
    }

    public void SetGunLoaded(Entity<HunterHealingGunComponent> ent, bool loaded)
    {
        ent.Comp.Loaded = loaded;
        Dirty(ent);
        UpdateGunAppearance(ent);
    }

    private void UpdateGunAppearance(Entity<HunterHealingGunComponent> ent)
    {
        _appearance.SetData(ent, STHealingGunVisuals.Loaded, ent.Comp.Loaded);
    }
}

[Serializable] [NetSerializable]
public enum STHealingGunVisuals : byte
{
    Loaded,
}
