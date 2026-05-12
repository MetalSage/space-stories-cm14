using Content.Shared._RMC14.Emote;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Clothing;

public sealed class HelmetTapSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HelmetTapComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<HelmetTapComponent> ent, ref InteractUsingEvent args)
    {
        //проверка на то, что шлем находится на голове (чтобы нельзя было стучать по шлемам на земле)
        if (!_inventory.TryGetContainingSlot(ent.Owner, out var slot) || slot.Name != "head")
            return;

        var curTime = _timing.CurTime;

        if (curTime < ent.Comp.LastTapTime + ent.Comp.Cooldown)
            return;

        ent.Comp.LastTapTime = curTime;
        Dirty(ent);

        //проигрывание аудио и эмоута в чат
        _audio.PlayPredicted(ent.Comp.TapSound, ent, args.User);
        _emote.TryEmoteWithChat(args.User, "HelmetTap");

        // Локальное сообщение о патронах в магазине
        if (TryComp<BallisticAmmoProviderComponent>(args.Used, out var ammo))
        {
            var percent = (float)ammo.Count / ammo.Capacity * 100f;
            string msg;

            if (percent >= 100f)
                msg = "Магазин полон";
            else if (percent > 50f)
                msg = "Магазин наполовину полон";
            else if (percent > 0f)
                msg = "Магазин наполовину пуст";
            else
                msg = "Магазин пуст";

            _popup.PopupClient(msg, args.User, args.User, PopupType.Medium);
        }

        args.Handled = true;
    }
}