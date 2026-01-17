using Robust.Shared.Audio;

namespace Content.Shared._Stories.Hunter.Teleporter.Components;

[RegisterComponent]
public sealed partial class HunterTeleporterBeaconComponent : Component
{
    [DataField]
    public SoundSpecifier ActivateSound = new SoundPathSpecifier(
        "/Audio/_Stories/Ambience/signal.ogg",
        AudioParams.Default.WithMaxDistance(5f)
    );

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(10);

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextUse;
}
