using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SynthGenerationComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId<SynthGenerationComponent>? Generation;

    [DataField, AutoNetworkedField]
    public EntProtoId GenerationAction = "ActionChooseGen";

    [DataField, AutoNetworkedField]
    public EntityUid? SelectGenerationActionEntity;

    [DataField]
    public ProtoId<DamageModifierSetPrototype>? DamageModifier;

    [DataField]
    public bool Selectable = true;
}
