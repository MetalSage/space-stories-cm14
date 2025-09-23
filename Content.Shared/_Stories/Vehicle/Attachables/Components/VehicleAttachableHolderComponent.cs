using Content.Shared._Stories.Vehicle.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Attachables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleAttachableHolderComponent : Component
{
    /// <summary>
    ///     The key is one of the slot IDs at the bottom of this file.
    ///     Each key is followed by the description of the slot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, VehicleAttachableSlot> Slots = new();
}
