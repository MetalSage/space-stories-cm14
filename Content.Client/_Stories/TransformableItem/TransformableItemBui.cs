using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Stories.TransformableItem;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.TransformableItem;

[UsedImplicitly]
public sealed class TransformableItemBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private readonly TransformSystem _transform;

    private TransformableItemMenu? _menu;
    
    private readonly List<EntityUid> _previewEntities = new();

    public TransformableItemBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _transform = _entityManager.System<TransformSystem>();
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out TransformableItemComponent? transformable) ||
            transformable.Transformations.Count == 0)
        {
            return;
        }

        _menu = this.CreateWindow<TransformableItemMenu>();
        _menu.OnClose += Close;

        var container = new RadialContainer();
        _menu.AddChild(container);

        foreach (var transformationId in transformable.Transformations)
        {
            if (!_prototypes.TryIndex(transformationId, out var proto))
            {
                continue;
            }

            var previewEntity = _entityManager.Spawn(proto.ID);
            _previewEntities.Add(previewEntity);

            var button = new RadialMenuTextureButton
            {
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64, 64),
                ToolTip = proto.Name,
            };
            
            var spriteView = new SpriteView(previewEntity, _entityManager)
            {
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center,
                Scale = new Vector2(2f, 2f)
            };
            
            button.OnButtonDown += _ =>
            {
                SendPredictedMessage(new TransformableItemBuiMsg(transformationId));
                Close();
            };

            button.AddChild(spriteView);
            container.AddChild(button);
        }

        if (EntMan.EntityExists(Owner))
        {
            var ownerCoords = _transform.GetMapCoordinates(Owner);
            if (ownerCoords.MapId == _eye.CurrentMap)
            {
                var vpSize = _clyde.ScreenSize;
                var pos = _eye.WorldToScreen(ownerCoords.Position) / vpSize;
                _menu.OpenCenteredAt(pos);
            }
            else
            {
                _menu.OpenCentered();
            }
        }
        else
        {
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var entity in _previewEntities)
            {
                _entityManager.DeleteEntity(entity);
            }
            _previewEntities.Clear();

            _menu?.Dispose();
            _menu = null;
        }
    }
}
