using Robust.Shared.Utility;

namespace Content.Client._Stories.Vehicle.Attachables;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class VehicleAttachableHolderVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath Rsi;

    [DataField, AutoNetworkedField]
    public string DamagedState = string.Empty;

    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ActiveLayers = new();
}
