namespace Content.Client._Stories.Vehicle.Attachables;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class VehicleAttachableHolderVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ActiveLayers = new();
}
