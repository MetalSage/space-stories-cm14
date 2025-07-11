using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCEntityComponent : Component
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
    public Angle EntryInteractionRange = 45f;

    [DataField, AutoNetworkedField]
    public int MaxPassangers = 15;

    [DataField, AutoNetworkedField]
    public int Passangers;

    [DataField, AutoNetworkedField]
    public ResPath GridPath = new ResPath("/Maps/Test/admin_test_arena.yml");

    [DataField, AutoNetworkedField]
    public string MovementSlot = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Hardpoints = new();

    [ViewVariables, AutoNetworkedField]
    public EntityUid? ActiveHardpoint;

    [ViewVariables]
    public ContainerSlot AmmoStorage = default!;

    [ViewVariables, AutoNetworkedField]
    public string AmmoStorageID = "ammo-storage";
}
