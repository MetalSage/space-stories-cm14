using System.Numerics;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Popups;

/// <summary>
/// Draws popup text, either in world or on screen.
/// </summary>
public sealed class PopupOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly IConfigurationManager _configManager;
    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _playerMgr;
    private readonly IUserInterfaceManager _uiManager;
    private readonly PopupSystem _popup;
    private readonly PopupUIController _controller;
    private readonly ExamineSystemShared _examine;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PopupOverlay(
        IConfigurationManager configManager,
        IEntityManager entManager,
        IPlayerManager playerMgr,
        IPrototypeManager protoManager,
        IUserInterfaceManager uiManager,
        PopupUIController controller,
        ExamineSystemShared examine,
        SharedTransformSystem transform,
        PopupSystem popup)
    {
        _configManager = configManager;
        _entManager = entManager;
        _playerMgr = playerMgr;
        _uiManager = uiManager;
        _examine = examine;
        _transform = transform;
        _popup = popup;
        _controller = controller;

        _shader = protoManager.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);
        args.DrawingHandle.UseShader(_shader);
        var scale = _configManager.GetCVar(CVars.DisplayUIScale);

        if (scale == 0f)
            scale = _uiManager.DefaultUIScale;

        DrawWorld(args.ScreenHandle, args, scale);

        args.DrawingHandle.UseShader(null);
    }

    private void DrawWorld(DrawingHandleScreen worldHandle, OverlayDrawArgs args, float scale)
    {
        if (_popup.WorldLabels.Count == 0 || args.ViewportControl == null)
            return;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var ourEntity = _playerMgr.LocalEntity;
        // Stories-Popup-Fix-Start
        EntityUid? eyeTarget = null;
        if (ourEntity != null && 
            _entManager.TryGetComponent<EyeComponent>(ourEntity.Value, out var eyeComp) &&
            eyeComp.Target != null &&
            _entManager.EntityExists(eyeComp.Target.Value))
        {
            eyeTarget = eyeComp.Target;
        }

        var viewEntity = eyeTarget ?? ourEntity;
        var viewPos = new MapCoordinates(args.WorldAABB.Center, args.MapId);
        var ourPos = args.WorldBounds.Center;
        if (viewEntity != null && _entManager.EntityExists(viewEntity.Value))
        {
            if (_entManager.TryGetComponent<TransformComponent>(viewEntity.Value, out var xform))
            {
                viewPos = _transform.GetMapCoordinates(viewEntity.Value, xform);
                ourPos = viewPos.Position;
            }
        }
        // Stories-Popup-Fix-End

        foreach (var popup in _popup.WorldLabels)
        {
            if (popup.InitialPos.EntityId != EntityUid.Invalid && 
                !_entManager.EntityExists(popup.InitialPos.EntityId)) // Stories-Popup-Fix-End
            {
                continue;
            }

            var mapPos = _transform.ToMapCoordinates(popup.InitialPos);

            if (mapPos.MapId != args.MapId)
                continue;

            var distance = (mapPos.Position - ourPos).Length();

            if (!args.WorldBounds.Contains(mapPos.Position)) // Stories-Popup-Fix
                continue;

            // Stories-Popup-Fix-Start    
            if (viewEntity != null && _entManager.EntityExists(viewEntity.Value))
            {
                if (!_examine.InRangeUnOccluded(viewPos, mapPos, distance,
                        e => e == popup.InitialPos.EntityId || e == viewEntity, entMan: _entManager))
                    continue;
            }
            // Stories-Popup-Fix-End
            var pos = Vector2.Transform(mapPos.Position, matrix);
            _controller.DrawPopup(popup, worldHandle, pos, scale);
        }
    }
}
