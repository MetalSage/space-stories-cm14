using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Atmos;

public sealed partial class TileFireComponent
{
    [DataField, AutoNetworkedField]
    public float Intensity;

    [DataField, AutoNetworkedField]
    public float StoryDuration;

    [DataField, AutoNetworkedField]
    public bool IsPenetrating;
}
