using System.Numerics;
using Content.Client._RMC14.Attachable.Systems;
using Robust.Shared.Utility;
using Content.Shared._Stories.APC;

namespace Content.Client._Stories.APC.Modules;

[RegisterComponent, AutoGenerateComponentState]
[Access(typeof(APCModulesHolderVisuals))]
public sealed partial class APCModulesHolderVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ActiveLayers = new();
}
