using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Spawning;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGridSpawnerSystem))]
public sealed partial class GridSpawnerComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ResPath? Spawn;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;
}
