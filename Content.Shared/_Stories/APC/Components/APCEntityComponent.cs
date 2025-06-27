using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCEntityComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapEnt;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? GridEnt;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? User;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Controller;

    [DataField, AutoNetworkedField]
    public bool Destroyed = false;

    [DataField, AutoNetworkedField]
    public float EntryDelay = 2f;

    [DataField, AutoNetworkedField]
    public int MaxOnAPC = 15;

    [DataField, AutoNetworkedField]
    public int OnAPC;

    [DataField, AutoNetworkedField]
    public string GridPath = "/Maps/Test/admin_test_arena.yml";

    [DataField, AutoNetworkedField]
    public string MovementSlot = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Hardpoints = new();
    
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ActiveHardpoint;
}
