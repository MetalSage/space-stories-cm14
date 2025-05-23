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
    #region Entities
    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapEnt;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? GridEnt;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? User;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Controller;
    #endregion

    #region States
    [DataField, AutoNetworkedField]
    public string? BaseState;

    [DataField, AutoNetworkedField]
    public string? DestroyedState;

    [DataField, AutoNetworkedField]
    public bool Destroyed = false;
    #endregion

    #region Audio
    [DataField, AutoNetworkedField]
    public SoundSpecifier AccessDeniedSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier EntrySound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");
    #endregion

    #region APC Control
    [DataField, AutoNetworkedField]
    public EntProtoId APCControlReturnAction = "APCControlReturnAction";

    [DataField, AutoNetworkedField]
    public EntityUid? APCControlReturnActEntity;
    #endregion

    #region APC Parameters
    [DataField, AutoNetworkedField]
    public float EntryDelay = 2f;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxOnAPC = 3;

    [DataField, AutoNetworkedField]
    public FixedPoint2 OnAPC = FixedPoint2.Zero;

    [DataField, AutoNetworkedField]
    public string GridPath = "/Maps/Test/admin_test_arena.yml";
    #endregion

    #region Modules
    [DataField, AutoNetworkedField]
    public List<EntProtoId?> StartingModules = new();

    [DataField, AutoNetworkedField]
    public List<EntityUid> VirtualModules = new();

    [ViewVariables]
    public Container ModulesContainer = default!;

    [ViewVariables]
    public readonly string ModulesContainerId = "apc-modules-container";
    #endregion

    #region Prototypes
    #endregion
}
