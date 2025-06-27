using System.Numerics;
using Content.Client._RMC14.Attachable.Systems;
using Robust.Shared.Utility;
using Content.Shared._Stories.APC;

namespace Content.Client._Stories.APC;

[RegisterComponent, AutoGenerateComponentState]
[Access(typeof(APCAttachableHolderVisuals))]
public sealed partial class APCAttachableHolderVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ActiveLayers = new();
}