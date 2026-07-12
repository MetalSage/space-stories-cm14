using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Ordnance.Scanners;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DemolitionsScannerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? LastScanName;

    [DataField, AutoNetworkedField]
    public string? LastScanText;
}
