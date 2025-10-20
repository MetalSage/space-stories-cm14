namespace Content.Shared._Stories.Vehicle;

[RegisterComponent]
public sealed partial class VehicleEnterPointComponent : Component
{
    [DataField]
    public EntryDirection Direction = EntryDirection.Back;
}
