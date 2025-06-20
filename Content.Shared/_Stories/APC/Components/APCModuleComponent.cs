using Robust.Shared.GameStates;
using System.Numerics;
using Robust.Shared.Prototypes;
namespace Content.Shared._Stories.APC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class APCModuleComponent : Component
{
    [ViewVariables]
    public EntityUid? APC;

    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2i.Zero;
    
    [DataField, AutoNetworkedField]
    public float AttachTime = 0f;

    [DataField, AutoNetworkedField]
    public float DeattachTime = 0f;
}