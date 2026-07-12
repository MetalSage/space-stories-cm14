using Content.Shared._RMC14.Chemistry;
using Content.Shared.Chemistry;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Chemistry;

public sealed class RMCTankVisualizerSystem : VisualizerSystem<RMCTankVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, RMCTankVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<float>(uid, SolutionContainerVisuals.FillFraction, out var fraction, args.Component))
        {
            if (fraction <= 0f)
            {
                args.Sprite.LayerSetVisible(RMCTankVisualLayers.Meter, false);
            }
            else
            {
                args.Sprite.LayerSetVisible(RMCTankVisualLayers.Meter, true);
                var percent = fraction * 100f;
                string state = percent switch
                {
                    <= 20f => "t_20",
                    <= 40f => "t_40",
                    <= 60f => "t_60",
                    <= 80f => "t_80",
                    _ => "t_100"
                };
                args.Sprite.LayerSetState(RMCTankVisualLayers.Meter, state);
            }
        }

        if (AppearanceSystem.TryGetData<SolutionTransferDirection>(uid, RMCTankVisuals.TransferDirection, out var direction, args.Component))
        {
            args.Sprite.LayerSetVisible(RMCTankVisualLayers.TransferMode, true);
            args.Sprite.LayerSetState(RMCTankVisualLayers.TransferMode, direction == SolutionTransferDirection.Output ? "dispensing" : "filling");
        }
        else
        {
            args.Sprite.LayerSetVisible(RMCTankVisualLayers.TransferMode, false);
        }
    }
}
