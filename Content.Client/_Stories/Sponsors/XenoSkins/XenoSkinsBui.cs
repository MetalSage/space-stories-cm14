using System.Linq;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Sponsors.XenoSkins;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Sponsors.XenoSkins;

[UsedImplicitly]
public sealed class XenoSkinsBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    private readonly SpriteSystem _spriteSystem;

    private ProtoId<XenoSkinsPrototype>? _selectedSkin;
    private Direction _previewRotation = Direction.North;
    private XenoSkinsWindow? _window;
    private EntityUid _previewEntity;

    public XenoSkinsBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _spriteSystem = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        _window = new XenoSkinsWindow();
        _window.Select.OnPressed += OnSelectPressed;
        _window.PrevDirection.OnPressed += _ =>
        {
            _previewRotation = _previewRotation.TurnCw();
            RotatePreview(_previewRotation);
        };
        _window.NextDirection.OnPressed += _ =>
        {
            _previewRotation = _previewRotation.TurnCcw();
            RotatePreview(_previewRotation);
        };

        InitializePreviewEntity();
        PopulateSkins();

        _window.OnClose += CloseAndCleanup;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void InitializePreviewEntity()
    {
        _previewEntity = EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        if (EntMan.TryGetComponent(Owner, out SpriteComponent? ownerSprite))
        {
            var previewSprite = EntMan.EnsureComponent<SpriteComponent>(_previewEntity);
            previewSprite.CopyFrom(ownerSprite);
        }
        _window!.Mob.SetEntity(_previewEntity);
    }

    private void PopulateSkins()
    {
        if (!EntMan.TryGetComponent(Owner, out XenoSkinsComponent? xenoSkins) ||
            !EntMan.TryGetComponent(Owner, out XenoComponent? xeno))
        {
            DisableSelectButton();
            return;
        }

        bool hasValidSkins = false;
        foreach (var skinId in xenoSkins.Skins)
        {
            var skinProto = _prototype.Index(skinId);
            if (xeno.Role.Id == skinProto.Xeno.Id)
            {
                AddSkinButton(xenoSkins, skinId);
                hasValidSkins = true;
            }
        }

        _window!.NoSkinsLabel.Visible = !hasValidSkins;
        _window.NoSkinsLabel.SetMarkupPermissive(Loc.GetString("ui-xeno-skins-none"));

        if (xenoSkins.CurrentSkin != null && _prototype.TryIndex(xenoSkins.CurrentSkin, out var currentSkin))
        {
            UpdatePreview(currentSkin);
            _selectedSkin = xenoSkins.CurrentSkin;
            _window.Select.Disabled = false;
        }
        else
        {
            DisableSelectButton();
        }
    }

    private void AddSkinButton(XenoSkinsComponent xenoSkins, ProtoId<XenoSkinsPrototype> skinId)
    {
        var skin = _prototype.Index(skinId);
        var button = new XenoSkinsButton(skinId)
        {
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = xenoSkins.CurrentSkin == skinId,
            Text = Loc.GetString(skin.Name),
            Margin = new Thickness(5f),
            StyleClasses = { StyleBase.ButtonOpenRight }
        };

        button.OnToggled += args =>
        {
            if (args.Pressed)
            {
                foreach (var child in _window!.SkinsContainer.Children)
                {
                    if (child is XenoSkinsButton otherButton && otherButton != button)
                        otherButton.Pressed = false;
                }
                SelectSkin(xenoSkins, skinId);
            }
            else
            {
                DeselectSkin();
            }
        };
        _window!.SkinsContainer.AddChild(button);
    }

    private void SelectSkin(XenoSkinsComponent xenoSkins, ProtoId<XenoSkinsPrototype> skinId)
    {
        if (!_prototype.TryIndex(skinId, out var skin))
            return;

        _selectedSkin = skinId;
        UpdatePreview(skin);
        _window!.Select.Disabled = xenoSkins.CurrentSkin == skinId;
    }

    private void DeselectSkin()
    {
        _selectedSkin = null;
        _window!.Select.Disabled = true;
    }

    private void OnSelectPressed(BaseButton.ButtonEventArgs _)
    {
        if (_selectedSkin == null)
            return;

        SendPredictedMessage(new XenoSkinsBuiMsg(_selectedSkin.Value));
        Close();
    }

    private void RotatePreview(Direction rotation)
    {
        // 0 = North, 2 = East, 4 = South, 6 = West
        _window!.Mob.OverrideDirection = (Direction)((int)rotation % 4 * 2);
    }

    private void UpdatePreview(XenoSkinsPrototype skin)
    {
        if (_window?.Mob.Sprite == null)
            return;

        _window.Mob.Sprite.LayerSetRSI(0, skin.SpriteRsi);
        _previewRotation = Direction.South;
        _window.Mob.OverrideDirection = _previewRotation;
    }

    private void DisableSelectButton()
    {
        _window!.Select.Disabled = true;
    }

    private void CloseAndCleanup()
    {
        EntMan.QueueDeleteEntity(_previewEntity);
        Close();
    }

    private sealed class XenoSkinsButton(ProtoId<XenoSkinsPrototype> skin) : Button
    {
        public ProtoId<XenoSkinsPrototype> Skin { get; } = skin;
    }
}
