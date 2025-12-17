namespace Content.Shared._Stories.Vehicle;

[RegisterComponent]
public sealed partial class VehicleInteriorDoorComponent : Component
{
    [DataField]
    public EntryDirection Side = EntryDirection.Back;
}
