using Robust.Shared.GameStates;
using System.Numerics;
using Robust.Shared.Prototypes;
namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCModuleComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public APCModuleType ModuleType;

    [ViewVariables]
    public EntityUid? VirtualModuleEnt;

    [ViewVariables]
    public EntityUid? APC;

    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2i.Zero;

    [DataField, AutoNetworkedField]
    public EntProtoId? VirtualModule;
    
    [DataField, AutoNetworkedField]
    public float AttachTime = 0f;

    [DataField, AutoNetworkedField]
    public float DeattachTime = 0f;
}

public enum APCModuleType
{
    Weapon,
    Movement,
    ExtraWeaponLeft,
    ExtraWeaponRight,
    ExtraWeaponForward,
    ExtraWeaponTop
}