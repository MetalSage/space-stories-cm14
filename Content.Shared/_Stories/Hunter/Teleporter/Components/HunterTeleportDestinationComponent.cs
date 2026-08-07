namespace Content.Shared._Stories.Hunter.Teleporter.Components;

[RegisterComponent]
public sealed partial class HunterTeleportDestinationComponent : Component
{
    [DataField]
    public string? DestinationGroup;

    [DataField]
    public HunterTeleporterType TeleporterType;
}
