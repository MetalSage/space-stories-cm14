using Robust.Shared.Timing;
using Content.Client.Message;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories;

[UsedImplicitly]
public sealed class XenoSkinsBui : BoundUserInterface
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _rescache = default!;
    private readonly SpriteSystem _sprite;

    private Timer? _spriteDirectionTimer;

    [ViewVariables]
    private XenoSkinsWindow? _window;
    public XenoSkinsBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        _window = this.CreateWindow<XenoSkinsWindow>();

        if (EntMan.TryGetComponent(Owner, out XenoSkinsComponent? xenoSkins))
        {
            foreach (var skin in xenoSkins.Skins)
            {
                var skinIndex = _prototype.Index(skin);

                if (!EntMan.TryGetComponent(Owner, out XenoComponent? xeno))
                    return;

                if (xeno.Role.Id == skinIndex.Xeno.Id)
                    AddSkin(xenoSkins, skin);

            }

            _window.NoSkinsLabel.SetMarkupPermissive("[bold][color=red]There are no skins for your sub\nor you are on a xeno\nfor which there are none![/color][/bold]");
            _window.NoSkinsLabel.Visible = xenoSkins.Skins.Count == FixedPoint2.Zero;
            Logger.Debug($"{xenoSkins.Skins.Count == FixedPoint2.Zero}");
        }
    }
    private void AddSkin(XenoSkinsComponent comp, ProtoId<XenoSkinsPrototype> skinId)
    {
        var skin = _prototype.Index(skinId);

        var button = new XenoSkinsButton(skinId)
        {
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = comp.CurrentSkin == skin.ID,
            Text = Loc.GetString(skin.Name),
            Margin = new Thickness(5f, 5f),
        };
        button.OnToggled += args => SelectSkin(comp, skin.ID);
        // SendPredictedMessage(new XenoSkinsBuiMsg(skin));
        // SetSprite(skin);
        // Close();

        _window?.SkinsContainer.AddChild(button);
    }

    private void OnSpriteMouseEntered(GUIMouseHoverEventArgs args)
    {
        if (_window?.Mob.Sprite != null)
            _spriteDirectionTimer = new Timer(2000, true, UpdateSpriteDirection);

        Logger.Debug("вызван вход");
    }

    private void OnSpriteMouseExited(GUIMouseHoverEventArgs args)
    {
        _spriteDirectionTimer = null;
        Logger.Debug("вызван выход");
    }
    private void UpdateSpriteDirection()
    {
        if (_window?.Mob.Sprite != null)
            _window.Mob.OverrideDirection = (Direction)((int)(_window.Mob.OverrideDirection ?? 0) % 4 * 2);
        Logger.Debug("вызван обновление");

    }

    private void SetSprite(XenoSkinsPrototype skin)
    {
        _window?.Mob.SetEntity(Owner);
        if (_window?.Mob.Sprite == null)
            return;

        _window.Mob.Sprite.LayerSetRSI(0, skin.Rsi);
        _window.Mob.OnMouseEntered += OnSpriteMouseEntered;
        _window.Mob.OnMouseExited += OnSpriteMouseExited;
    }

    private void SelectSkin(XenoSkinsComponent comp, ProtoId<XenoSkinsPrototype> skin)
    {

        if (_window == null || _window.SkinsContainer == null)
            return;

        foreach (var item in _window.SkinsContainer.Children)
        {
            if (item is not XenoSkinsButton button)
                continue;

            button.Pressed = skin == button.Skin;

            var proto = _prototype.Index(skin);

            SetSprite(proto);

            _window.Select.Disabled = comp.CurrentSkin == skin;
            _window.Select.OnPressed += _ =>
            {
                SendPredictedMessage(new XenoSkinsBuiMsg(skin));
                Close();
            };
        }

    }
    private sealed partial class XenoSkinsButton(ProtoId<XenoSkinsPrototype> skin) : Button
    {
        public ProtoId<XenoSkinsPrototype> Skin = skin;
    }

}
