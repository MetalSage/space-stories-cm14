using System.Numerics;
using Content.Client._Stories.APC.Modules;
using Content.Shared._Stories.APC;
using Robust.Shared.Utility;

namespace Content.Client._Stories.APC.Modules;

[RegisterComponent, AutoGenerateComponentState]
[Access(typeof(APCModulesHolderVisuals))]
public sealed partial class APCModulesVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath? Rsi;

    [DataField, AutoNetworkedField]
    public int Layer;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;
}
