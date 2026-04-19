using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpStickyDartComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId MineProto = default!;

    [DataField]
    public EntProtoId? IffDropProto;

    [DataField, AutoNetworkedField]
    public float ShortDelay = 2.5f;

    [DataField, AutoNetworkedField]
    public float LongDelay = 5f;

    public float? SelectedDelay;

    [DataField]
    public float ExplosionRadius;

    [DataField, AutoNetworkedField]
    public bool MineSpawned;

    [AutoNetworkedField]
    public bool Armed;

    [AutoNetworkedField]
    public TimeSpan DetonateAt;
}
