using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapEnt;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? GridEnt;

    [DataField, AutoNetworkedField]
    public bool Destroyed = false;

    [DataField, AutoNetworkedField]
    public float EntryDelay = 2f;

    [DataField, AutoNetworkedField]
    public float EntryInteractionRange = 45f;

    [DataField, AutoNetworkedField]
    public ResPath GridPath = new ResPath("/Maps/Test/admin_test_arena.yml");

    [DataField, AutoNetworkedField]
    public string MovementSlot = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Hardpoints = new();

    [ViewVariables, AutoNetworkedField]
    public EntityUid? ActiveHardpoint;

    [DataField, AutoNetworkedField]
    public EntryDirection EntryDirections = EntryDirection.Left | EntryDirection.Right;

    [ViewVariables]
    public ContainerSlot AmmoStorage = default!;

    [ViewVariables, AutoNetworkedField]
    public string AmmoStorageID = "ammo-storage";

    [DataField, AutoNetworkedField]
    public Dictionary<string, float> DamageMults = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 Health = default!;

    [DataField, AutoNetworkedField]
    public SlotCount PassengerSlots = new();

    [DataField, AutoNetworkedField]
    public Dictionary<string, SlotCount> RoleReservedSlots = new();

    [DataField, AutoNetworkedField]
    public SlotCount RevivableDeadSlots = new();

    [DataField, AutoNetworkedField]
    public SlotCount XenoSlots = new();

}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SlotCount
{
    [DataField(required: true)]
    public int Current = 0;

    [DataField(required: true)]
    public int Max = 0;
}
    
[Flags]
public enum EntryDirection : byte
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Front = 1 << 2,
    Back = 1 << 3
}
