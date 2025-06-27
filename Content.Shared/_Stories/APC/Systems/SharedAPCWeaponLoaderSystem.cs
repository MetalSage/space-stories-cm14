using Content.Shared.Interaction;
using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.APC;
using Content.Shared._RMC14.Xenonids;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.APC.Systems;

public sealed partial class SharedAPCWeaponLoaderSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<APCWeaponLoaderComponent, InteractUsingEvent>(OnLoaderInteractUsing);
    }

    private void OnLoaderInteractUsing(Entity<APCWeaponLoaderComponent> loader, ref InteractUsingEvent args)
    {
        if (HasComp<XenoComponent>(args.User))
            return;

        if (!TryComp<TransformComponent>(loader, out var xform))
            return;

        if (!TryComp<APCEntityGridComponent>(xform.GridUid, out var apcGrid))
            return;

        if (!TryGetEntity(apcGrid.APC, out var apc))
            return;

        if (apc is null)
            return;

        _ui.OpenUi(apc.Value, APCSelectHardpointUI.Key, args.User);
        Logger.Info("Trying to open UI for loader.");
        args.Handled = true;
    }
}