using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Sponsors.XenoSkins;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Sponsors.XenoSkins;

[UsedImplicitly]
public sealed class XenoSkinsBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    private readonly SpriteSystem _spriteSystem;

    private ProtoId<XenoSkinsPrototype>? _selectedSkin;
    private Direction _previewRotation = Direction.South;
    private XenoSkinsWindow? _window;
    private EntityUid _previewEntity;

    private static readonly Direction[] CardinalCycle =
    {
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West
    };

    private int _currentCardinalIndex;

    public XenoSkinsBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _spriteSystem = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = new XenoSkinsWindow();

        _window.Select.OnPressed += OnSelectButtonPressed;
        _window.PrevDirection.OnPressed += OnPrevDirectionButtonPressed;
        _window.NextDirection.OnPressed += OnNextDirectionButtonPressed;
        _window.OnClose += OnGuiWindowManuallyClosed;

        _currentCardinalIndex = Array.IndexOf(CardinalCycle, _previewRotation);
        if (_currentCardinalIndex == -1)
        {
            _previewRotation = CardinalCycle[2];
            _currentCardinalIndex = 2;
        }

        InitializePreviewEntity();
        PopulateSkins();
        RotatePreview(_previewRotation);

        _window.OpenCentered();
    }

    private void OnSelectButtonPressed(BaseButton.ButtonEventArgs args)
    {
        if (_selectedSkin == null)
            return;

        SendPredictedMessage(new XenoSkinsBuiMsg(_selectedSkin.Value));
    }

    private void OnPrevDirectionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        _currentCardinalIndex = (_currentCardinalIndex - 1 + CardinalCycle.Length) % CardinalCycle.Length;
        _previewRotation = CardinalCycle[_currentCardinalIndex];
        RotatePreview(_previewRotation);
    }

    private void OnNextDirectionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        _currentCardinalIndex = (_currentCardinalIndex + 1) % CardinalCycle.Length;
        _previewRotation = CardinalCycle[_currentCardinalIndex];
        RotatePreview(_previewRotation);
    }

    private void OnGuiWindowManuallyClosed()
    {
        Close();
    }

    private void InitializePreviewEntity()
    {
        if (EntMan.EntityExists(_previewEntity))
        {
            EntMan.QueueDeleteEntity(_previewEntity);
            _previewEntity = EntityUid.Invalid;
        }

        _previewEntity = EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        if (EntMan.TryGetComponent(Owner, out SpriteComponent? ownerSprite) && _window != null)
        {
            var previewSprite = EntMan.EnsureComponent<SpriteComponent>(_previewEntity);
            previewSprite.CopyFrom(ownerSprite);
            _window.Mob.SetEntity(_previewEntity);
        }
    }

    private void PopulateSkins()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out XenoSkinsComponent? xenoSkins) ||
            !EntMan.TryGetComponent(Owner, out XenoComponent? xeno))
        {
            _window.Select.Disabled = true;
            _window.NoSkinsLabel.Visible = true;
            _window.NoSkinsLabel.SetMarkupPermissive(Loc.GetString("ui-xeno-skins-none"));
            return;
        }

        bool hasValidSkins = false;
        _window.SkinsContainer.DisposeAllChildren();

        foreach (var skinId in xenoSkins.Skins)
        {
            if (!_prototype.TryIndex(skinId, out var skinProto) || xeno.Role.Id != skinProto.Xeno.Id)
                continue;

            AddSkinButtonToList(xenoSkins, skinId, skinProto);
            hasValidSkins = true;
        }

        _window.NoSkinsLabel.Visible = !hasValidSkins;
        if (!hasValidSkins)
            _window.NoSkinsLabel.SetMarkupPermissive(Loc.GetString("ui-xeno-skins-none"));


        if (xenoSkins.CurrentSkin != null && _prototype.TryIndex(xenoSkins.CurrentSkin, out var currentSkinProto))
        {
            _selectedSkin = xenoSkins.CurrentSkin;
            UpdatePreview(currentSkinProto);
        }
        else
        {
            _selectedSkin = null;
            if (EntMan.EntityExists(_previewEntity))
            {
                _previewRotation = CardinalCycle[_currentCardinalIndex];
                RotatePreview(_previewRotation);
            }
        }

        UpdateSelectButtonState(xenoSkins);
    }

    private void AddSkinButtonToList(XenoSkinsComponent xenoSkins,
        ProtoId<XenoSkinsPrototype> skinId,
        XenoSkinsPrototype skinProto)
    {
        if (_window == null)
            return;

        var button = new XenoSkinsButton(skinId)
        {
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = (_selectedSkin == skinId) || (_selectedSkin == null && xenoSkins.CurrentSkin == skinId),
            Text = Loc.GetString(skinProto.Name),
            Margin = new Thickness(5f),
            StyleClasses = { StyleBase.ButtonOpenRight }
        };

        button.OnToggled += args =>
        {
            if (_window == null)
                return;

            if (args.Pressed)
            {
                foreach (var child in _window.SkinsContainer.Children)
                {
                    if (child is XenoSkinsButton otherButton && otherButton != button)
                        otherButton.Pressed = false;
                }

                HandleSkinSelection(xenoSkins, skinId);
            }
            else
            {
                if (_selectedSkin == skinId)
                {
                    HandleSkinDeselection(xenoSkins);
                }
            }
        };
        _window.SkinsContainer.AddChild(button);
    }

    private void HandleSkinSelection(XenoSkinsComponent xenoSkins, ProtoId<XenoSkinsPrototype> skinId)
    {
        if (!_prototype.TryIndex(skinId, out var skinProto))
            return;

        _selectedSkin = skinId;
        UpdatePreview(skinProto);
        UpdateSelectButtonState(xenoSkins);
    }

    private void HandleSkinDeselection(XenoSkinsComponent xenoSkins)
    {
        _selectedSkin = null;

        if (_window == null)
            return;

        if (xenoSkins.CurrentSkin != null && _prototype.TryIndex(xenoSkins.CurrentSkin, out var currentSkinProto))
        {
            UpdatePreview(currentSkinProto);
        }
        else if (EntMan.TryGetComponent(Owner, out SpriteComponent? ownerSprite) && _window.Mob.Sprite != null)
        {
            _window.Mob.Sprite.CopyFrom(ownerSprite);
            _previewRotation = CardinalCycle[_currentCardinalIndex];
            RotatePreview(_previewRotation);
        }

        UpdateSelectButtonState(xenoSkins);
    }


    private void UpdateSelectButtonState(XenoSkinsComponent xenoSkins)
    {
        if (_window == null)
            return;
        _window.Select.Disabled = _selectedSkin == null || _selectedSkin == xenoSkins.CurrentSkin;
    }

    private void RotatePreview(Direction rotation)
    {
        if (_window?.Mob == null)
            return;
        _window.Mob.OverrideDirection = rotation;
    }

    private void UpdatePreview(XenoSkinsPrototype skin)
    {
        if (_window?.Mob.Sprite == null)
            return;

        _window.Mob.Sprite.LayerSetRSI(0, skin.SpriteRsi);
        _previewRotation = CardinalCycle[_currentCardinalIndex];
        RotatePreview(_previewRotation);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (EntMan.EntityExists(_previewEntity))
        {
            EntMan.QueueDeleteEntity(_previewEntity);
            _previewEntity = EntityUid.Invalid;
        }

        if (_window != null)
        {
            _window.Select.OnPressed -= OnSelectButtonPressed;
            _window.PrevDirection.OnPressed -= OnPrevDirectionButtonPressed;
            _window.NextDirection.OnPressed -= OnNextDirectionButtonPressed;
            _window.OnClose -= OnGuiWindowManuallyClosed;

            _window.Dispose();
            _window = null;
        }
    }

    private sealed class XenoSkinsButton : Button
    {
        public ProtoId<XenoSkinsPrototype> Skin { get; }

        public XenoSkinsButton(ProtoId<XenoSkinsPrototype> skin)
        {
            Skin = skin;
        }
    }
}
