using Content.Shared._Stories.APC.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Attachables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(APCAttachableHolderSystem), typeof(SharedAPCEntitySystem))]
public sealed partial class APCAttachableHolderComponent : Component
{
    /// <summary>
    ///     The key is one of the slot IDs at the bottom of this file.
    ///     Each key is followed by the description of the slot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, APCAttachableSlot> Slots = new();
}
