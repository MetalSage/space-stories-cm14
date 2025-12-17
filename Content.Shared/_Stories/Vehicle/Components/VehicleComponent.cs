using Content.Shared._RMC14.Stun;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

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
    public float EntryDelay = 0.5f;

    [DataField, AutoNetworkedField]
    public float EntryDelayXeno = 3f;

    [DataField, AutoNetworkedField]
    public float EntryDelayPulling = 2f;

    [DataField, AutoNetworkedField]
    public float EntryInteractionRange = 15f;

    [DataField, AutoNetworkedField]
    public bool Locked;

    [DataField, AutoNetworkedField]
    public ResPath GridPath = new ResPath("/Maps/_Stories/Vehicles/M577.yml");

    [DataField, AutoNetworkedField]
    public string MovementSlot = "st-vehicle-slot-treads";

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Hardpoints = new();

    [ViewVariables, AutoNetworkedField]
    public EntityUid? ActiveHardpoint;

    [DataField, AutoNetworkedField]
    public EntryDirection EntryDirections = EntryDirection.Left | EntryDirection.Right | EntryDirection.Back;

    [ViewVariables]
    public ContainerSlot AmmoStorage = default!;

    [ViewVariables, AutoNetworkedField]
    public string AmmoStorageID = "ammo-storage";

    [DataField, AutoNetworkedField]
    public Dictionary<string, float> DamageMults = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxHealth = FixedPoint2.New(1000);

    [DataField, AutoNetworkedField]
    public SlotCount PassengerSlots = new();

    [DataField, AutoNetworkedField]
    public List<RoleSlotGroup> RoleReservedSlots { get; set; } = new();

    [DataField, AutoNetworkedField]
    public SlotCount RevivableDeadSlots = new();

    [DataField, AutoNetworkedField]
    public SlotCount XenoSlots = new();

    [DataField, AutoNetworkedField]
    public RMCSizes SizeRequiredToHit = RMCSizes.Xeno;

    [DataField, AutoNetworkedField]
    public VehicleClass Class = VehicleClass.Light;

    [DataField, AutoNetworkedField]
    public FixedPoint2 WallRamDamage = 50;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SlotCount
{
    [DataField(required: true)]
    public int Current = 0;

    [DataField(required: true)]
    public int Max = 0;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class RoleSlotGroup
{
    [DataField(required: true)]
    public string CategoryName = default!;

    [DataField(required: true)]
    public List<string> Roles = new();

    [DataField(required: true)]
    public SlotCount Total = new();
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

[Flags]
public enum VehicleClass : byte
{
    Weak = 1 << 1,
    Light = 1 << 2,
    Medium = 1 << 3,
    Heavy = 1 << 4
}
