using System.Linq;
using Content.Shared._Stories.Sponsors.XenoSkins;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Server._Stories.Sponsors.XenoSkins;

public sealed class XenoSkinsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SponsorsManager _partners = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoSkinsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<XenoSkinsComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<XenoSkinsComponent, XenoOpenSkinsMenuActionEvent>(OnXenoSkinsMenuAction);
        SubscribeLocalEvent<XenoSkinsComponent, XenoSkinsDoAfterEvent>(OnXenoSkinsDoAfter);

        Subs.BuiEvents<XenoSkinsComponent>(XenoSkinsUIKey.Key, subs =>
        {
            subs.Event<XenoSkinsBuiMsg>(OnXenoSkinsBui);
        });
    }

    private void OnMapInit(Entity<XenoSkinsComponent> xeno, ref MapInitEvent args)
    {
        if (!TryComp<MindComponent>(xeno, out var mind) || mind.UserId is not { } userId)
            return;

        if (_partners.TryGetInfo(userId, out var sponsorData))
            xeno.Comp.Skins = sponsorData.XenoSkins.ToList();

        if (xeno.Comp.Skins.Count > 0)
            xeno.Comp.ActionEntity = _actions.AddAction(xeno, xeno.Comp.Action);
    }

    private void OnComponentShutdown(Entity<XenoSkinsComponent> xeno, ref ComponentShutdown args)
    {
        _actions.RemoveAction(xeno, xeno.Comp.ActionEntity);
    }

    private void OnXenoSkinsMenuAction(Entity<XenoSkinsComponent> xeno, ref XenoOpenSkinsMenuActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (xeno.Comp.ActiveDoAfter != null)
        {
            _doAfter.Cancel(xeno.Comp.ActiveDoAfter.Value);
            xeno.Comp.ActiveDoAfter = null;
            _popup.PopupClient(Loc.GetString("st-xeno-skin-apply-cancel"), xeno, xeno);
            return;
        }

        _ui.OpenUi(xeno.Owner, XenoSkinsUIKey.Key, xeno);
    }

    private void OnXenoSkinsBui(Entity<XenoSkinsComponent> xeno, ref XenoSkinsBuiMsg args)
    {
        var actor = args.Actor;
        var skin = args.Choice;

        _ui.CloseUi(xeno.Owner, XenoSkinsUIKey.Key, actor);

        if (!_prototype.TryIndex(skin, out XenoSkinsPrototype? skinIndex) || !xeno.Comp.Skins.Contains(skin))
            return;

        var path = SpriteSpecifierSerializer.TextureRoot / skinIndex.SpriteRsi;
        var ev = new XenoSkinsDoAfterEvent(path, skin);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.DoAfterDelay, ev, xeno);

        if (xeno.Comp.DoAfterDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("st-xeno-skin-apply-start"), xeno, xeno);

        if (_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            xeno.Comp.ActiveDoAfter = id;

            _jitter.DoJitter(xeno, xeno.Comp.DoAfterDelay, true, 80, 8, true);

            var popupOthers = Loc.GetString("st-xeno-skins-apply-start-others", ("xeno", xeno));
            _popup.PopupEntity(popupOthers, xeno, Filter.PvsExcept(xeno), true, PopupType.Medium);

            var popupSelf = Loc.GetString("st-xeno-skins-apply-start-self");
            _popup.PopupEntity(popupSelf, xeno, xeno, PopupType.Medium);
        }
    }

    private void OnXenoSkinsDoAfter(Entity<XenoSkinsComponent> xeno, ref XenoSkinsDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            xeno.Comp.ActiveDoAfter = null;
            return;
        }

        RaiseNetworkEvent(new XenoSkinChangeRSIEvent(GetNetEntity(xeno), args.Path), xeno);
        xeno.Comp.CurrentSkin = args.Proto;
        xeno.Comp.ActiveDoAfter = null;

        _actions.RemoveAction(xeno, xeno.Comp.ActionEntity);
    }
}
