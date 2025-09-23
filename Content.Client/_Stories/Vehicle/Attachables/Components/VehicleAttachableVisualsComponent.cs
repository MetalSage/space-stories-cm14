using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Vehicle.Attachables;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class VehicleAttachableVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath? Rsi;

    [DataField, AutoNetworkedField]
    public int Layer;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField, AutoNetworkedField]
    public string State = string.Empty;

    [DataField, AutoNetworkedField]
    public bool RedrawOnAppearanceChange = true;
}
