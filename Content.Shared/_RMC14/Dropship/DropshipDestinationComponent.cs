using Content.Shared._RMC14.Spawning;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem), typeof(SharedGridSpawnerSystem))]
public sealed partial class DropshipDestinationComponent : Component
{
    [DataField]
    public ResPath? Spawn;

    [DataField]
    public Box2? DockBounds;

    [DataField, AutoNetworkedField]
    public EntityUid? Ship;

    [DataField, AutoNetworkedField]
    public bool AutoRecall;

    [DataField, AutoNetworkedField]
    public int LightSearchRadius = 14;

    [DataField, AutoNetworkedField]
    public EntityUid? ArrivalSoundEntity;
}
