using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Stories;

public sealed class XenoSkinsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<XenoSkinsComponent, XenoOpenSkinsMenuActionEvent>(OnXenoSkinsMenuAction);
        Subs.BuiEvents<XenoSkinsComponent>(XenoSkinsUIKey.Key,
            subs =>
            {
                subs.Event<XenoSkinsBuiMsg>(OnXenoSkinsBui);

            });
    }
    private void OnXenoSkinsMenuAction(Entity<XenoSkinsComponent> xeno, ref XenoOpenSkinsMenuActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.OpenUi(xeno.Owner, XenoSkinsUIKey.Key, xeno);
    }

    private void OnXenoSkinsBui(Entity<XenoSkinsComponent> xeno, ref XenoSkinsBuiMsg args)
    {
        var actor = args.Actor;
        var skin = args.Choice;
        _ui.CloseUi(xeno.Owner, XenoSkinsUIKey.Key, actor);

        if (_net.IsClient)
            return;

        if (!_prototype.TryIndex(skin, out XenoSkinsPrototype? skinIndex))
            return;

        if (!xeno.Comp.Skins.Contains(skin))
            return;

        xeno.Comp.CurrentSkin = skin;
        var path = SpriteSpecifierSerializer.TextureRoot / skinIndex.Rsi;
        RaiseNetworkEvent(new XenoSkinChangeRSIEvent(GetNetEntity(xeno), path), xeno);
    }
}
