using System.Numerics;
using Content.Client._Stories.APC;
using Content.Shared._Stories.APC;
using Robust.Shared.Utility;

namespace Content.Client._Stories.APC;

[RegisterComponent, AutoGenerateComponentState]
[Access(typeof(APCAttachableHolderVisuals))]
public sealed partial class APCAttachableVisualsComponent : Component
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