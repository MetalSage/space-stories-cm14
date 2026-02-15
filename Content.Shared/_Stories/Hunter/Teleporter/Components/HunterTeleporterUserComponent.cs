namespace Content.Shared._Stories.Hunter.Teleporter.Components;

[RegisterComponent]
public sealed partial class HunterTeleporterUserComponent : Component
{
    public EntityUid? InteractingWith;
    public TimeSpan NextUse;
}
