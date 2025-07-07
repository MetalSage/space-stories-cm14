namespace Content.Client._Stories.APC.Attachables;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class APCAttachableHolderVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ActiveLayers = new();
}
