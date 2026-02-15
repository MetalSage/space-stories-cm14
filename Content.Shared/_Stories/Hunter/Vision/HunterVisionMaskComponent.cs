using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Vision;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class HunterVisionMaskComponent : Component
{
    [DataField]
    public float EnergyUsage = 3.0f;

    [DataField] [AutoNetworkedField]
    public bool IsActive;

    [DataField] [AutoNetworkedField]
    public EntityUid? ToggleVisionAction;

    [DataField] [AutoNetworkedField]
    public EntProtoId ToggleVisionActionId = "STActionToggleHunterVision";

    [DataField]
    public SoundSpecifier VisorSwitchSound = new SoundPathSpecifier(
        "/Audio/_Stories/Effects/Hunter/pred_vision.ogg",
        AudioParams.Default.WithMaxDistance(2f)
    );

    [DataField]
    public SoundSpecifier ZoomInSound = new SoundPathSpecifier(
        "/Audio/_Stories/Effects/Hunter/pred_zoom_on.ogg",
        AudioParams.Default.WithMaxDistance(2f)
    );

    [DataField]
    public SoundSpecifier ZoomOutSound = new SoundPathSpecifier(
        "/Audio/_Stories/Effects/Hunter/pred_zoom_off.ogg",
        AudioParams.Default.WithMaxDistance(2f)
    );
}
