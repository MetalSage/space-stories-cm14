using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Teleporter.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class HunterTeleporterComponent : Component
{
    [DataField] [AutoNetworkedField]
    public Dictionary<int, EntityUid> ActiveDestinations = new();

    [DataField(required: true)]
    public HunterTeleporterType TeleporterType;

    [DataField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier(
        "/Audio/Effects/teleport_arrival.ogg",
        AudioParams.Default.WithMaxDistance(5f)
    );
}

public enum HunterTeleporterType
{
    Normal,
    Youngblood,
}
